using Xunit;

namespace SohailOS.Tests;

public sealed class KnowledgeRetrievalTests
{
    [Fact]
    public void Gate0DataPlaneProvider_Exists()
    {
        var type = Type.GetType(
            "SohailOS.Ecosystem.InMemoryDataPlaneProvider, SohailOS.Ecosystem",
            throwOnError: false);

        Assert.NotNull(type);
    }
}
