using System.Collections.ObjectModel;

namespace SohailOS.Ecosystem;

public enum KnowledgeTrustTier
{
    Authoritative,
    Verified,
    ReferenceOnly,
    Unverified,
    Tombstoned
}

public enum KnowledgeIndexState
{
    Discovered,
    Authorized,
    Queued,
    Fetching,
    Parsing,
    Chunking,
    Embedding,
    Upserting,
    Committed,
    FailedRetryable,
    FailedPermanent,
    Superseded,
    Partial,
    Tombstoned
}

public sealed record RepositoryIdentity(
    string RepoId,
    long GithubNumericId,
    string FullName,
    string DefaultBranch,
    bool IsPrivate,
    KnowledgeTrustTier TrustTier,
    DateTimeOffset? AuthorizedAt,
    DateTimeOffset? LastSeenAt);

public sealed record SourceRevision(
    string RevisionId,
    string RepoId,
    string GitOid,
    string Path,
    string BlobSha,
    string ContentHash,
    long SizeBytes,
    DateTimeOffset FetchedAt,
    KnowledgeIndexState State = KnowledgeIndexState.Discovered,
    string? FailureReason = null);

public sealed record DocumentRecord(
    string DocId,
    string RepoId,
    string NormalizedPath,
    string? CurrentRevisionId,
    string Language,
    bool IsBinary,
    bool Deleted,
    DateTimeOffset? DeletedAt,
    string SourceType = "text");

public sealed record ChunkRecord(
    string ChunkId,
    string DocId,
    string RevisionId,
    int Ordinal,
    long ByteStart,
    long ByteEnd,
    string ContentHash,
    string Text,
    string Language);

public sealed record EmbeddingGeneration(
    string Id,
    string Provider,
    string Model,
    int Dimensions,
    string NormalizationVersion);

public sealed record KnowledgeProviderCapabilities(
    bool Vector,
    bool Lexical,
    bool IterativeScan,
    int MaxDimension);

public sealed record KnowledgeHealthStatus(
    bool Healthy,
    string Provider,
    string? Message = null);

public sealed record KnowledgeWriteResult(
    bool Success,
    int Affected,
    string? Message = null);

public sealed record KnowledgeRunRecord(
    string RunId,
    string? RepoId,
    string? GitOid,
    KnowledgeIndexState State,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    IReadOnlyDictionary<string, long> PhaseTimingsMs,
    IReadOnlyDictionary<string, int> Counts,
    IReadOnlyCollection<string> DegradedFlags,
    string? FailureReason = null);

public sealed record KnowledgeFilters(
    IReadOnlySet<KnowledgeTrustTier>? TrustTiers = null,
    IReadOnlySet<string>? RepositoryIds = null,
    IReadOnlySet<string>? SourceTypes = null,
    bool IncludeHistorical = false);

public sealed record ProvenanceEnvelope(
    string RepoId,
    string RepoFullName,
    string Path,
    string GitOid,
    string BlobSha,
    string ContentHash,
    string ChunkId,
    int Ordinal,
    long ByteStart,
    long ByteEnd,
    DateTimeOffset IndexedAt,
    string? EmbeddingGenId,
    KnowledgeTrustTier TrustTier,
    IReadOnlyCollection<string> DegradedFlags,
    string Permalink);

public sealed record KnowledgeHit(
    ProvenanceEnvelope Provenance,
    string Text,
    double Score,
    string RetrievalSource,
    string? Language = null);

public sealed record RetrievalRequest(
    string Query,
    int Limit = 10,
    string Mode = "current",
    DateTimeOffset? AsOf = null,
    string? AtCommit = null,
    string? EmbeddingGenerationId = null,
    bool IncludeUnverified = false,
    IReadOnlySet<KnowledgeTrustTier>? TrustTiers = null,
    IReadOnlySet<string>? RepositoryIds = null,
    IReadOnlySet<string>? SourceTypes = null);

public sealed record RetrievalResponse(
    IReadOnlyList<KnowledgeHit> Results,
    string Mode,
    string FusionVersion,
    string? EmbeddingGenerationId,
    IReadOnlyCollection<string> DegradedFlags,
    bool DataNotInstructions = true,
    bool Approximate = false,
    string? Resolution = null);

public sealed record KnowledgeDocumentResult(
    DocumentRecord Document,
    IReadOnlyList<ChunkRecord> Chunks,
    string Mode,
    IReadOnlyCollection<string> DegradedFlags);

public sealed record KnowledgeSourcePointer(
    string RepoId,
    string RepoFullName,
    string Path,
    string GitOid,
    string BlobSha,
    string Permalink,
    KnowledgeTrustTier TrustTier);

public interface IEmbeddingProvider
{
    bool IsAvailable { get; }
    EmbeddingGeneration Generation { get; }
    Task<float[]?> EmbedAsync(string text, CancellationToken cancellationToken = default);
}

public interface DataPlaneProvider
{
    Task<KnowledgeWriteResult> UpsertRepositoriesAsync(IEnumerable<RepositoryIdentity> repositories, CancellationToken cancellationToken = default);
    Task<KnowledgeHealthStatus> HealthCheckAsync(CancellationToken cancellationToken = default);
    Task<KnowledgeProviderCapabilities> CapabilitiesAsync(CancellationToken cancellationToken = default);
    Task<KnowledgeWriteResult> MigrateAsync(string targetVersion, CancellationToken cancellationToken = default);
    Task<KnowledgeWriteResult> UpsertRevisionsAsync(IEnumerable<SourceRevision> revisions, CancellationToken cancellationToken = default);
    Task<KnowledgeWriteResult> UpsertDocumentsAsync(IEnumerable<DocumentRecord> documents, CancellationToken cancellationToken = default);
    Task<KnowledgeWriteResult> UpsertChunksAsync(IEnumerable<ChunkRecord> chunks, CancellationToken cancellationToken = default);
    Task<KnowledgeWriteResult> UpsertEmbeddingsAsync(string generationId, IEnumerable<(string ChunkId, float[] Vector)> items, CancellationToken cancellationToken = default);
    Task<KnowledgeWriteResult> TombstoneAsync(IEnumerable<string> ids, string reason, CancellationToken cancellationToken = default);
    Task<KnowledgeWriteResult> RecordRunAsync(KnowledgeRunRecord run, CancellationToken cancellationToken = default);
    Task<KnowledgeWriteResult> SetRevisionStateAsync(string revisionId, KnowledgeIndexState state, string? reason = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KnowledgeHit>> LexicalSearchAsync(string query, KnowledgeFilters filters, int k, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KnowledgeHit>> VectorSearchAsync(float[] vector, string generationId, KnowledgeFilters filters, int k, CancellationToken cancellationToken = default);
    Task<KnowledgeDocumentResult?> GetDocumentAsync(string docId, string? asOf, CancellationToken cancellationToken = default);
    Task<KnowledgeDocumentResult?> GetDocumentAtCommitAsync(string docId, string gitOid, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentRecord>> ListDocumentsAsync(string repoId, bool includeDeleted, CancellationToken cancellationToken = default);
    Task<KnowledgeSourcePointer?> GetSourceAsync(string revisionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmbeddingGeneration>> ListGenerationsAsync(CancellationToken cancellationToken = default);
}

public sealed class NullEmbeddingProvider : IEmbeddingProvider
{
    public bool IsAvailable => false;
    public EmbeddingGeneration Generation { get; } =
        new("emb:null:none:0:none", "none", "none", 0, "none");

    public Task<float[]?> EmbedAsync(string text, CancellationToken cancellationToken = default)
        => Task.FromResult<float[]?>(null);
}
