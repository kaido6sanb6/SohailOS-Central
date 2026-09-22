using Xunit;

namespace SohailOS.Tests;

public class LiveCorpusPolicyTests
{
    [Fact]
    public void Live_corpus_contains_both_upstream_and_personal_fork()
    {
        var manifest = SohailOS.Ecosystem.LiveCorpusManifest.CreateDefault();

        Assert.Contains(manifest.Sources, x => x.Repository == "asgeirtj/system_prompts_leaks");
        Assert.Contains(manifest.Sources, x => x.Repository == "kaido6sanb6/system_prompts_leaks");
        Assert.All(manifest.Sources, x => Assert.Equal("untrusted-data", x.Trust));
    }

    [Fact]
    public void Live_corpus_pipeline_has_required_stages()
    {
        var manifest = SohailOS.Ecosystem.LiveCorpusManifest.CreateDefault();

        Assert.Equal(
            new[] { "fetch", "inspect", "classify", "extract", "normalize", "deduplicate", "compare", "verify", "synthesize", "index" },
            manifest.Stages);
    }
}
