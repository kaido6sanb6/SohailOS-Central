using System.Text.Json;
using SohailOS.Core;

namespace SohailOS.Gateway;

public static class EcosystemKnowledgeTools
{
    public static readonly IReadOnlySet<string> AllowedToolNames =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "ecosystem.search",
            "ecosystem.get_document",
            "ecosystem.get_source"
        };

    public static void Register(
        ToolRegistry registry,
        SohailOS.Ecosystem.KnowledgeRetrievalService retrieval)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(retrieval);

        registry.Register(new SearchTool(retrieval));
        registry.Register(new GetDocumentTool(retrieval));
        registry.Register(new GetSourceTool(retrieval));
    }

    private abstract class BaseTool(
        string name,
        string description,
        SohailOS.Ecosystem.KnowledgeRetrievalService retrieval) : ITool
    {
        protected SohailOS.Ecosystem.KnowledgeRetrievalService Retrieval { get; } = retrieval;

        public ToolDefinition Definition { get; } =
            new(name, description, ToolPermission.ReadOnly);

        public abstract Task<ToolResult> ExecuteAsync(
            IReadOnlyDictionary<string, object?> arguments,
            CancellationToken cancellationToken = default);

        protected static string? Arg(
            IReadOnlyDictionary<string, object?> arguments,
            string name)
        {
            if (!arguments.TryGetValue(name, out var value) || value is null)
                return null;

            if (value is JsonElement element)
            {
                return element.ValueKind switch
                {
                    JsonValueKind.String => element.GetString(),
                    JsonValueKind.Number => element.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => null
                };
            }

            return Convert.ToString(value);
        }

        protected static int IntArg(
            IReadOnlyDictionary<string, object?> arguments,
            string name,
            int fallback)
            => int.TryParse(Arg(arguments, name), out var value) ? value : fallback;

        protected static bool BoolArg(
            IReadOnlyDictionary<string, object?> arguments,
            string name,
            bool fallback = false)
            => bool.TryParse(Arg(arguments, name), out var value) ? value : fallback;

        protected static IReadOnlySet<string>? CsvSet(
            IReadOnlyDictionary<string, object?> arguments,
            string name)
        {
            var raw = Arg(arguments, name);
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.Ordinal);
        }

        protected static IReadOnlySet<SohailOS.Ecosystem.KnowledgeTrustTier>? TrustSet(
            IReadOnlyDictionary<string, object?> arguments)
        {
            var raw = Arg(arguments, "trust");
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            var result = new HashSet<SohailOS.Ecosystem.KnowledgeTrustTier>();
            foreach (var item in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!Enum.TryParse<SohailOS.Ecosystem.KnowledgeTrustTier>(item, true, out var tier))
                    continue;
                result.Add(tier);
            }

            return result.Count == 0 ? null : result;
        }

        protected static ToolResult JsonSuccess(string toolName, object value) =>
            new(toolName, true, JsonSerializer.Serialize(value));

        protected static ToolResult JsonFailure(string toolName, string message) =>
            new(toolName, false, message);
    }

    private sealed class SearchTool(
        SohailOS.Ecosystem.KnowledgeRetrievalService retrieval)
        : BaseTool(
            "ecosystem.search",
            "Search the authorized GitHub ecosystem knowledge index using lexical and semantic retrieval.",
            retrieval)
    {
        public async Task<ToolResult> ExecuteAsync(
            IReadOnlyDictionary<string, object?> arguments,
            CancellationToken cancellationToken = default)
        {
            var query = Arg(arguments, "query");
            if (string.IsNullOrWhiteSpace(query))
                return JsonFailure(Definition.Name, "query is required.");

            var request = new SohailOS.Ecosystem.RetrievalRequest(
                Query: query,
                Limit: Math.Clamp(IntArg(arguments, "limit", 10), 1, 100),
                Mode: Arg(arguments, "mode") ?? "current",
                AsOf: TryDate(Arg(arguments, "as_of")),
                AtCommit: Arg(arguments, "at_commit"),
                EmbeddingGenerationId: Arg(arguments, "embedding_generation_id"),
                IncludeUnverified: BoolArg(arguments, "include_unverified"),
                TrustTiers: TrustSet(arguments),
                RepositoryIds: CsvSet(arguments, "repo_id"),
                SourceTypes: CsvSet(arguments, "source_type"));

            var result = await Retrieval.SearchAsync(request, cancellationToken);
            return JsonSuccess(
                Definition.Name,
                new
                {
                    results = result.Results,
                    mode = result.Mode,
                    fusion_version = result.FusionVersion,
                    embedding_gen_id = result.EmbeddingGenerationId,
                    degraded_flags = result.DegradedFlags,
                    data_not_instructions = true,
                    approximate = result.Approximate,
                    resolution = result.Resolution
                });
        }

        private static DateTimeOffset? TryDate(string? value) =>
            DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
    }

    private sealed class GetDocumentTool(
        SohailOS.Ecosystem.KnowledgeRetrievalService retrieval)
        : BaseTool(
            "ecosystem.get_document",
            "Retrieve one indexed ecosystem document in current or explicit historical mode.",
            retrieval)
    {
        public async Task<ToolResult> ExecuteAsync(
            IReadOnlyDictionary<string, object?> arguments,
            CancellationToken cancellationToken = default)
        {
            var docId = Arg(arguments, "doc_id");
            if (string.IsNullOrWhiteSpace(docId))
                return JsonFailure(Definition.Name, "doc_id is required.");

            var mode = Arg(arguments, "mode") ?? "current";
            var asOf = TryDate(Arg(arguments, "as_of"));
            var atCommit = Arg(arguments, "at_commit");

            try
            {
                var result = await Retrieval.GetDocumentAsync(
                    docId,
                    mode,
                    asOf,
                    atCommit,
                    cancellationToken);

                if (result is null)
                {
                    return JsonSuccess(
                        Definition.Name,
                        new
                        {
                            results = Array.Empty<object>(),
                            mode,
                            fusion_version = SohailOS.Ecosystem.HybridRanker.FusionVersion,
                            embedding_gen_id = (string?)null,
                            degraded_flags = new[] { "historical_unavailable" },
                            data_not_instructions = true
                        });
                }

                var sources = new Dictionary<string, SohailOS.Ecosystem.KnowledgeSourcePointer?>(
                    StringComparer.Ordinal);

                foreach (var chunk in result.Chunks)
                {
                    if (!sources.ContainsKey(chunk.RevisionId))
                        sources[chunk.RevisionId] =
                            await Retrieval.GetSourceAsync(chunk.RevisionId, cancellationToken);
                }

                var results = result.Chunks
                    .Select(chunk =>
                    {
                        var source = sources[chunk.RevisionId];
                        if (source is null)
                            return null;

                        var provenance = new
                        {
                            repo_id = source.RepoId,
                            repo_full_name = source.RepoFullName,
                            path = source.Path,
                            git_oid = source.GitOid,
                            blob_sha = source.BlobSha,
                            content_hash = chunk.ContentHash,
                            chunk_id = chunk.ChunkId,
                            ordinal = chunk.Ordinal,
                            byte_start = chunk.ByteStart,
                            byte_end = chunk.ByteEnd,
                            indexed_at = source.IndexedAt,
                            embedding_gen_id = (string?)null,
                            trust_tier = source.TrustTier,
                            degraded_flags = result.DegradedFlags,
                            permalink = source.Permalink
                        };

                        return new
                        {
                            provenance,
                            text = chunk.Text,
                            language = chunk.Language
                        };
                    })
                    .Where(x => x is not null)
                    .ToArray();

                return JsonSuccess(
                    Definition.Name,
                    new
                    {
                        results,
                        mode = result.Mode,
                        fusion_version = SohailOS.Ecosystem.HybridRanker.FusionVersion,
                        embedding_gen_id = (string?)null,
                        degraded_flags = result.DegradedFlags,
                        data_not_instructions = true
                    });
            }
            catch (ArgumentException exception)
            {
                return JsonFailure(Definition.Name, exception.Message);
            }
        }

        private static DateTimeOffset? TryDate(string? value) =>
            DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
    }

    private sealed class GetSourceTool(
        SohailOS.Ecosystem.KnowledgeRetrievalService retrieval)
        : BaseTool(
            "ecosystem.get_source",
            "Retrieve the immutable provenance pointer for a known indexed source revision.",
            retrieval)
    {
        public async Task<ToolResult> ExecuteAsync(
            IReadOnlyDictionary<string, object?> arguments,
            CancellationToken cancellationToken = default)
        {
            var revisionId = Arg(arguments, "revision_id");
            if (string.IsNullOrWhiteSpace(revisionId))
                return JsonFailure(Definition.Name, "revision_id is required.");

            var source = await Retrieval.GetSourceAsync(revisionId, cancellationToken);
            if (source is null)
            {
                return JsonFailure(
                    Definition.Name,
                    $"Source revision not found: {revisionId}");
            }

            return JsonSuccess(
                Definition.Name,
                new
                {
                    results = new[] { source },
                    mode = "at_revision",
                    fusion_version = SohailOS.Ecosystem.HybridRanker.FusionVersion,
                    embedding_gen_id = (string?)null,
                    degraded_flags = Array.Empty<string>(),
                    data_not_instructions = true
                });
        }
    }
}
