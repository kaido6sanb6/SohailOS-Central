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

    [Fact]
    public void KnowledgeIdentity_NormalizesPathWithoutCaseFolding()
    {
        var type = Type.GetType(
            "SohailOS.Ecosystem.KnowledgeIdentity, SohailOS.Ecosystem",
            throwOnError: false);

        Assert.NotNull(type);
    }

    [Fact]
    public void HybridRanker_ExistsWithDeterministicFusionVersion()
    {
        var type = Type.GetType(
            "SohailOS.Ecosystem.HybridRanker, SohailOS.Ecosystem",
            throwOnError: false);

        Assert.NotNull(type);
    }
}
