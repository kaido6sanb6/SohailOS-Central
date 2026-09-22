using Xunit;

namespace SohailOS.Tests;

public sealed class GatewayKnowledgeToolTests
{
    [Fact]
    public void KnowledgeToolRegistration_Exists()
    {
        var type = Type.GetType(
            "SohailOS.Gateway.EcosystemKnowledgeTools, SohailOS.Gateway",
            throwOnError: false);

        Assert.NotNull(type);
    }
}
