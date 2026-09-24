using System.Text.RegularExpressions;

namespace SohailOS.Ecosystem;

public sealed record ScholarlyAuthor(string Name, string? Id = null, string? Affiliation = null);

public sealed record OpenAccessLocation(
    string Url,
    string? Version = null,
    string? License = null,
    string? Repository = null);

public sealed record ScholarlyRecord(
    string Id,
    string Title,
    string Url,
    string? Doi,
    string Source,
    double SourceScore,
    string? Abstract = null,
    IReadOnlyList<ScholarlyAuthor>? Authors = null,
    IReadOnlyCollection<string>? Subjects = null,
    DateTimeOffset? PublishedAt = null,
    IReadOnlyList<OpenAccessLocation>? OpenAccessLocations = null,
    string AccessMode = "metadata",
    string License = "unknown");

public sealed record ProviderSearchResult(
    string Provider,
    IReadOnlyList<ScholarlyRecord> Records,
    bool Degraded,
    string? DegradedReason);

public interface IScholarlyProvider
{
    string Name { get; }
    bool IsConfigured { get; }
    Task<ProviderSearchResult> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default);
}

public interface IOpenAccessResolver
{
    string Name { get; }
    bool IsConfigured { get; }

    Task<ScholarlyRecord?> EnrichAsync(
        ScholarlyRecord record,
        CancellationToken cancellationToken = default);
}

public interface ICitationGraphProvider
{
    string Name { get; }

    Task<IReadOnlyList<CitationEdge>> GetCitationEdgesAsync(
        string doi,
        int limit,
        CancellationToken cancellationToken = default);
}

public sealed record CitationEdge(
    string CitingDoi,
    string CitedDoi,
    string Source);

public sealed record ResearchAuditEvent(
    DateTimeOffset At,
    string Provider,
    string Operation,
    int RecordCount,
    bool Degraded,
    string? Detail = null);

public sealed record ResearchIntelligencePlanStage(
    string Name,
    string Purpose,
    bool Required);

public sealed record ResearchIntelligencePlan(
    IReadOnlyList<ResearchIntelligencePlanStage> Stages,
    bool RequiresCitations,
    bool RequiresProvenance);

public sealed record ResearchIntelligenceResult(
    string Query,
    ResearchDiscipline Discipline,
    IReadOnlyList<ScholarlyRecord> Records,
    ScholarlyGraph Graph,
    ResearchIntelligencePlan Plan,
    IReadOnlyList<ResearchAuditEvent> AuditTrail,
    IReadOnlyCollection<string> DegradedFlags);

public enum ScholarlyNodeType
{
    Work,
    Author,
    Institution,
    Topic
}

public enum ScholarlyEdgeType
{
    Cites,
    AuthoredBy,
    AffiliatedWith,
    HasTopic
}

public sealed record ScholarlyGraphNode(
    string Id,
    ScholarlyNodeType Type,
    string Label);

public sealed record ScholarlyGraphEdge(
    string SourceId,
    string TargetId,
    ScholarlyEdgeType Type,
    string Provenance);

public sealed record ScholarlyGraph(
    IReadOnlyList<ScholarlyGraphNode> Nodes,
    IReadOnlyList<ScholarlyGraphEdge> Edges);

public static class ScholarlyCanonicalizer
{
    public static IReadOnlyList<ScholarlyRecord> Deduplicate(
        IEnumerable<ScholarlyRecord> records)
    {
        var map = new Dictionary<string, ScholarlyRecord>(StringComparer.OrdinalIgnoreCase);

        foreach (var record in records)
        {
            var key = CanonicalKey(record);
            if (!map.TryGetValue(key, out var existing))
            {
                map[key] = record;
                continue;
            }

            map[key] = Merge(existing, record);
        }

        return map.Values
            .OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Doi, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string NormalizeDoi(string? doi) =>
        string.IsNullOrWhiteSpace(doi)
            ? string.Empty
            : doi.Trim()
                .Replace("https://doi.org/", "", StringComparison.OrdinalIgnoreCase)
                .Replace("http://doi.org/", "", StringComparison.OrdinalIgnoreCase)
                .Trim()
                .TrimEnd('.')
                .ToLowerInvariant();

    public static string NormalizeTitle(string title) =>
        Regex.Replace(title.Trim().ToLowerInvariant(), @"[^\p{L}\p{N}]+", " ").Trim();

    private static string CanonicalKey(ScholarlyRecord record)
    {
        var doi = NormalizeDoi(record.Doi);
        return string.IsNullOrEmpty(doi)
            ? "title:" + NormalizeTitle(record.Title)
            : "doi:" + doi;
    }

    private static ScholarlyRecord Merge(
        ScholarlyRecord first,
        ScholarlyRecord second)
    {
        var primary = first.SourceScore >= second.SourceScore ? first : second;
        var secondary = ReferenceEquals(primary, first) ? second : first;

        return primary with
        {
            Doi = primary.Doi ?? secondary.Doi,
            Abstract = primary.Abstract ?? secondary.Abstract,
            Authors = primary.Authors is { Count: > 0 } ? primary.Authors : secondary.Authors,
            Subjects = primary.Subjects is { Count: > 0 } ? primary.Subjects : secondary.Subjects,
            PublishedAt = primary.PublishedAt ?? secondary.PublishedAt,
            OpenAccessLocations = (primary.OpenAccessLocations ?? [])
                .Concat(secondary.OpenAccessLocations ?? [])
                .GroupBy(x => x.Url, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .ToArray(),
            AccessMode = primary.AccessMode == "metadata" ? secondary.AccessMode : primary.AccessMode,
            License = primary.License == "unknown" ? secondary.License : primary.License
        };
    }
}

public static class ScholarlyGraphBuilder
{
    public static ScholarlyGraph Build(
        IEnumerable<ScholarlyRecord> records,
        IEnumerable<CitationEdge> citationEdges)
    {
        var nodes = new Dictionary<string, ScholarlyGraphNode>(StringComparer.Ordinal);
        var edges = new HashSet<(string Source, string Target, ScholarlyEdgeType Type)>();

        foreach (var record in records)
        {
            var workId = WorkId(record);
            nodes[workId] = new ScholarlyGraphNode(workId, ScholarlyNodeType.Work, record.Title);

            foreach (var author in record.Authors ?? [])
            {
                var authorId = "author:" + ScholarlyCanonicalizer.NormalizeTitle(author.Name);
                nodes.TryAdd(authorId, new ScholarlyGraphNode(authorId, ScholarlyNodeType.Author, author.Name));
                edges.Add((workId, authorId, ScholarlyEdgeType.AuthoredBy));
            }

            foreach (var subject in record.Subjects ?? [])
            {
                if (string.IsNullOrWhiteSpace(subject)) continue;
                var topicId = "topic:" + ScholarlyCanonicalizer.NormalizeTitle(subject);
                nodes.TryAdd(topicId, new ScholarlyGraphNode(topicId, ScholarlyNodeType.Topic, subject));
                edges.Add((workId, topicId, ScholarlyEdgeType.HasTopic));
            }
        }

        foreach (var edge in citationEdges)
        {
            var citingId = "doi:" + ScholarlyCanonicalizer.NormalizeDoi(edge.CitingDoi);
            var citedId = "doi:" + ScholarlyCanonicalizer.NormalizeDoi(edge.CitedDoi);

            nodes.TryAdd(citingId, new ScholarlyGraphNode(citingId, ScholarlyNodeType.Work, edge.CitingDoi));
            nodes.TryAdd(citedId, new ScholarlyGraphNode(citedId, ScholarlyNodeType.Work, edge.CitedDoi));
            edges.Add((citingId, citedId, ScholarlyEdgeType.Cites));
        }

        var finalEdges = edges
            .Select(x => new ScholarlyGraphEdge(x.Source, x.Target, x.Type, x.Type == ScholarlyEdgeType.Cites ? "OpenCitations" : "normalized-record"))
            .OrderBy(x => x.Type)
            .ThenBy(x => x.SourceId, StringComparer.Ordinal)
            .ThenBy(x => x.TargetId, StringComparer.Ordinal)
            .ToArray();

        return new ScholarlyGraph(
            nodes.Values.OrderBy(x => x.Type).ThenBy(x => x.Id, StringComparer.Ordinal).ToArray(),
            finalEdges);
    }

    private static string WorkId(ScholarlyRecord record)
    {
        var doi = ScholarlyCanonicalizer.NormalizeDoi(record.Doi);
        return string.IsNullOrEmpty(doi) ? "work:" + record.Id : "doi:" + doi;
    }
}
