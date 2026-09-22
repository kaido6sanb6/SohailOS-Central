using Xunit;

namespace SohailOS.Tests;

public sealed class KnowledgeContractsTests
{
    [Fact]
    public void DataPlaneProviderContract_Exists()
    {
        var type = Type.GetType(
            "SohailOS.Ecosystem.DataPlaneProvider, SohailOS.Ecosystem",
            throwOnError: false);

        Assert.NotNull(type);
    }
}
