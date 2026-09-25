using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace SohailOS.Ecosystem;

public sealed class OpenAlexProvider(HttpClient http) : IScholarlyProvider
{
    public string Name => "OpenAlex";
    public bool IsConfigured => true;

    public async Task<ProviderSearchResult> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var uri = $"https://api.openalex.org/works?search={Uri.EscapeDataString(query)}&per-page={Math.Clamp(limit, 1, 50)}";
        using var response = await http.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<OpenAlexResponse>(cancellationToken: cancellationToken)
                      ?? new OpenAlexResponse();

        var records = payload.Results.Select(Map).ToArray();
        return new(Name, records, false, null);
    }

    private static ScholarlyRecord Map(OpenAlexWork work)
    {
        var oa = work.OpenAccess?.IsOa == true;
        var locations = work.PrimaryLocation is null
            ? []
            : new[]
            {
                work.PrimaryLocation.PdfUrl is null
                    ? null
                    : new OpenAccessLocation(work.PrimaryLocation.PdfUrl, "pdf", work.OpenAccess?.License),
                work.PrimaryLocation.LandingPageUrl is null
                    ? null
                    : new OpenAccessLocation(work.PrimaryLocation.LandingPageUrl, "landing", work.OpenAccess?.License)
            }.Where(x => x is not null).Cast<OpenAccessLocation>().ToArray();

        return new ScholarlyRecord(
            work.Id ?? work.Doi ?? Guid.NewGuid().ToString("N"),
            work.Title ?? "Untitled work",
            work.PrimaryLocation?.LandingPageUrl ?? work.Doi ?? work.Id ?? "https://openalex.org/",
            work.Doi,
            "OpenAlex",
            0.95,
            ReconstructAbstract(work.AbstractInvertedIndex),
            work.Authorships?.Select(x => new ScholarlyAuthor(
                x.Author?.DisplayName ?? "Unknown",
                x.Author?.Id)).ToArray(),
            work.Topics?.Select(x => x.DisplayName).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToArray(),
            DateTimeOffset.TryParse(work.PublicationDate, out var published) ? published : null,
            locations,
            oa ? "oa" : "metadata",
            work.OpenAccess?.License ?? "unknown");
    }

    private static string? ReconstructAbstract(Dictionary<string, int[]>? index)
    {
        if (index is null || index.Count == 0) return null;
        return string.Join(' ', index
            .SelectMany(pair => pair.Value.Select(position => (position, token: pair.Key)))
            .OrderBy(x => x.position)
            .Select(x => x.token));
    }

    private sealed record OpenAlexResponse(
        [property: JsonPropertyName("results")] List<OpenAlexWork> Results)
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
        [property: JsonPropertyName("authorships")] List<OpenAlexAuthorship>? Authorships = null,
        [property: JsonPropertyName("abstract_inverted_index")] Dictionary<string, int[]>? AbstractInvertedIndex = null);

    private sealed record OpenAlexLocation(
        [property: JsonPropertyName("landing_page_url")] string? LandingPageUrl = null,
        [property: JsonPropertyName("pdf_url")] string? PdfUrl = null);

    private sealed record OpenAlexOpenAccess(
        [property: JsonPropertyName("is_oa")] bool IsOa = false,
        [property: JsonPropertyName("license")] string? License = null);

    private sealed record OpenAlexTopic(
        [property: JsonPropertyName("display_name")] string? DisplayName = null);

    private sealed record OpenAlexAuthorship(
        [property: JsonPropertyName("author")] OpenAlexAuthor? Author = null);

    private sealed record OpenAlexAuthor(
        [property: JsonPropertyName("id")] string? Id = null,
        [property: JsonPropertyName("display_name")] string? DisplayName = null);
}

public sealed class CrossrefProvider(HttpClient http) : IScholarlyProvider
{
    public string Name => "Crossref";
    public bool IsConfigured => true;

    public async Task<ProviderSearchResult> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var uri = $"https://api.crossref.org/works?query.bibliographic={Uri.EscapeDataString(query)}&rows={Math.Clamp(limit, 1, 50)}";
        using var response = await http.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<CrossrefResponse>(cancellationToken: cancellationToken)
                      ?? new CrossrefResponse();

        var records = payload.Message.Items.Select(item =>
        {
            var url = item.URL ?? (item.DOI is null ? "https://doi.org/" : $"https://doi.org/{item.DOI}");
            var published = item.Published?.DateParts is { Count: > 0 } parts && parts[0].Count >= 1
                ? ToDate(parts[0])
                : null;

            return new ScholarlyRecord(
                item.DOI ?? url,
                item.Title?.FirstOrDefault() ?? "Untitled work",
                url,
                item.DOI,
                "Crossref",
                0.88,
                item.Abstract,
                item.Author?.Select(x => new ScholarlyAuthor(
                    $"{x.Given} {x.Family}".Trim())).ToArray(),
                null,
                published,
                [],
                "metadata",
                "unknown");
        }).ToArray();

        return new(Name, records, false, null);
    }

    private static DateTimeOffset? ToDate(IReadOnlyList<int> parts)
    {
        try
        {
            return new DateTimeOffset(
                parts[0],
                parts.Count >= 2 ? parts[1] : 1,
                parts.Count >= 3 ? parts[2] : 1,
                0, 0, 0,
                TimeSpan.Zero);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private sealed record CrossrefResponse(
        [property: JsonPropertyName("message")] CrossrefMessage Message)
    {
        public CrossrefResponse() : this(new CrossrefMessage()) { }
    }

    private sealed record CrossrefMessage(
        [property: JsonPropertyName("items")] List<CrossrefItem> Items)
    {
        public CrossrefMessage() : this([]) { }
    }

    private sealed record CrossrefItem(
        [property: JsonPropertyName("DOI")] string? DOI = null,
        [property: JsonPropertyName("URL")] string? URL = null,
        [property: JsonPropertyName("title")] List<string>? Title = null,
        [property: JsonPropertyName("abstract")] string? Abstract = null,
        [property: JsonPropertyName("author")] List<CrossrefAuthor>? Author = null,
        [property: JsonPropertyName("published")] CrossrefPublished? Published = null);

    private sealed record CrossrefAuthor(
        [property: JsonPropertyName("given")] string? Given = null,
        [property: JsonPropertyName("family")] string? Family = null);

    private sealed record CrossrefPublished(
        [property: JsonPropertyName("date-parts")] List<List<int>> DateParts);
}

public sealed class SemanticScholarProvider(HttpClient http, string? apiKey = null) : IScholarlyProvider
{
    public string Name => "Semantic Scholar";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(apiKey);

    public async Task<ProviderSearchResult> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return new(Name, [], true, "credentials_missing");

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://api.semanticscholar.org/graph/v1/paper/search?query={Uri.EscapeDataString(query)}&limit={Math.Clamp(limit, 1, 50)}&fields=title,abstract,year,authors,externalIds,url,openAccessPdf");
        request.Headers.TryAddWithoutValidation("x-api-key", apiKey);

        using var response = await http.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
            response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            return new(Name, [], true, "credentials_rejected");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<S2Response>(cancellationToken: cancellationToken)
                      ?? new S2Response();

        var records = payload.Data.Select(x => new ScholarlyRecord(
            x.PaperId ?? x.ExternalIds?.DOI ?? Guid.NewGuid().ToString("N"),
            x.Title ?? "Untitled work",
            x.Url ?? (x.ExternalIds?.DOI is null ? "https://www.semanticscholar.org/" : $"https://doi.org/{x.ExternalIds.DOI}"),
            x.ExternalIds?.DOI,
            Name,
            0.90,
            x.Abstract,
            x.Authors?.Select(a => new ScholarlyAuthor(a.Name ?? "Unknown", a.AuthorId)).ToArray(),
            null,
            x.Year is null ? null : new DateTimeOffset(x.Year.Value, 1, 1, 0, 0, 0, TimeSpan.Zero),
            x.OpenAccessPdf?.Url is null ? [] : [new OpenAccessLocation(x.OpenAccessPdf.Url, "pdf")],
            x.OpenAccessPdf?.Url is null ? "metadata" : "oa",
            "unknown")).ToArray();

        return new(Name, records, false, null);
    }

    private sealed record S2Response(
        [property: JsonPropertyName("data")] List<S2Paper> Data)
    {
        public S2Response() : this([]) { }
    }

    private sealed record S2Paper(
        [property: JsonPropertyName("paperId")] string? PaperId = null,
        [property: JsonPropertyName("title")] string? Title = null,
        [property: JsonPropertyName("abstract")] string? Abstract = null,
        [property: JsonPropertyName("year")] int? Year = null,
        [property: JsonPropertyName("authors")] List<S2Author>? Authors = null,
        [property: JsonPropertyName("externalIds")] S2ExternalIds? ExternalIds = null,
        [property: JsonPropertyName("url")] string? Url = null,
        [property: JsonPropertyName("openAccessPdf")] S2OpenAccessPdf? OpenAccessPdf = null);

    private sealed record S2Author(
        [property: JsonPropertyName("authorId")] string? AuthorId = null,
        [property: JsonPropertyName("name")] string? Name = null);

    private sealed record S2ExternalIds(
        [property: JsonPropertyName("DOI")] string? DOI = null);

    private sealed record S2OpenAccessPdf(
        [property: JsonPropertyName("url")] string? Url = null);
}

public sealed class EuropePmcProvider(HttpClient http) : IScholarlyProvider
{
    public string Name => "Europe PMC";
    public bool IsConfigured => true;

    public async Task<ProviderSearchResult> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var uri = $"https://www.ebi.ac.uk/europepmc/webservices/rest/search?query={Uri.EscapeDataString(query)}&format=json&pageSize={Math.Clamp(limit, 1, 50)}&resultType=core";
        using var response = await http.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<EpmcResponse>(cancellationToken: cancellationToken)
                      ?? new EpmcResponse();

        var records = payload.ResponseList?.Results.Select(x => new ScholarlyRecord(
            x.Id ?? x.DOI ?? Guid.NewGuid().ToString("N"),
            x.Title ?? "Untitled work",
            x.FullTextUrlList?.FullTextUrl?.FirstOrDefault()?.Url
                ?? (x.DOI is null ? "https://europepmc.org/" : $"https://doi.org/{x.DOI}"),
            x.DOI,
            Name,
            0.84,
            x.AbstractText,
            x.AuthorString?.Split(", ", StringSplitOptions.RemoveEmptyEntries)
                .Select(a => new ScholarlyAuthor(a))
                .ToArray(),
            x.Subjects?.Select(s => s.Term).Where(s => !string.IsNullOrWhiteSpace(s)).Cast<string>().ToArray(),
            DateTimeOffset.TryParse(x.FirstPublicationDate, out var date) ? date : null,
            x.FullTextUrlList?.FullTextUrl is null
                ? []
                : x.FullTextUrlList.FullTextUrl
                    .Where(u => !string.IsNullOrWhiteSpace(u.Url))
                    .Select(u => new OpenAccessLocation(u.Url!, "fulltext"))
                    .ToArray(),
            x.FullTextUrlList?.FullTextUrl is null ? "metadata" : "oa",
            "unknown")).ToArray() ?? [];

        return new(Name, records, false, null);
    }

    private sealed record EpmcResponse(
        [property: JsonPropertyName("resultList")] EpmcResultList? ResponseList)
    {
        public EpmcResponse() : this((EpmcResultList?)null) { }
    }

    private sealed record EpmcResultList(
        [property: JsonPropertyName("result")] List<EpmcResult> Results);

    private sealed record EpmcResult(
        [property: JsonPropertyName("id")] string? Id = null,
        [property: JsonPropertyName("doi")] string? DOI = null,
        [property: JsonPropertyName("title")] string? Title = null,
        [property: JsonPropertyName("abstractText")] string? AbstractText = null,
        [property: JsonPropertyName("authorString")] string? AuthorString = null,
        [property: JsonPropertyName("firstPublicationDate")] string? FirstPublicationDate = null,
        [property: JsonPropertyName("fullTextUrlList")] EpmcFullTextUrlList? FullTextUrlList = null,
        [property: JsonPropertyName("subject")] List<EpmcSubject>? Subjects = null);

    private sealed record EpmcFullTextUrlList(
        [property: JsonPropertyName("fullTextUrl")] List<EpmcFullTextUrl> FullTextUrl);

    private sealed record EpmcFullTextUrl(
        [property: JsonPropertyName("url")] string? Url = null);

    private sealed record EpmcSubject(
        [property: JsonPropertyName("term")] string? Term = null);
}

public sealed class OpenCitationsProvider(HttpClient http) : IScholarlyProvider, ICitationGraphProvider
{
    public string Name => "OpenCitations";
    public bool IsConfigured => true;

    public async Task<ProviderSearchResult> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var doi = query.Trim().TrimEnd('.');
        if (!doi.StartsWith("10.", StringComparison.OrdinalIgnoreCase))
            return new(Name, [], false, null);

        var edges = await GetCitationEdgesAsync(doi, limit, cancellationToken);
        var records = edges.Select((edge, i) => new ScholarlyRecord(
            $"{doi}:citation:{i}:{edge.CitingDoi}",
            $"Citation of {doi}: {edge.CitingDoi}",
            $"https://doi.org/{edge.CitingDoi}",
            edge.CitingDoi,
            Name,
            0.86,
            Subjects: ["citation-graph"])).ToArray();

        return new(Name, records, false, null);
    }

    public async Task<IReadOnlyList<CitationEdge>> GetCitationEdgesAsync(
        string doi,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var normalized = ScholarlyCanonicalizer.NormalizeDoi(doi);
        if (string.IsNullOrWhiteSpace(normalized)) return [];

        var uri = $"https://api.opencitations.net/index/v2/citations/{Uri.EscapeDataString(normalized)}";
        using var response = await http.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        var rows = await response.Content.ReadFromJsonAsync<List<OpenCitationRow>>(cancellationToken: cancellationToken) ?? [];

        return rows
            .Where(x => !string.IsNullOrWhiteSpace(x.Citing))
            .Take(Math.Clamp(limit, 1, 50))
            .Select(x => new CitationEdge(x.Citing, normalized, Name))
            .ToArray();
    }

    private sealed record OpenCitationRow(
        [property: JsonPropertyName("citing")] string Citing = "");
}

public sealed class UnpaywallProvider(HttpClient http, string? email) : IOpenAccessResolver
{
    public string Name => "Unpaywall";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(email);

    public async Task<ScholarlyRecord?> EnrichAsync(
        ScholarlyRecord record,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(record.Doi))
            return record;

        var doi = ScholarlyCanonicalizer.NormalizeDoi(record.Doi);
        var uri = $"https://api.unpaywall.org/v2/{Uri.EscapeDataString(doi)}?email={Uri.EscapeDataString(email!)}";
        using var response = await http.GetAsync(uri, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return record;
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<UnpaywallResponse>(cancellationToken: cancellationToken)
                      ?? new UnpaywallResponse();

        IReadOnlyList<OpenAccessLocation> locations = payload.BestOaLocation is not null
            ? new[]
            {
                new OpenAccessLocation(
                    payload.BestOaLocation.EffectiveUrl,
                    payload.BestOaLocation.Version,
                    payload.BestOaLocation.License,
                    payload.BestOaLocation.HostType)
            }
            : Array.Empty<OpenAccessLocation>();

        return record with
        {
            AccessMode = locations.Count > 0 ? "oa" : record.AccessMode,
            License = locations.Count > 0 ? locations[0].License ?? record.License : record.License,
            OpenAccessLocations = (record.OpenAccessLocations ?? []).Concat(locations).DistinctBy(x => x.Url, StringComparer.OrdinalIgnoreCase).ToArray(),
            Url = locations.FirstOrDefault()?.Url ?? record.Url
        };
    }

    private sealed record UnpaywallResponse(
        [property: JsonPropertyName("best_oa_location")] UnpaywallLocation? BestOaLocation = null);

    private sealed record UnpaywallLocation(
        [property: JsonPropertyName("url")] string? RawUrl = null,
        [property: JsonPropertyName("url_for_pdf")] string? UrlForPdf = null,
        [property: JsonPropertyName("version")] string? Version = null,
        [property: JsonPropertyName("license")] string? License = null,
        [property: JsonPropertyName("host_type")] string? HostType = null)
    {
        public string EffectiveUrl => UrlForPdf ?? RawUrl ?? "https://doi.org/";
    }
}

public sealed class OaiPmhProvider(HttpClient http, string endpoint) : IRepositoryHarvester
{
    public string Name => "Institutional OAI-PMH";

    public async Task<IReadOnlyList<ScholarlyRecord>> HarvestAsync(
        string? set,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var separator = endpoint.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        var query = $"{separator}verb=ListRecords&metadataPrefix=oai_dc";
        if (!string.IsNullOrWhiteSpace(set))
            query += $"&set={Uri.EscapeDataString(set)}";

        using var response = await http.GetAsync(endpoint + query, cancellationToken);
        response.EnsureSuccessStatusCode();
        var xml = await response.Content.ReadAsStringAsync(cancellationToken);
        return OaiPmhParser.Parse(xml, Name).Take(Math.Clamp(limit, 1, 100)).ToArray();
    }
}

public interface IRepositoryHarvester
{
    string Name { get; }

    Task<IReadOnlyList<ScholarlyRecord>> HarvestAsync(
        string? set,
        int limit,
        CancellationToken cancellationToken = default);
}

public static class OaiPmhParser
{
    private static readonly XNamespace Oai = "http://www.openarchives.org/OAI/2.0/";
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";

    public static IReadOnlyList<ScholarlyRecord> Parse(
        string xml,
        string source)
    {
        var doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);

        return doc.Descendants(Oai + "record")
            .Select(record =>
            {
                var headerId = record.Element(Oai + "header")?.Element(Oai + "identifier")?.Value;
                var dc = record.Descendants(Dc + "dc").FirstOrDefault();
                var titles = dc?.Elements(Dc + "title").Select(x => x.Value.Trim()).Where(x => x.Length > 0).ToArray() ?? [];
                var authors = dc?.Elements(Dc + "creator").Select(x => new ScholarlyAuthor(x.Value.Trim())).ToArray() ?? [];
                var subjects = dc?.Elements(Dc + "subject").Select(x => x.Value.Trim()).Where(x => x.Length > 0).ToArray() ?? [];
                var identifiers = dc?.Elements(Dc + "identifier").Select(x => x.Value.Trim()).Where(x => x.Length > 0).ToArray() ?? [];
                var url = identifiers.FirstOrDefault(x => Uri.TryCreate(x, UriKind.Absolute, out _))
                          ?? identifiers.FirstOrDefault()
                          ?? "https://oai.example/";

                return new ScholarlyRecord(
                    headerId ?? url,
                    titles.FirstOrDefault() ?? "Untitled repository record",
                    url,
                    identifiers.FirstOrDefault(x => x.Contains("doi.org/", StringComparison.OrdinalIgnoreCase))?.Split("doi.org/", StringSplitOptions.None).Last(),
                    source,
                    0.72,
                    Authors: authors,
                    Subjects: subjects,
                    AccessMode: "repository",
                    License: "unknown");
            })
            .ToArray();
    }
}

public sealed record GrobidSection(
    string Title,
    string Text);

public sealed record GrobidCitation(
    string Key,
    string? Doi,
    string? Title);

public sealed record GrobidDocument(
    IReadOnlyList<GrobidSection> Sections,
    IReadOnlyList<GrobidCitation> Citations);

public static class GrobidParser
{
    private static readonly XNamespace Tei = "http://www.tei-c.org/ns/1.0";

    public static GrobidDocument Parse(string teiXml)
    {
        var doc = XDocument.Parse(teiXml, LoadOptions.PreserveWhitespace);

        var sections = doc.Descendants(Tei + "div")
            .Select(div => new GrobidSection(
                div.Element(Tei + "head")?.Value.Trim() ?? "Untitled section",
                string.Join(
                    " ",
                    div.Descendants(Tei + "p").Select(p => p.Value.Trim()).Where(x => x.Length > 0))))
            .Where(x => x.Text.Length > 0)
            .ToArray();

        var citations = doc.Descendants(Tei + "biblStruct")
            .Select(bibl =>
            {
                var id = (string?)bibl.Attribute(XNamespace.Xml + "id") ?? Guid.NewGuid().ToString("N");
                var doi = bibl.Descendants(Tei + "idno")
                    .FirstOrDefault(x => string.Equals((string?)x.Attribute("type"), "DOI", StringComparison.OrdinalIgnoreCase))
                    ?.Value.Trim();
                var title = bibl.Descendants(Tei + "title").FirstOrDefault()?.Value.Trim();

                return new GrobidCitation(id, doi, title);
            })
            .ToArray();

        return new GrobidDocument(sections, citations);
    }
}

public sealed class GrobidClient(HttpClient http, Uri baseUri)
{
    public async Task<GrobidDocument> ParsePdfAsync(
        Stream pdf,
        string fileName = "document.pdf",
        CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        using var file = new StreamContent(pdf);
        content.Add(file, "input", fileName);

        using var response = await http.PostAsync(
            new Uri(baseUri, "api/processFulltextDocument"),
            content,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var tei = await response.Content.ReadAsStringAsync(cancellationToken);
        return GrobidParser.Parse(tei);
    }
}
