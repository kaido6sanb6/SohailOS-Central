namespace SohailOS.Ecosystem;

public sealed class KnowledgeRetrievalService
{
    private readonly DataPlaneProvider _provider;
    private readonly IEmbeddingProvider _embeddingProvider;

    public KnowledgeRetrievalService(
        DataPlaneProvider provider,
        IEmbeddingProvider? embeddingProvider = null)
    {
        _provider = provider;
        _embeddingProvider = embeddingProvider ?? new NullEmbeddingProvider();
    }

    public async Task<RetrievalResponse> SearchAsync(
        RetrievalRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var mode = string.IsNullOrWhiteSpace(request.Mode)
            ? "current"
            : request.Mode.Trim().ToLowerInvariant();

        if (mode != "current")
        {
            return new RetrievalResponse(
                [],
                mode,
                HybridRanker.FusionVersion,
                request.EmbeddingGenerationId,
                ["historical_fallback"],
                true,
                false,
                "Historical retrieval is explicit through get_document.");
        }

        var limit = Math.Clamp(request.Limit, 1, 100);
        var tiers = ResolveTrustTiers(request);
        var filters = new KnowledgeFilters(
            tiers,
            request.RepositoryIds,
            request.SourceTypes,
            false);

        var capabilities = await _provider.CapabilitiesAsync(cancellationToken);
        var degraded = new HashSet<string>(StringComparer.Ordinal);

        var lexical = capabilities.Lexical
            ? await _provider.LexicalSearchAsync(request.Query, filters, limit, cancellationToken)
            : [];

        IReadOnlyList<KnowledgeHit> vector = [];
        string? generationId = request.EmbeddingGenerationId;

        if (_embeddingProvider.IsAvailable && capabilities.Vector)
        {
            generationId ??= _embeddingProvider.Generation.Id;
            var embedding = await _embeddingProvider.EmbedAsync(request.Query, cancellationToken);

            if (embedding is null || embedding.Length == 0)
                degraded.Add("semantic_degraded");
            else
                vector = await _provider.VectorSearchAsync(
                    embedding,
                    generationId,
                    filters,
                    limit,
                    cancellationToken);
        }
        else
        {
            degraded.Add("semantic_degraded");
            degraded.Add("lexical_only");
        }

        var results = HybridRanker.Fuse(lexical, vector, limit);

        return new RetrievalResponse(
            results,
            mode,
            HybridRanker.FusionVersion,
            generationId,
            degraded,
            true,
            !capabilities.IterativeScan,
            "current default-branch indexed revisions");
    }

    public async Task<KnowledgeDocumentResult?> GetDocumentAsync(
        string docId,
        string mode = "current",
        DateTimeOffset? asOf = null,
        string? atCommit = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(docId);

        mode = string.IsNullOrWhiteSpace(mode)
            ? "current"
            : mode.Trim().ToLowerInvariant();

        return mode switch
        {
            "current" => await _provider.GetDocumentAsync(docId, "current", cancellationToken),
            "as_of" when asOf.HasValue =>
                await _provider.GetDocumentAsync(
                    docId,
                    asOf.Value.ToUniversalTime().ToString("O"),
                    cancellationToken),
            "at_commit" when !string.IsNullOrWhiteSpace(atCommit) =>
                await _provider.GetDocumentAtCommitAsync(docId, atCommit, cancellationToken),
            "as_of" => throw new ArgumentException(
                "as_of mode requires a timestamp.",
                nameof(asOf)),
            "at_commit" => throw new ArgumentException(
                "at_commit mode requires a commit SHA.",
                nameof(atCommit)),
            _ => throw new ArgumentException($"Unsupported retrieval mode: {mode}", nameof(mode))
        };
    }

    public Task<KnowledgeSourcePointer?> GetSourceAsync(
        string revisionId,
        CancellationToken cancellationToken = default) =>
        _provider.GetSourceAsync(revisionId, cancellationToken);

    private static IReadOnlySet<KnowledgeTrustTier> ResolveTrustTiers(RetrievalRequest request)
    {
        if (request.TrustTiers is not null)
        {
            var explicitTiers = request.TrustTiers.ToHashSet();
            if (request.IncludeUnverified)
                explicitTiers.Add(KnowledgeTrustTier.Unverified);
            explicitTiers.Remove(KnowledgeTrustTier.Tombstoned);
            return explicitTiers;
        }

        var tiers = new HashSet<KnowledgeTrustTier>
        {
            KnowledgeTrustTier.Authoritative,
            KnowledgeTrustTier.Verified,
            KnowledgeTrustTier.ReferenceOnly
        };

        if (request.IncludeUnverified)
            tiers.Add(KnowledgeTrustTier.Unverified);

        return tiers;
    }
}
