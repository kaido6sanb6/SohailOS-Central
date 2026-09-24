using System.Net.Http;
using System.Text.Json;

namespace SohailOS.Ecosystem;

public sealed class ResearchIntelligenceEngine
{
    private readonly IReadOnlyList<IScholarlyProvider> _providers;
    private readonly IOpenAccessResolver? _openAccessResolver;
    private readonly ICitationGraphProvider? _citationGraphProvider;

    public ResearchIntelligenceEngine(
        IEnumerable<IScholarlyProvider> providers,
        IOpenAccessResolver? openAccessResolver = null,
        ICitationGraphProvider? citationGraphProvider = null)
    {
        _providers = providers
            .Where(x => x.IsConfigured)
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ToArray();
        _openAccessResolver = openAccessResolver;
        _citationGraphProvider = citationGraphProvider;
    }

    public async Task<ResearchIntelligenceResult> SearchAsync(
        ResearchSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var discipline = request.Discipline == ResearchDiscipline.General
            ? ResearchQueryClassifier.Classify(request.Query)
            : request.Discipline;

        var plan = SystematicResearchPlan.Create(request.Query, discipline);
        var audit = new List<ResearchAuditEvent>();
        var degraded = new HashSet<string>(StringComparer.Ordinal);
        var all = new List<ScholarlyRecord>();

        var tasks = _providers.Select(provider =>
            SearchProviderAsync(provider, request.Query, request.Limit * 2, all, audit, degraded, cancellationToken));

        await Task.WhenAll(tasks);

        var unique = ScholarlyCanonicalizer.Deduplicate(all);
        var enriched = await EnrichOpenAccessAsync(unique, audit, degraded, cancellationToken);

        var ranked = Rank(enriched, request.Query)
            .Where(x => !request.OpenAccessOnly || x.AccessMode is "oa" or "fulltext")
            .Take(Math.Max(1, request.Limit))
            .ToArray();

        var citationEdges = await ExpandCitationsAsync(ranked, request.Limit, audit, degraded, cancellationToken);
        var graph = ScholarlyGraphBuilder.Build(ranked, citationEdges);

        if (ranked.Length == 0)
            degraded.Add("no_admissible_results");

        return new ResearchIntelligenceResult(
            request.Query,
            discipline,
            ranked,
            graph,
            plan,
            audit.OrderBy(x => x.At).ThenBy(x => x.Provider, StringComparer.Ordinal).ToArray(),
            degraded);
    }

    private static async Task SearchProviderAsync(
        IScholarlyProvider provider,
        string query,
        int limit,
        List<ScholarlyRecord> all,
        List<ResearchAuditEvent> audit,
        HashSet<string> degraded,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await provider.SearchAsync(query, Math.Clamp(limit, 1, 100), cancellationToken);
            lock (all) all.AddRange(result.Records);
            lock (audit)
            {
                audit.Add(new ResearchAuditEvent(
                    DateTimeOffset.UtcNow,
                    provider.Name,
                    "search",
                    result.Records.Count,
                    result.Degraded,
                    result.DegradedReason));
            }
            if (result.Degraded && !string.IsNullOrWhiteSpace(result.DegradedReason))
                lock (degraded) degraded.Add($"{provider.Name}:{result.DegradedReason}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            lock (audit) audit.Add(new ResearchAuditEvent(
                DateTimeOffset.UtcNow,
                provider.Name,
                "search",
                0,
                true,
                "timeout"));
            lock (degraded) degraded.Add($"{provider.Name}:timeout");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            lock (audit) audit.Add(new ResearchAuditEvent(
                DateTimeOffset.UtcNow,
                provider.Name,
                "search",
                0,
                true,
                ex.GetType().Name));
            lock (degraded) degraded.Add($"{provider.Name}:unavailable");
        }
    }

    private async Task<IReadOnlyList<ScholarlyRecord>> EnrichOpenAccessAsync(
        IReadOnlyList<ScholarlyRecord> records,
        List<ResearchAuditEvent> audit,
        HashSet<string> degraded,
        CancellationToken cancellationToken)
    {
        if (_openAccessResolver is null || !_openAccessResolver.IsConfigured)
        {
            if (_openAccessResolver is not null)
                degraded.Add($"{_openAccessResolver.Name}:not_configured");
            return records;
        }

        var enriched = new List<ScholarlyRecord>();
        foreach (var record in records)
        {
            if (string.IsNullOrWhiteSpace(record.Doi))
            {
                enriched.Add(record);
                continue;
            }

            try
            {
                enriched.Add(await _openAccessResolver.EnrichAsync(record, cancellationToken) ?? record);
            }
            catch (HttpRequestException ex)
            {
                enriched.Add(record);
                audit.Add(new ResearchAuditEvent(
                    DateTimeOffset.UtcNow,
                    _openAccessResolver.Name,
                    "oa_enrich",
                    0,
                    true,
                    ex.GetType().Name));
                degraded.Add($"{_openAccessResolver.Name}:unavailable");
            }
        }

        audit.Add(new ResearchAuditEvent(
            DateTimeOffset.UtcNow,
            _openAccessResolver.Name,
            "oa_enrich",
            enriched.Count(x => x.AccessMode == "oa"),
            false,
            null));

        return enriched;
    }

    private async Task<IReadOnlyList<CitationEdge>> ExpandCitationsAsync(
        IReadOnlyList<ScholarlyRecord> records,
        int limit,
        List<ResearchAuditEvent> audit,
        HashSet<string> degraded,
        CancellationToken cancellationToken)
    {
        if (_citationGraphProvider is null)
            return [];

        var edges = new List<CitationEdge>();
        foreach (var record in records.Where(x => !string.IsNullOrWhiteSpace(x.Doi)).Take(5))
        {
            try
            {
                var found = await _citationGraphProvider.GetCitationEdgesAsync(record.Doi!, Math.Min(limit, 25), cancellationToken);
                edges.AddRange(found);
            }
            catch (HttpRequestException ex)
            {
                audit.Add(new ResearchAuditEvent(
                    DateTimeOffset.UtcNow,
                    _citationGraphProvider.Name,
                    "citation_expand",
                    0,
                    true,
                    ex.GetType().Name));
                degraded.Add($"{_citationGraphProvider.Name}:unavailable");
            }
        }

        audit.Add(new ResearchAuditEvent(
            DateTimeOffset.UtcNow,
            _citationGraphProvider.Name,
            "citation_expand",
            edges.Count,
            false,
            null));

        return edges;
    }

    private static IEnumerable<ScholarlyRecord> Rank(
        IEnumerable<ScholarlyRecord> records,
        string query)
    {
        var terms = query.ToLowerInvariant()
            .Split([' ', '\t', '\r', '\n', ',', '.', ':', ';', '-', '_', '/', '(', ')', '[', ']'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Length > 2)
            .ToHashSet(StringComparer.Ordinal);

        return records
            .Select(record =>
            {
                var titleTerms = ScholarlyCanonicalizer.NormalizeTitle(record.Title)
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .ToHashSet(StringComparer.Ordinal);
                var overlap = terms.Count == 0 ? 0 : terms.Intersect(titleTerms).Count() / (double)terms.Count;
                var oa = record.AccessMode is "oa" or "fulltext" ? 1.0 : .55;
                var score = .55 * overlap + .30 * record.SourceScore + .15 * oa;
                return (record, score);
            })
            .OrderByDescending(x => x.score)
            .ThenBy(x => ScholarlyCanonicalizer.NormalizeTitle(x.record.Title), StringComparer.Ordinal)
            .ThenBy(x => x.record.Id, StringComparer.Ordinal)
            .Select(x => x.record);
    }
}

public static class SystematicResearchPlan
{
    public static ResearchIntelligencePlan Create(
        string query,
        ResearchDiscipline discipline)
    {
        var stages = new List<ResearchIntelligencePlanStage>
        {
            new("search", "federated discovery across scholarly indexes", true),
            new("canonicalize", "deduplicate by DOI or normalized title", true),
            new("enrich_oa", "resolve lawful open-access locations", false),
            new("expand_citations", "expand citation graph where DOI evidence exists", false),
            new("screen", "apply explicit inclusion and exclusion criteria", true),
            new("extract", "capture structured study-level evidence", true),
            new("audit", "preserve source, access, provenance and decision events", true)
        };

        if (discipline is ResearchDiscipline.Sociology or ResearchDiscipline.Psychology or ResearchDiscipline.Humanities)
            stages.Insert(1, new("repository_harvest", "include institutional and domain repositories", false));

        return new ResearchIntelligencePlan(
            stages,
            RequiresCitations: true,
            RequiresProvenance: true);
    }
}

public static class SystematicReviewPlanner
{
    public static SystematicReviewPlan Create(
        string query,
        ResearchDiscipline discipline)
    {
        var plan = SystematicResearchPlan.Create(query, discipline);
        return new SystematicReviewPlan(
            plan.Stages,
            RequiresPrismaStyleAuditTrail: true,
            EvidenceExtraction: StudyEvidenceExtractionSchema.Default);
    }
}

public sealed record SystematicReviewPlan(
    IReadOnlyList<ResearchIntelligencePlanStage> Stages,
    bool RequiresPrismaStyleAuditTrail,
    StudyEvidenceExtractionSchema EvidenceExtraction);

public sealed record StudyEvidenceExtractionSchema(
    IReadOnlyList<string> RequiredFields)
{
    public static StudyEvidenceExtractionSchema Default => new(
    [
        "study_id",
        "citation",
        "population",
        "sample_size",
        "design",
        "exposure_or_intervention",
        "outcome",
        "effect_measure",
        "effect_estimate",
        "lower_ci",
        "upper_ci",
        "adjustment_set",
        "risk_of_bias",
        "extraction_source",
        "extraction_confidence"
    ]);
}
