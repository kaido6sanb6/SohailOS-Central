using Xunit;

namespace SohailOS.Tests;

public sealed class GitHubKnowledgeIndexerTests
{
    [Fact]
    public void KnowledgeIndexerContract_Exists()
    {
        var type = Type.GetType(
            "SohailOS.Ecosystem.GitHubKnowledgeIndexer, SohailOS.Ecosystem",
            throwOnError: false);

        Assert.NotNull(type);
    }
}
