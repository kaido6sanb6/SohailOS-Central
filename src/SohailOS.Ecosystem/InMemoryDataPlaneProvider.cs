namespace SohailOS.Ecosystem;

public class InMemoryDataPlaneProvider : DataPlaneProvider
{
    private readonly object _gate = new();
    private readonly Dictionary<string, RepositoryIdentity> _repositories = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SourceRevision> _revisions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DocumentRecord> _documents = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ChunkRecord> _chunks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, float[]> _embeddings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EmbeddingGeneration> _generations = new(StringComparer.Ordinal);
    private readonly List<KnowledgeRunRecord> _runs = [];
    private readonly HashSet<string> _tombstones = new(StringComparer.Ordinal);

    public virtual Task<KnowledgeHealthStatus> HealthCheckAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new KnowledgeHealthStatus(true, GetType().Name));

    public virtual Task<KnowledgeProviderCapabilities> CapabilitiesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new KnowledgeProviderCapabilities(true, true, false, _generations.Values.Select(x => x.Dimensions).DefaultIfEmpty(0).Max()));

    public virtual Task<KnowledgeWriteResult> MigrateAsync(string targetVersion, CancellationToken cancellationToken = default) =>
        Task.FromResult(new KnowledgeWriteResult(true, 0, $"Gate-0 provider ready for schema {targetVersion}."));

    public virtual Task<KnowledgeWriteResult> UpsertRepositoriesAsync(IEnumerable<RepositoryIdentity> repositories, CancellationToken cancellationToken = default)
    {
        var items = repositories.ToArray();
        lock (_gate)
            foreach (var item in items)
                _repositories[item.RepoId] = item;
        return Task.FromResult(new KnowledgeWriteResult(true, items.Length));
    }

    public virtual Task<KnowledgeWriteResult> UpsertRevisionsAsync(IEnumerable<SourceRevision> revisions, CancellationToken cancellationToken = default)
    {
        var items = revisions.ToArray();
        lock (_gate)
            foreach (var item in items)
                _revisions[item.RevisionId] = item;
        return Task.FromResult(new KnowledgeWriteResult(true, items.Length));
    }

    public virtual Task<KnowledgeWriteResult> UpsertDocumentsAsync(IEnumerable<DocumentRecord> documents, CancellationToken cancellationToken = default)
    {
        var items = documents.ToArray();
        lock (_gate)
            foreach (var item in items)
                _documents[item.DocId] = item;
        return Task.FromResult(new KnowledgeWriteResult(true, items.Length));
    }

    public virtual Task<KnowledgeWriteResult> UpsertChunksAsync(IEnumerable<ChunkRecord> chunks, CancellationToken cancellationToken = default)
    {
        var items = chunks.ToArray();
        lock (_gate)
            foreach (var item in items)
                _chunks[item.ChunkId] = item;
        return Task.FromResult(new KnowledgeWriteResult(true, items.Length));
    }

    public virtual Task<KnowledgeWriteResult> UpsertEmbeddingsAsync(string generationId, IEnumerable<(string ChunkId, float[] Vector)> items, CancellationToken cancellationToken = default)
    {
        var values = items.ToArray();
        lock (_gate)
        {
            foreach (var item in values)
                _embeddings[$"{generationId}|{item.ChunkId}"] = item.Vector.ToArray();

            if (!_generations.ContainsKey(generationId))
            {
                var dimensions = values.Select(x => x.Vector.Length).FirstOrDefault();
                var parts = generationId.Split(':', 5);
                _generations[generationId] = new EmbeddingGeneration(
                    generationId,
                    parts.Length > 1 ? parts[1] : "unknown",
                    parts.Length > 2 ? parts[2] : "unknown",
                    dimensions,
                    parts.Length > 4 ? parts[4] : "unknown");
            }
        }

        return Task.FromResult(new KnowledgeWriteResult(true, values.Length));
    }

    public virtual Task<KnowledgeWriteResult> TombstoneAsync(IEnumerable<string> ids, string reason, CancellationToken cancellationToken = default)
    {
        var values = ids.Distinct(StringComparer.Ordinal).ToArray();
        lock (_gate)
        {
            foreach (var id in values)
            {
                if (id.StartsWith("gh:doc:", StringComparison.Ordinal))
                {
                    if (_documents.TryGetValue(id, out var doc))
                        _documents[id] = doc with { Deleted = true, DeletedAt = DateTimeOffset.UtcNow };

                    foreach (var revision in _revisions.Values.Where(x => x.Path == id[(id.IndexOf(':', 7) + 1)..]).ToArray())
                    {
                        _revisions[revision.RevisionId] = revision with
                        {
                            State = KnowledgeIndexState.Tombstoned,
                            FailureReason = reason
                        };
                        RemoveRevisionContent(revision.RevisionId);
                    }
                }
                else if (id.StartsWith("gh:rev:", StringComparison.Ordinal))
                {
                    if (_revisions.TryGetValue(id, out var revision))
                        _revisions[id] = revision with { State = KnowledgeIndexState.Tombstoned, FailureReason = reason };
                    RemoveRevisionContent(id);
                }
                else if (id.StartsWith("gh:repo:", StringComparison.Ordinal))
                {
                    foreach (var doc in _documents.Values.Where(x => x.RepoId == id).Select(x => x.DocId).ToArray())
                        TombstoneDocument(doc, reason);
                }
                else if (id.StartsWith("gh:chunk:", StringComparison.Ordinal))
                {
                    _chunks.Remove(id);
                    foreach (var key in _embeddings.Keys.Where(x => x.EndsWith("|" + id, StringComparison.Ordinal)).ToArray())
                        _embeddings.Remove(key);
                }

                _tombstones.Add(id);
            }
        }

        return Task.FromResult(new KnowledgeWriteResult(true, values.Length));
    }

    public virtual Task<KnowledgeWriteResult> RecordRunAsync(KnowledgeRunRecord run, CancellationToken cancellationToken = default)
    {
        lock (_gate)
            _runs.Add(run);
        return Task.FromResult(new KnowledgeWriteResult(true, 1));
    }

    public virtual Task<KnowledgeWriteResult> SetRevisionStateAsync(string revisionId, KnowledgeIndexState state, string? reason = null, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_revisions.TryGetValue(revisionId, out var revision))
                return Task.FromResult(new KnowledgeWriteResult(false, 0, $"Revision not found: {revisionId}."));
            _revisions[revisionId] = revision with { State = state, FailureReason = reason };
        }
        return Task.FromResult(new KnowledgeWriteResult(true, 1));
    }

    public virtual Task<IReadOnlyList<KnowledgeHit>> LexicalSearchAsync(string query, KnowledgeFilters filters, int k, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || k <= 0)
            return Task.FromResult<IReadOnlyList<KnowledgeHit>>([]);

        var terms = Tokenize(query);
        lock (_gate)
        {
            var hits = _chunks.Values
                .Select(chunk => BuildLexicalHit(chunk, terms, filters))
                .Where(x => x is not null)
                .Cast<KnowledgeHit>()
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Provenance.ChunkId, StringComparer.Ordinal)
                .Take(k)
                .ToArray();

            return Task.FromResult<IReadOnlyList<KnowledgeHit>>(hits);
        }
    }

    public virtual Task<IReadOnlyList<KnowledgeHit>> VectorSearchAsync(float[] vector, string generationId, KnowledgeFilters filters, int k, CancellationToken cancellationToken = default)
    {
        if (vector is null || vector.Length == 0 || string.IsNullOrWhiteSpace(generationId) || k <= 0)
            return Task.FromResult<IReadOnlyList<KnowledgeHit>>([]);

        lock (_gate)
        {
            var query = Normalize(vector);
            var hits = new List<KnowledgeHit>();

            foreach (var pair in _embeddings)
            {
                var prefix = generationId + "|";
                if (!pair.Key.StartsWith(prefix, StringComparison.Ordinal))
                    continue;

                var chunkId = pair.Key[prefix.Length..];
                if (!_chunks.TryGetValue(chunkId, out var chunk) ||
                    !_documents.TryGetValue(chunk.DocId, out var document) ||
                    !_revisions.TryGetValue(chunk.RevisionId, out var revision) ||
                    !_repositories.TryGetValue(document.RepoId, out var repository) ||
                    document.Deleted ||
                    document.CurrentRevisionId != chunk.RevisionId ||
                    revision.State == KnowledgeIndexState.Tombstoned ||
                    !Matches(filters, repository, document))
                    continue;

                var candidate = Normalize(pair.Value);
                if (candidate.Length != query.Length)
                    continue;

                hits.Add(CreateHit(repository, document, revision, chunk, Dot(query, candidate), "vector", generationId));
            }

            return Task.FromResult<IReadOnlyList<KnowledgeHit>>(
                hits.OrderByDescending(x => x.Score)
                    .ThenBy(x => x.Provenance.ChunkId, StringComparer.Ordinal)
                    .Take(k)
                    .ToArray());
        }
    }

    public virtual Task<KnowledgeDocumentResult?> GetDocumentAsync(string docId, string? asOf, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_documents.TryGetValue(docId, out var document) || document.Deleted)
                return Task.FromResult<KnowledgeDocumentResult?>(null);

            var revision = ResolveRevision(document, asOf);
            return Task.FromResult<KnowledgeDocumentResult?>(
                revision is null ? null : BuildDocumentResult(document, revision, string.IsNullOrWhiteSpace(asOf) || asOf == "current" ? "current" : "as_of"));
        }
    }

    public virtual Task<KnowledgeDocumentResult?> GetDocumentAtCommitAsync(string docId, string gitOid, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_documents.TryGetValue(docId, out var document) || document.Deleted)
                return Task.FromResult<KnowledgeDocumentResult?>(null);

            var revision = _revisions.Values
                .Where(x => x.RepoId == document.RepoId &&
                            x.Path == document.NormalizedPath &&
                            x.GitOid == gitOid &&
                            x.State != KnowledgeIndexState.Tombstoned)
                .OrderByDescending(x => x.FetchedAt)
                .FirstOrDefault();

            return Task.FromResult<KnowledgeDocumentResult?>(
                revision is null ? null : BuildDocumentResult(document, revision, "at_commit"));
        }
    }

    public virtual Task<IReadOnlyList<DocumentRecord>> ListDocumentsAsync(string repoId, bool includeDeleted, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var items = _documents.Values
                .Where(x => x.RepoId == repoId && (includeDeleted || !x.Deleted))
                .OrderBy(x => x.NormalizedPath, StringComparer.Ordinal)
                .ToArray();
            return Task.FromResult<IReadOnlyList<DocumentRecord>>(items);
        }
    }

    public virtual Task<KnowledgeSourcePointer?> GetSourceAsync(string revisionId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_revisions.TryGetValue(revisionId, out var revision) ||
                !_repositories.TryGetValue(revision.RepoId, out var repository))
                return Task.FromResult<KnowledgeSourcePointer?>(null);

            return Task.FromResult<KnowledgeSourcePointer?>(
                new KnowledgeSourcePointer(
                    revision.RepoId,
                    repository.FullName,
                    revision.Path,
                    revision.GitOid,
                    revision.BlobSha,
                    $"https://github.com/{repository.FullName}/blob/{revision.GitOid}/{revision.Path}",
                    repository.TrustTier));
        }
    }

    public virtual Task<IReadOnlyList<EmbeddingGeneration>> ListGenerationsAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
            return Task.FromResult<IReadOnlyList<EmbeddingGeneration>>(_generations.Values.OrderBy(x => x.Id).ToArray());
    }

    protected sealed record KnowledgeIndexSnapshot(
        List<RepositoryIdentity> Repositories,
        List<SourceRevision> Revisions,
        List<DocumentRecord> Documents,
        List<ChunkRecord> Chunks,
        Dictionary<string, float[]> Embeddings,
        List<EmbeddingGeneration> Generations,
        List<KnowledgeRunRecord> Runs,
        HashSet<string> Tombstones);

    protected KnowledgeIndexSnapshot ExportSnapshot()
    {
        lock (_gate)
        {
            return new KnowledgeIndexSnapshot(
                _repositories.Values.ToList(),
                _revisions.Values.ToList(),
                _documents.Values.ToList(),
                _chunks.Values.ToList(),
                _embeddings.ToDictionary(x => x.Key, x => x.Value.ToArray(), StringComparer.Ordinal),
                _generations.Values.ToList(),
                _runs.ToList(),
                new HashSet<string>(_tombstones, StringComparer.Ordinal));
        }
    }

    protected void ImportSnapshot(KnowledgeIndexSnapshot snapshot)
    {
        lock (_gate)
        {
            foreach (var x in snapshot.Repositories) _repositories[x.RepoId] = x;
            foreach (var x in snapshot.Revisions) _revisions[x.RevisionId] = x;
            foreach (var x in snapshot.Documents) _documents[x.DocId] = x;
            foreach (var x in snapshot.Chunks) _chunks[x.ChunkId] = x;
            foreach (var x in snapshot.Embeddings) _embeddings[x.Key] = x.Value;
            foreach (var x in snapshot.Generations) _generations[x.Id] = x;
            _runs.AddRange(snapshot.Runs);
            foreach (var x in snapshot.Tombstones) _tombstones.Add(x);
        }
    }

    private KnowledgeHit? BuildLexicalHit(ChunkRecord chunk, IReadOnlyList<string> terms, KnowledgeFilters filters)
    {
        if (!_documents.TryGetValue(chunk.DocId, out var document) ||
            document.Deleted ||
            document.CurrentRevisionId != chunk.RevisionId ||
            !_revisions.TryGetValue(chunk.RevisionId, out var revision) ||
            revision.State == KnowledgeIndexState.Tombstoned ||
            !_repositories.TryGetValue(document.RepoId, out var repository) ||
            !Matches(filters, repository, document))
            return null;

        var normalizedText = chunk.Text.ToLowerInvariant();
        var occurrences = terms.Sum(term => CountOccurrences(normalizedText, term));
        if (occurrences == 0)
            return null;

        var pathBonus = terms.Count(term => document.NormalizedPath.Contains(term, StringComparison.OrdinalIgnoreCase));
        return CreateHit(repository, document, revision, chunk, occurrences + (pathBonus * 2), "lexical", null);
    }

    private KnowledgeDocumentResult BuildDocumentResult(DocumentRecord document, SourceRevision revision, string mode)
    {
        var chunks = _chunks.Values
            .Where(x => x.RevisionId == revision.RevisionId)
            .OrderBy(x => x.Ordinal)
            .ToArray();

        return new KnowledgeDocumentResult(document, chunks, mode, []);
    }

    private KnowledgeHit CreateHit(
        RepositoryIdentity repository,
        DocumentRecord document,
        SourceRevision revision,
        ChunkRecord chunk,
        double score,
        string source,
        string? generation)
    {
        var provenance = new ProvenanceEnvelope(
            repository.RepoId,
            repository.FullName,
            document.NormalizedPath,
            revision.GitOid,
            revision.BlobSha,
            revision.ContentHash,
            chunk.ChunkId,
            chunk.Ordinal,
            chunk.ByteStart,
            chunk.ByteEnd,
            revision.FetchedAt,
            generation,
            repository.TrustTier,
            [],
            $"https://github.com/{repository.FullName}/blob/{revision.GitOid}/{document.NormalizedPath}");

        return new KnowledgeHit(provenance, chunk.Text, score, source, chunk.Language);
    }

    private bool Matches(KnowledgeFilters filters, RepositoryIdentity repository, DocumentRecord document)
    {
        if (repository.TrustTier == KnowledgeTrustTier.Tombstoned)
            return false;

        if (filters.TrustTiers is not null && !filters.TrustTiers.Contains(repository.TrustTier))
            return false;

        if (filters.RepositoryIds is not null && !filters.RepositoryIds.Contains(repository.RepoId))
            return false;

        if (filters.SourceTypes is not null &&
            !filters.SourceTypes.Contains(document.SourceType))
            return false;

        return true;
    }

    private SourceRevision? ResolveRevision(DocumentRecord document, string? asOf)
    {
        var revisions = _revisions.Values
            .Where(x => x.RepoId == document.RepoId &&
                        x.Path == document.NormalizedPath &&
                        x.State != KnowledgeIndexState.Tombstoned);

        if (string.IsNullOrWhiteSpace(asOf) || asOf == "current")
            return revisions.FirstOrDefault(x => x.RevisionId == document.CurrentRevisionId);

        if (!DateTimeOffset.TryParse(asOf, out var moment))
            return null;

        return revisions
            .Where(x => x.FetchedAt <= moment)
            .OrderByDescending(x => x.FetchedAt)
            .FirstOrDefault();
    }

    private void TombstoneDocument(string docId, string reason)
    {
        if (_documents.TryGetValue(docId, out var document))
        {
            _documents[docId] = document with { Deleted = true, DeletedAt = DateTimeOffset.UtcNow };
            foreach (var revision in _revisions.Values.Where(x => x.RepoId == document.RepoId && x.Path == document.NormalizedPath).ToArray())
            {
                _revisions[revision.RevisionId] = revision with { State = KnowledgeIndexState.Tombstoned, FailureReason = reason };
                RemoveRevisionContent(revision.RevisionId);
            }
        }
        _tombstones.Add(docId);
    }

    private void RemoveRevisionContent(string revisionId)
    {
        foreach (var chunk in _chunks.Values.Where(x => x.RevisionId == revisionId).ToArray())
        {
            _chunks.Remove(chunk.ChunkId);
            foreach (var key in _embeddings.Keys.Where(x => x.EndsWith("|" + chunk.ChunkId, StringComparison.Ordinal)).ToArray())
                _embeddings.Remove(key);
        }
    }

    private static IReadOnlyList<string> Tokenize(string query) =>
        query.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => new string(x.Where(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-' or '.').ToArray()).ToLowerInvariant())
            .Where(x => x.Length >= 2)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static int CountOccurrences(string text, string term)
    {
        var count = 0;
        var start = 0;
        while ((start = text.IndexOf(term, start, StringComparison.Ordinal)) >= 0)
        {
            count++;
            start += term.Length;
        }
        return count;
    }

    private static float[] Normalize(float[] vector)
    {
        var norm = Math.Sqrt(vector.Sum(x => (double)x * x));
        if (norm <= double.Epsilon)
            return vector.Select(_ => 0f).ToArray();
        return vector.Select(x => (float)(x / norm)).ToArray();
    }

    private static double Dot(float[] left, float[] right)
    {
        var sum = 0d;
        for (var i = 0; i < left.Length; i++)
            sum += left[i] * right[i];
        return sum;
    }
}
