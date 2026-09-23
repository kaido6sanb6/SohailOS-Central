using System.Text.Json.Nodes;

namespace SohailOS.Ecosystem;

public sealed record KnowledgeGraphNode(
    string Id,
    string Type,
    string Name,
    string Trust = "unverified");

public sealed record KnowledgeGraphEdge(
    string From,
    string To,
    string Type);

public sealed record KnowledgeGraphDocument(
    string SchemaVersion,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<KnowledgeGraphNode> Nodes,
    IReadOnlyList<KnowledgeGraphEdge> Edges);

public static class KnowledgeGraphBuilder
{
    private const string Central = "kaido6sanb6/SohailOS-Central";

    private static readonly HashSet<string> LiveCorpus = new(StringComparer.OrdinalIgnoreCase)
    {
        "asgeirtj/system_prompts_leaks",
        "kaido6sanb6/system_prompts_leaks"
    };

    public static KnowledgeGraphDocument Build(
        IEnumerable<LiveRepository> repositories,
        JsonArray? inventory)
    {
        var nodes = new Dictionary<string, KnowledgeGraphNode>(StringComparer.OrdinalIgnoreCase);
        var edges = new HashSet<KnowledgeGraphEdge>();

        foreach (var repository in repositories)
        {
            if (repository.Id is not long id)
                continue;

            var trust = string.Equals(repository.FullName, Central, StringComparison.OrdinalIgnoreCase)
                ? "authoritative"
                : LiveCorpus.Contains(repository.FullName)
                    ? "untrusted-data"
                    : "unverified";

            nodes[$"repo:{id}"] = new(
                $"repo:{id}",
                "Repository",
                repository.FullName,
                trust);

            if (!string.Equals(repository.FullName, Central, StringComparison.OrdinalIgnoreCase))
            {
                var edgeType = LiveCorpus.Contains(repository.FullName)
                    ? "RESEARCH_CORPUS_FOR"
                    : "KNOWLEDGE_SOURCE_FOR";

                edges.Add(new(
                    repository.FullName,
                    Central,
                    edgeType));
            }
        }

        foreach (var corpus in LiveCorpus.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            var externalId = $"repo:external:{corpus}";
            if (!nodes.ContainsKey(externalId))
            {
                nodes[externalId] = new(
                    externalId,
                    "Repository",
                    corpus,
                    "untrusted-data");
            }

            if (!string.Equals(corpus, Central, StringComparison.OrdinalIgnoreCase))
            {
                edges.Add(new(corpus, Central, "RESEARCH_CORPUS_FOR"));
            }
        }

        foreach (var item in inventory?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>())
        {
            var fullName = item["full_name"]?.GetValue<string>();
            var upstream = item["upstream"]?.GetValue<string>();

            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(upstream))
                continue;

            edges.Add(new(fullName, upstream, "FORK_OF"));
        }

        return new(
            "1.0",
            DateTimeOffset.UtcNow,
            nodes.Values.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray(),
            edges.OrderBy(x => x.From, StringComparer.Ordinal)
                 .ThenBy(x => x.To, StringComparer.Ordinal)
                 .ThenBy(x => x.Type, StringComparer.Ordinal)
                 .ToArray());
    }
}
