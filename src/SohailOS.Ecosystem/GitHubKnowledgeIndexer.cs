using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace SohailOS.Ecosystem;

public sealed class GitHubKnowledgeIndexer
{
    private const long MaxTextBytes = 8L * 1024L * 1024;

    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "bin", "obj", "node_modules", "vendor", "dist", "build", "coverage", ".next"
    };

    private readonly GitHubKnowledgeSourceClient _source;
    private readonly DataPlaneProvider _provider;
    private readonly IEmbeddingProvider _embeddingProvider;

    public GitHubKnowledgeIndexer(
        GitHubKnowledgeSourceClient source,
        DataPlaneProvider provider,
        IEmbeddingProvider? embeddingProvider = null)
    {
        _source = source;
        _provider = provider;
        _embeddingProvider = embeddingProvider ?? new NullEmbeddingProvider();
    }

    public async Task<KnowledgeRunRecord> IndexRepositoryAsync(
        LiveRepository repository,
        KnowledgeTrustTier trustTier,
        CancellationToken cancellationToken = default)
    {
        var started = DateTimeOffset.UtcNow;
        var runId = KnowledgeIdentity.RunId(started);
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var degraded = new HashSet<string>(StringComparer.Ordinal);

        void Count(string key) =>
            counts[key] = counts.TryGetValue(key, out var value) ? value + 1 : 1;

        void Stage(string stage) => Count($"pipeline:{stage}");

        var state = KnowledgeIndexState.Discovered;
        string? gitOid = null;

        async Task MoveAsync(KnowledgeIndexState next, string? reason = null)
        {
            IndexStateMachine.EnsureTransition(state, next);
            state = next;
            Count($"state:{next}");
            await _provider.RecordRunAsync(
                new KnowledgeRunRecord(
                    runId,
                    repository.Id is long id ? KnowledgeIdentity.RepositoryId(id) : null,
                    gitOid,
                    state,
                    started,
                    null,
                    new Dictionary<string, long>(),
                    counts,
                    degraded,
                    reason),
                cancellationToken);
        }

        try
        {
            if (repository.Id is not long repositoryId)
                throw new InvalidDataException($"Repository {repository.FullName} has no numeric GitHub ID.");

            await MoveAsync(KnowledgeIndexState.Authorized);
            await MoveAsync(KnowledgeIndexState.Queued);
            await MoveAsync(KnowledgeIndexState.Fetching);
            Stage("fetch");

            await _provider.UpsertRepositoriesAsync(
                [new RepositoryIdentity(
                    KnowledgeIdentity.RepositoryId(repositoryId),
                    repositoryId,
                    repository.FullName,
                    repository.DefaultBranch,
                    repository.IsPrivate,
                    trustTier,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow)],
                cancellationToken);

            var snapshot = await _source.GetRepositorySnapshotAsync(repository, cancellationToken);
            gitOid = snapshot.GitOid;

            await MoveAsync(KnowledgeIndexState.Parsing);
            Stage("inspect");
            var (entries, truncated) = await _source.GetTreeAsync(repository, snapshot.GitOid, cancellationToken);
            if (truncated)
                degraded.Add("partial_index");

            await MoveAsync(KnowledgeIndexState.Chunking);
            Stage("classify");
            Stage("extract");
            Stage("normalize");

            var seenPaths = new HashSet<string>(StringComparer.Ordinal);
            var repositoriesToUpsert = new List<DocumentRecord>();
            var revisionsToUpsert = new List<SourceRevision>();
            var chunksToUpsert = new List<ChunkRecord>();

            foreach (var entry in entries.OrderBy(x => x.Path, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!IsEligible(entry.Path, entry.Size))
                {
                    Count("excluded");
                    continue;
                }

                var path = KnowledgeIdentity.NormalizePath(entry.Path);
                seenPaths.Add(path);

                var docId = KnowledgeIdentity.DocumentId(repositoryId, path);
                var existing = await _provider.GetDocumentAsync(docId, "current", cancellationToken);
                if (existing?.Document.CurrentRevisionId is not null)
                {
                    var pointer = await _provider.GetSourceAsync(
                        existing.Document.CurrentRevisionId,
                        cancellationToken);

                    if (pointer is not null &&
                        string.Equals(pointer.BlobSha, entry.Sha, StringComparison.Ordinal))
                    {
                        Count("unchanged");
                        continue;
                    }

                    if (pointer is not null)
                    {
                        await _provider.SetRevisionStateAsync(
                            existing.Document.CurrentRevisionId,
                            KnowledgeIndexState.Superseded,
                            "New default-branch revision indexed.",
                            cancellationToken);
                    }
                }

                var bytes = await _source.GetBlobAsync(repository, entry.Sha, cancellationToken);
                if (LooksBinary(bytes))
                {
                    Count("binary_skipped");
                    continue;
                }

                var contentHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                Stage("verify");
                var text = Encoding.UTF8.GetString(bytes);
                var language = DetectLanguage(path);
                var revisionId = KnowledgeIdentity.RevisionId(snapshot.GitOid, path);

                var revision = new SourceRevision(
                    revisionId,
                    KnowledgeIdentity.RepositoryId(repositoryId),
                    snapshot.GitOid,
                    path,
                    entry.Sha,
                    contentHash,
                    bytes.LongLength,
                    DateTimeOffset.UtcNow,
                    KnowledgeIndexState.Chunking);

                var document = new DocumentRecord(
                    docId,
                    KnowledgeIdentity.RepositoryId(repositoryId),
                    path,
                    revisionId,
                    language,
                    false,
                    false,
                    null);

                var chunks = KnowledgeChunker.Chunk(
                    text,
                    docId,
                    revisionId,
                    language);
                Stage("synthesize");

                repositoriesToUpsert.Add(document);
                revisionsToUpsert.Add(revision);
                chunksToUpsert.AddRange(chunks);
                Count("changed");

                if (chunks.Count == 0)
                    Count("empty");
            }

            Stage("deduplicate");
            Stage("compare");

            foreach (var document in repositoriesToUpsert)
            {
                await _provider.UpsertDocumentsAsync([document], cancellationToken);
            }

            if (revisionsToUpsert.Count > 0)
                await _provider.UpsertRevisionsAsync(
                    revisionsToUpsert.Select(x => x with { State = KnowledgeIndexState.Upserting }),
                    cancellationToken);

            if (chunksToUpsert.Count > 0)
                await _provider.UpsertChunksAsync(chunksToUpsert, cancellationToken);

            if (_embeddingProvider.IsAvailable && chunksToUpsert.Count > 0)
            {
                await MoveAsync(KnowledgeIndexState.Embedding);

                var vectors = new List<(string ChunkId, float[] Vector)>();
                foreach (var chunk in chunksToUpsert)
                {
                    if (await _provider.EmbeddingExistsAsync(
                            _embeddingProvider.Generation.Id,
                            chunk.ChunkId,
                            cancellationToken))
                    {
                        Count("embedding_reused");
                        continue;
                    }

                    var vector = await _embeddingProvider.EmbedAsync(chunk.Text, cancellationToken);
                    if (vector is null || vector.Length == 0)
                    {
                        degraded.Add("semantic_degraded");
                        continue;
                    }

                    vectors.Add((chunk.ChunkId, vector));
                    Count("embedded");
                }

                if (vectors.Count > 0)
                {
                    await _provider.UpsertEmbeddingsAsync(
                        _embeddingProvider.Generation.Id,
                        vectors,
                        cancellationToken);
                }
            }
            else
            {
                degraded.Add("semantic_degraded");
            }

            Stage("index");
            await MoveAsync(KnowledgeIndexState.Upserting);

            foreach (var revision in revisionsToUpsert)
            {
                await _provider.SetRevisionStateAsync(
                    revision.RevisionId,
                    KnowledgeIndexState.Committed,
                    cancellationToken: cancellationToken);
            }

            if (!truncated)
            {
                var repoId = KnowledgeIdentity.RepositoryId(repositoryId);
                var existingDocuments = await _provider.ListDocumentsAsync(
                    repoId,
                    includeDeleted: false,
                    cancellationToken);
                foreach (var stale in existingDocuments.Where(x => !seenPaths.Contains(x.NormalizedPath)))
                {
                    await _provider.TombstoneAsync(
                        [stale.DocId],
                        "Path is absent from the indexed GitHub tree.",
                        cancellationToken);
                    Count("tombstoned");
                }
            }

            if (truncated)
            {
                state = KnowledgeIndexState.Partial;
                Count("partial");
            }
            else
            {
                await MoveAsync(KnowledgeIndexState.Committed);
            }

            var final = new KnowledgeRunRecord(
                runId,
                KnowledgeIdentity.RepositoryId(repositoryId),
                gitOid,
                state,
                started,
                DateTimeOffset.UtcNow,
                new Dictionary<string, long>(),
                counts,
                degraded);

            await _provider.RecordRunAsync(final, cancellationToken);
            return final;
        }
        catch (GitHubRateLimitException exception)
        {
            degraded.Add("stale");
            state = KnowledgeIndexState.FailedRetryable;
            Count("failure");
            var result = BuildFailure(runId, repository, gitOid, started, counts, degraded, state, exception.Message);
            await _provider.RecordRunAsync(result, cancellationToken);
            return result;
        }
        catch (GitHubAuthorizationException exception)
        {
            state = KnowledgeIndexState.FailedPermanent;
            Count("failure");
            var result = BuildFailure(runId, repository, gitOid, started, counts, degraded, state, exception.Message);
            await _provider.RecordRunAsync(result, cancellationToken);
            return result;
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidDataException)
        {
            state = KnowledgeIndexState.Partial;
            Count("failure");
            var result = BuildFailure(runId, repository, gitOid, started, counts, degraded, state, exception.Message);
            await _provider.RecordRunAsync(result, cancellationToken);
            return result;
        }
    }

    private static KnowledgeRunRecord BuildFailure(
        string runId,
        LiveRepository repository,
        string? gitOid,
        DateTimeOffset started,
        IReadOnlyDictionary<string, int> counts,
        IReadOnlyCollection<string> degraded,
        KnowledgeIndexState state,
        string reason) =>
        new(
            runId,
            repository.Id is long id ? KnowledgeIdentity.RepositoryId(id) : null,
            gitOid,
            state,
            started,
            DateTimeOffset.UtcNow,
            new Dictionary<string, long>(),
            counts,
            degraded,
            reason);

    private static bool IsEligible(string path, long declaredSize)
    {
        if (declaredSize > MaxTextBytes)
            return false;

        var parts = KnowledgeIdentity.NormalizePath(path)
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Any(ExcludedDirectories.Contains))
            return false;

        var extension = Path.GetExtension(path);
        if (string.IsNullOrWhiteSpace(extension))
            return true;

        return !new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".gif", ".webp", ".ico", ".pdf", ".zip", ".gz",
            ".7z", ".rar", ".mp3", ".mp4", ".mov", ".avi", ".exe", ".dll", ".so", ".bin",
            ".woff", ".woff2", ".ttf", ".otf", ".pyc", ".class"
        }.Contains(extension);
    }

    private static bool LooksBinary(byte[] bytes)
    {
        var limit = Math.Min(bytes.Length, 8192);
        for (var i = 0; i < limit; i++)
            if (bytes[i] == 0)
                return true;

        return false;
    }

    private static string DetectLanguage(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".cs" => "csharp",
            ".fs" => "fsharp",
            ".py" => "python",
            ".js" or ".jsx" => "javascript",
            ".ts" or ".tsx" => "typescript",
            ".java" => "java",
            ".go" => "go",
            ".rs" => "rust",
            ".cpp" or ".cc" or ".h" or ".hpp" => "cpp",
            ".c" => "c",
            ".rb" => "ruby",
            ".php" => "php",
            ".swift" => "swift",
            ".kt" => "kotlin",
            ".md" or ".markdown" => "markdown",
            ".json" => "json",
            ".yaml" or ".yml" => "yaml",
            ".toml" => "toml",
            ".ini" => "ini",
            ".sql" => "sql",
            ".html" or ".htm" => "html",
            ".css" => "css",
            ".scss" => "scss",
            ".xml" => "xml",
            ".sh" or ".bash" => "shell",
            ".ps1" => "powershell",
            ".ipynb" => "json",
            _ => "text"
        };
}
