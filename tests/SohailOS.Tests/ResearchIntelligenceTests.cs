using System.Net;
using System.Text;
using SohailOS.Ecosystem;
using Xunit;

namespace SohailOS.Tests;

public sealed class ResearchIntelligenceTests
{
    [Fact]
    public void ScholarlyCanonicalizer_DeduplicatesByDoiThenNormalizedTitle()
    {
        var records = new[]
        {
            new ScholarlyRecord("openalex:1", "Gender Socialization and Mental Health",
                "https://doi.org/10.1000/ABC", "10.1000/ABC", "OpenAlex", 0.95),
            new ScholarlyRecord("crossref:2", "gender socialization and mental health.",
                "https://doi.org/10.1000/abc", "10.1000/abc", "Crossref", 0.88),
            new ScholarlyRecord("other:3", "Another Study", "https://example.org/3", null, "Other", 0.50)
        };

        var unique = ScholarlyCanonicalizer.Deduplicate(records);

        Assert.Equal(2, unique.Count);
        Assert.Contains(unique, x => x.Doi == "10.1000/ABC");
        Assert.Contains(unique, x => x.Title == "Another Study");
    }

    [Fact]
    public async Task IntelligenceEngine_FederatesProvidersAndProducesTraceableAudit()
    {
        var providers = new IScholarlyProvider[]
        {
            new FakeScholarlyProvider("OpenAlex", new[]
            {
                new ScholarlyRecord("oa:1", "Gender socialization study", "https://doi.org/10.1/x", "10.1/x", "OpenAlex", 0.95)
            }),
            new FakeScholarlyProvider("Crossref", new[]
            {
                new ScholarlyRecord("cr:1", "Gender socialization study", "https://doi.org/10.1/x", "10.1/x", "Crossref", 0.88),
                new ScholarlyRecord("cr:2", "Self-silencing study", "https://example.org/2", null, "Crossref", 0.88)
            })
        };

        var engine = new ResearchIntelligenceEngine(providers);
        var result = await engine.SearchAsync(
            new ResearchSearchRequest("gender socialization self-silencing", ResearchDiscipline.Sociology, 10));

        Assert.Equal(2, result.Records.Count);
        Assert.NotEmpty(result.AuditTrail);
        Assert.Contains(result.AuditTrail, x => x.Provider == "OpenAlex");
        Assert.Contains(result.Plan.Stages, x => x.Name == "canonicalize");
        Assert.True(result.Graph.Nodes.Count >= 2);
    }

    [Fact]
    public async Task OptionalProviderWithoutCredentials_DegradesWithoutBreakingSearch()
    {
        var handler = new StubHttpHandler((request, _) =>
        {
            Assert.Contains("api.semanticscholar.org", request.RequestUri!.Host);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("{"message":"missing key"}", Encoding.UTF8, "application/json")
            });
        });
        using var http = new HttpClient(handler);

        var provider = new SemanticScholarProvider(http, apiKey: null);
        var result = await provider.SearchAsync("social anxiety", 5);

        Assert.Empty(result.Records);
        Assert.True(result.Degraded);
        Assert.Equal("credentials_missing", result.DegradedReason);
    }

    [Fact]
    public async Task UnpaywallProvider_EnrichesRecordWithLawfulOpenAccessLocation()
    {
        var handler = new StubHttpHandler((request, _) =>
        {
            Assert.Equal("api.unpaywall.org", request.RequestUri!.Host);
            Assert.Contains("email=research%40example.org", request.RequestUri.Query);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"best_oa_location":{"url":"https://repository.example/paper.pdf","url_for_pdf":"https://repository.example/paper.pdf","version":"acceptedVersion","license":"cc-by","host_type":"repository"}}""",
                    Encoding.UTF8,
                    "application/json")
            });
        });
        using var http = new HttpClient(handler);
        var provider = new UnpaywallProvider(http, "research@example.org");

        var original = new ScholarlyRecord(
            "work:1",
            "Open access study",
            "https://doi.org/10.1000/test",
            "10.1000/test",
            "Crossref",
            .88);

        var enriched = await provider.EnrichAsync(original);

        Assert.NotNull(enriched);
        Assert.Equal("oa", enriched!.AccessMode);
        Assert.Equal("cc-by", enriched.License);
        Assert.Contains(enriched.OpenAccessLocations!, x => x.Url == "https://repository.example/paper.pdf");
    }

    [Fact]
    public void OaiPmhParser_MapsDublinCoreFields()
    {
        const string xml = """
        <OAI-PMH xmlns="http://www.openarchives.org/OAI/2.0/">
          <ListRecords>
            <record>
              <header><identifier>oai:example:1</identifier><datestamp>2026-01-10</datestamp></header>
              <metadata>
                <oai_dc:dc xmlns:oai_dc="http://www.openarchives.org/OAI/2.0/oai_dc/"
                           xmlns:dc="http://purl.org/dc/elements/1.1/">
                  <dc:title>University Repository Study</dc:title>
                  <dc:creator>Jane Doe</dc:creator>
                  <dc:subject>sociology</dc:subject>
                  <dc:date>2026-01-10</dc:date>
                  <dc:identifier>https://repository.example/item/1</dc:identifier>
                </oai_dc:dc>
              </metadata>
            </record>
          </ListRecords>
        </OAI-PMH>
        """;

        var records = OaiPmhParser.Parse(xml, "Institutional OAI-PMH");

        var record = Assert.Single(records);
        Assert.Equal("University Repository Study", record.Title);
        Assert.Contains("Jane Doe", record.Authors);
        Assert.Contains("sociology", record.Subjects);
        Assert.Equal("https://repository.example/item/1", record.Url);
    }

    [Fact]
    public void GrobidParser_ExtractsSectionsAndCitationKeys()
    {
        const string tei = """
        <TEI xmlns="http://www.tei-c.org/ns/1.0">
          <text>
            <body>
              <div><head>Introduction</head><p>Socialization shapes norms <ref type="bibr" target="#b1">[1]</ref>.</p></div>
              <div><head>Method</head><p>Survey of university students.</p></div>
            </body>
          </text>
          <back>
            <listBibl>
              <biblStruct xml:id="b1">
                <analytic><title>Gender schemas</title></analytic>
                <idno type="DOI">10.1000/gender</idno>
              </biblStruct>
            </listBibl>
          </back>
        </TEI>
        """;

        var document = GrobidParser.Parse(tei);

        Assert.Equal(2, document.Sections.Count);
        Assert.Single(document.Citations);
        Assert.Equal("10.1000/gender", document.Citations[0].Doi);
        Assert.Contains("Introduction", document.Sections.Select(x => x.Title));
    }

    [Fact]
    public void KnowledgeGraphBuilder_CreatesCitationEdges()
    {
        var source = new ScholarlyRecord("a", "Foundational study", "https://doi.org/10.1/a", "10.1/a", "OpenAlex", .95);
        var citing = new ScholarlyRecord("b", "Later study", "https://doi.org/10.1/b", "10.1/b", "OpenAlex", .95);

        var graph = ScholarlyGraphBuilder.Build(
            new[] { source, citing },
            new[] { new CitationEdge("10.1/b", "10.1/a", "OpenCitations") });

        Assert.Contains(graph.Nodes, x => x.Id == "doi:10.1/a");
        Assert.Contains(graph.Edges, x =>
            x.Type == ScholarlyEdgeType.Cites &&
            x.SourceId == "doi:10.1/b" &&
            x.TargetId == "doi:10.1/a");
    }

    [Fact]
    public void SystematicReviewPlanner_CreatesAuditableEvidenceWorkflow()
    {
        var plan = SystematicReviewPlanner.Create(
            "self-silencing AND mental health",
            ResearchDiscipline.Psychology);

        Assert.Contains(plan.Stages, x => x.Name == "search");
        Assert.Contains(plan.Stages, x => x.Name == "deduplicate");
        Assert.Contains(plan.Stages, x => x.Name == "screen");
        Assert.Contains(plan.Stages, x => x.Name == "extract");
        Assert.Contains(plan.Stages, x => x.Name == "audit");
        Assert.True(plan.RequiresPrismaStyleAuditTrail);
    }

    private sealed class FakeScholarlyProvider(
        string name,
        IReadOnlyList<ScholarlyRecord> records) : IScholarlyProvider
    {
        public string Name => name;
        public bool IsConfigured => true;

        public Task<ProviderSearchResult> SearchAsync(
            string query,
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProviderSearchResult(Name, records.Take(limit).ToArray(), false, null));
    }

    private sealed class StubHttpHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => handler(request, cancellationToken);
    }
}
