using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SohailOS.Ecosystem;

public enum ResearchDiscipline
{
    General,
    Sociology,
    Psychology,
    Philosophy,
    History,
    Humanities,
    Interdisciplinary
}

public sealed record ResearchEvidence(
    string Id,
    string Title,
    string Url,
    string Source,
    string AccessMode,
    string License,
    double SourceScore,
    string? Abstract = null,
    string? Doi = null,
    IReadOnlyCollection<string>? Subjects = null,
    DateTimeOffset? PublishedAt = null);

public sealed record ResearchSearchRequest(
    string Query,
    ResearchDiscipline Discipline = ResearchDiscipline.General,
    int Limit = 12,
    bool OpenAccessOnly = false);

public sealed record ResearchSearchResult(
    string Query,
    ResearchDiscipline Discipline,
    IReadOnlyList<ResearchEvidence> Evidence,
    ResearchPlan Plan,
    IReadOnlyCollection<string> DegradedFlags);

public sealed record ResearchPlanStage(string Source, string Purpose, bool Required);
public sealed record ResearchPlan(IReadOnlyList<ResearchPlanStage> Stages, bool RequiresCitations);

public static class ResearchQueryClassifier
{
    private static readonly (ResearchDiscipline Discipline, string[] Terms)[] Rules =
    [
        (ResearchDiscipline.Psychology, ["depression","anxiety","rumination","self-esteem","trauma","cognition","behavior","mental health","social anxiety"]),
        (ResearchDiscipline.Sociology, ["socialization","self-silencing","social class","inequality","gender","institution","family","community","social network","stratification"]),
        (ResearchDiscipline.Philosophy, ["ontology","epistemology","ethics","phenomenology","dialectic","marxism","existentialism"]),
        (ResearchDiscipline.History, ["historical","history","empire","revolution","war","archive"]),
        (ResearchDiscipline.Humanities, ["literature","cinema","film","culture","narrative","religion","language"])
    ];

    public static ResearchDiscipline Classify(string query)
    {
        var q = query.Trim().ToLowerInvariant();
        var scores = Rules
            .Select(r => (r.Discipline, Score: r.Terms.Count(t => q.Contains(t, StringComparison.Ordinal))))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ToArray();
        return scores.Length == 0 ? ResearchDiscipline.General : scores[0].Discipline;
    }
}

public sealed class ResearchEvidencePolicy
{
    private static readonly HashSet<string> BlockedSources =
        new(StringComparer.OrdinalIgnoreCase) { "zotero-scihub", "scihub", "pirate-bypass" };

    public bool IsAdmissible(ResearchEvidence evidence)
    {
        if (string.IsNullOrWhiteSpace(evidence.Title) || string.IsNullOrWhiteSpace(evidence.Url))
            return false;
        if (BlockedSources.Contains(evidence.Source) ||
            evidence.AccessMode.Contains("bypass", StringComparison.OrdinalIgnoreCase))
            return false;
        return !string.Equals(evidence.License, "unknown", StringComparison.OrdinalIgnoreCase)
               || string.Equals(evidence.AccessMode, "metadata", StringComparison.OrdinalIgnoreCase)
               || string.Equals(evidence.AccessMode, "catalog", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record RankedResearchEvidence(ResearchEvidence Evidence, double Score);

public sealed class ResearchEvidenceRanker
{
    public IReadOnlyList<RankedResearchEvidence> Rank(
        IEnumerable<ResearchEvidence> evidence, string query)
    {
        var terms = Tokenize(query);
        return evidence
            .Where(e => !string.IsNullOrWhiteSpace(e.Title))
            .Select(e =>
            {
                var title = Tokenize(e.Title);
                var overlap = terms.Count == 0 ? 0 : terms.Intersect(title).Count() / (double)terms.Count;
                var provenance = e.SourceScore;
                var access = e.AccessMode.Equals("fulltext", StringComparison.OrdinalIgnoreCase) ? 1.0 :
                             e.AccessMode.Equals("oa", StringComparison.OrdinalIgnoreCase) ? .9 : .55;
                var score = .55 * overlap + .30 * provenance + .15 * access;
                return new RankedResearchEvidence(e, score);
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Evidence.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static HashSet<string> Tokenize(string value) =>
        value.ToLowerInvariant()
            .Split([' ', '\t', '\r', '\n', ',', '.', ':', ';', '-', '_', '/', '(', ')'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Length > 2)
            .ToHashSet(StringComparer.Ordinal);
}

public static class ResearchReviewPlanner
{
    public static ResearchPlan Create(string query, ResearchDiscipline discipline)
    {
        var stages = new List<ResearchPlanStage>
        {
            new("OpenAlex", "discover works, authors, topics and institutions", true),
            new("Crossref", "normalize DOI and bibliographic metadata", false),
            new("Semantic Scholar", "expand paper and citation discovery", false),
            new("OpenCitations", "expand citation relationships", true),
            new("Unpaywall", "resolve lawful open-access locations", true),
            new("GROBID", "parse permitted scholarly PDFs into structured text", false)
        };
        if (discipline is ResearchDiscipline.Sociology or ResearchDiscipline.Psychology or ResearchDiscipline.Humanities)
            stages.Add(new("Institutional repositories", "find university and domain repositories", false));
        return new(stages, true);
    }
}

public sealed class ResearchEngine
{
    private readonly HttpClient _http;
    private readonly ResearchEvidencePolicy _policy;
    private readonly ResearchEvidenceRanker _ranker;

    public ResearchEngine(HttpClient http, ResearchEvidencePolicy? policy = null, ResearchEvidenceRanker? ranker = null)
    {
        _http = http;
        _policy = policy ?? new ResearchEvidencePolicy();
        _ranker = ranker ?? new ResearchEvidenceRanker();
    }

    public async Task<ResearchSearchResult> SearchAsync(
        ResearchSearchRequest request, CancellationToken cancellationToken = default)
    {
        var discipline = request.Discipline == ResearchDiscipline.General
            ? ResearchQueryClassifier.Classify(request.Query)
            : request.Discipline;
        var plan = ResearchReviewPlanner.Create(request.Query, discipline);
        var degraded = new List<string>();
        var candidates = new List<ResearchEvidence>();

        var sourceTasks = new[]
        {
            RunSourceAsync("OpenAlex", () => SearchOpenAlexAsync(request.Query, request.Limit * 2, cancellationToken), candidates, degraded),
            RunSourceAsync("Crossref", () => SearchCrossrefAsync(request.Query, request.Limit, cancellationToken), candidates, degraded),
            RunSourceAsync("OpenCitations", () => SearchOpenCitationsAsync(request.Query, request.Limit, cancellationToken), candidates, degraded)
        };
        await Task.WhenAll(sourceTasks);

        var admissible = candidates.Where(_policy.IsAdmissible);
        if (request.OpenAccessOnly)
            admissible = admissible.Where(x => x.AccessMode is "oa" or "fulltext");
        var ranked = _ranker.Rank(admissible, request.Query).Take(Math.Max(1, request.Limit)).ToArray();

        if (ranked.Length == 0) degraded.Add("No admissible scholarly results returned");
        return new(request.Query, discipline, ranked.Select(x => x.Evidence).ToArray(), plan, degraded);
    }

    private static async Task RunSourceAsync(
        string source,
        Func<Task<IReadOnlyList<ResearchEvidence>>> action,
        List<ResearchEvidence> candidates,
        List<string> degraded)
    {
        try
        {
            var results = await action();
            lock (candidates) candidates.AddRange(results);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        { lock (degraded) degraded.Add($"{source} unavailable"); }
    }

    private async Task<IReadOnlyList<ResearchEvidence>> SearchOpenAlexAsync(
        string query, int limit, CancellationToken cancellationToken)
    {
        var encoded = Uri.EscapeDataString(query);
        var uri = $"https://api.openalex.org/works?search={encoded}&per-page={Math.Clamp(limit, 1, 50)}";
        using var response = await _http.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<OpenAlexResponse>(cancellationToken: cancellationToken)
                      ?? new OpenAlexResponse();
        return payload.Results.Select(MapOpenAlex).ToArray();
    }

    private static ResearchEvidence MapOpenAlex(OpenAlexWork work)
    {
        var oa = work.OpenAccess?.IsOa == true;
        var url = work.PrimaryLocation?.LandingPageUrl
                  ?? work.PrimaryLocation?.PdfUrl
                  ?? work.Doi
                  ?? work.Id
                  ?? "https://openalex.org/";
        var license = work.OpenAccess?.License ?? "unknown";
        var access = oa ? "oa" : "metadata";
        var subjects = work.Topics?.Select(x => x.DisplayName).OfType<string>().Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        return new(
            work.Id ?? work.Doi ?? Guid.NewGuid().ToString("N"),
            work.Title ?? "Untitled work",
            url,
            "OpenAlex",
            access,
            license,
            0.95,
            ReconstructAbstract(work.AbstractInvertedIndex),
            work.Doi,
            subjects,
            work.PublicationDate is null ? null : DateTimeOffset.TryParse(work.PublicationDate, out var d) ? d : null);
    }

    private static string? ReconstructAbstract(Dictionary<string, int[]>? index)
    {
        if (index is null || index.Count == 0) return null;
        var tokens = index.SelectMany(pair => pair.Value.Select(position => (position, token: pair.Key)))
            .OrderBy(x => x.position).Select(x => x.token);
        return string.Join(' ', tokens);
    }

    private async Task<IReadOnlyList<ResearchEvidence>> SearchCrossrefAsync(
        string query, int limit, CancellationToken cancellationToken)
    {
        var uri = $"https://api.crossref.org/works?query.bibliographic={Uri.EscapeDataString(query)}&rows={Math.Clamp(limit, 1, 50)}";
        using var response = await _http.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<CrossrefResponse>(cancellationToken: cancellationToken)
                      ?? new CrossrefResponse();
        return payload.Message.Items.Select(item => new ResearchEvidence(
            item.Doi ?? item.Url ?? Guid.NewGuid().ToString("N"),
            item.Title?.FirstOrDefault() ?? "Untitled work",
            item.Url ?? (item.Doi is null ? "https://doi.org/" : $"https://doi.org/{item.Doi}"),
            "Crossref",
            "metadata",
            "unknown",
            0.88,
            null,
            item.Doi,
            null,
            item.Published?.DateParts is { Count: > 0 } parts && parts[0].Count >= 1
                ? new DateTimeOffset(parts[0][0], parts[0].Count >= 2 ? parts[0][1] : 1, parts[0].Count >= 3 ? parts[0][2] : 1, 0, 0, 0, TimeSpan.Zero)
                : null)).ToArray();
    }

    private async Task<IReadOnlyList<ResearchEvidence>> SearchOpenCitationsAsync(
        string query, int limit, CancellationToken cancellationToken)
    {
        // OpenCitations is primarily a citation graph, so use it for DOI expansion
        // when the query itself contains a DOI. For natural-language queries it is
        // intentionally treated as a degraded/empty expansion rather than scraping.
        var doi = query.Trim().TrimEnd('.').StartsWith("10.", StringComparison.OrdinalIgnoreCase)
            ? query.Trim().TrimEnd('.')
            : null;
        if (doi is null) return [];

        var uri = $"https://api.opencitations.net/index/v2/citations/{Uri.EscapeDataString(doi)}";
        using var response = await _http.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        var rows = await response.Content.ReadFromJsonAsync<List<OpenCitationRow>>(cancellationToken: cancellationToken) ?? [];
        return rows.Take(Math.Clamp(limit, 1, 50)).Select((row, i) => new ResearchEvidence(
            $"{doi}:citation:{i}:{row.Citing}",
            $"Citation of {doi}: {row.Citing}",
            $"https://doi.org/{row.Citing}",
            "OpenCitations",
            "metadata",
            "CC0",
            0.86,
            null,
            row.Citing,
            ["citation-graph"])).ToArray();
    }

    private sealed record CrossrefResponse([property: JsonPropertyName("message")] CrossrefMessage Message)
    {
        public CrossrefResponse() : this(new CrossrefMessage()) { }
    }

    private sealed record CrossrefMessage([property: JsonPropertyName("items")] List<CrossrefItem> Items)
    {
        public CrossrefMessage() : this([]) { }
    }

    private sealed record CrossrefItem(
        [property: JsonPropertyName("DOI")] string? Doi = null,
        [property: JsonPropertyName("URL")] string? Url = null,
        [property: JsonPropertyName("title")] List<string>? Title = null,
        [property: JsonPropertyName("published")] CrossrefPublished? Published = null);

    private sealed record CrossrefPublished(
        [property: JsonPropertyName("date-parts")] List<List<int>> DateParts);

    private sealed record OpenCitationRow(
        [property: JsonPropertyName("citing")] string Citing = "");

    private sealed record OpenAlexResponse([property: JsonPropertyName("results")] List<OpenAlexWork> Results)
    {
        public OpenAlexResponse() : this([]) { }
    }

    private sealed record OpenAlexWork(
        [property: JsonPropertyName("id")] string? Id = null,
        [property: JsonPropertyName("doi")] string? Doi = null,
        [property: JsonPropertyName("title")] string? Title = null,
        [property: JsonPropertyName("publication_date")] string? PublicationDate = null,
        [property: JsonPropertyName("primary_location")] OpenAlexLocation? PrimaryLocation = null,
        [property: JsonPropertyName("open_access")] OpenAlexOpenAccess? OpenAccess = null,
        [property: JsonPropertyName("topics")] List<OpenAlexTopic>? Topics = null,
        [property: JsonPropertyName("abstract_inverted_index")] Dictionary<string, int[]>? AbstractInvertedIndex = null);

    private sealed record OpenAlexLocation(
        [property: JsonPropertyName("landing_page_url")] string? LandingPageUrl = null,
        [property: JsonPropertyName("pdf_url")] string? PdfUrl = null);

    private sealed record OpenAlexOpenAccess(
        [property: JsonPropertyName("is_oa")] bool IsOa = false,
        [property: JsonPropertyName("license")] string? License = null);

    private sealed record OpenAlexTopic(
        [property: JsonPropertyName("display_name")] string? DisplayName = null);
}
