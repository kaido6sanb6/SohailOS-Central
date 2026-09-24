using SohailOS.Ecosystem;
using Xunit;

namespace SohailOS.Tests;

public sealed class ResearchEngineTests
{
    [Fact]
    public void QueryClassifier_RoutesSociologyAndPsychologyQueries()
    {
        Assert.Equal(ResearchDiscipline.Sociology,
            ResearchQueryClassifier.Classify("gender socialization and self-silencing among university students"));
        Assert.Equal(ResearchDiscipline.Psychology,
            ResearchQueryClassifier.Classify("depression rumination and social anxiety"));
    }

    [Fact]
    public void EvidencePolicy_RejectsPaywallBypassAndRequiresProvenance()
    {
        var policy = new ResearchEvidencePolicy();
        Assert.False(policy.IsAdmissible(new ResearchEvidence(
            "x", "title", "https://example.org", "zotero-scihub",
            "paywall-bypass", "unknown", 0.9)));
        Assert.True(policy.IsAdmissible(new ResearchEvidence(
            "x", "title", "https://example.org", "OpenAlex",
            "metadata", "CC0", 0.9)));
    }

    [Fact]
    public void FusionRanker_PrefersRelevantEvidenceWithProvenance()
    {
        var ranker = new ResearchEvidenceRanker();
        var results = ranker.Rank(
        [
            new ResearchEvidence("a","Gender socialization and mental health",
                "https://a","OpenAlex","metadata","CC0",0.8),
            new ResearchEvidence("b","Cooking recipes",
                "https://b","unknown","metadata","unknown",0.99)
        ], "gender socialization mental health");

        Assert.Equal("a", results[0].Evidence.Id);
    }

    [Fact]
    public void ReviewPlanner_ProducesTraceableResearchPlan()
    {
        var plan = ResearchReviewPlanner.Create("self-silencing learned helplessness mental health",
            ResearchDiscipline.Sociology);
        Assert.NotEmpty(plan.Stages);
        Assert.Contains(plan.Stages, x => x.Source == "OpenAlex");
        Assert.Contains(plan.Stages, x => x.Source == "OpenCitations");
        Assert.Contains(plan.Stages, x => x.Source == "Unpaywall");
        Assert.True(plan.RequiresCitations);
    }
}
