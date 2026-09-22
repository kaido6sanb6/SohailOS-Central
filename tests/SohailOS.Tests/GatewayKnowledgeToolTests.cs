using SohailOS.Core;
using SohailOS.Ecosystem;
using SohailOS.Gateway;
using Xunit;

namespace SohailOS.Tests;

public sealed class GatewayKnowledgeToolTests
{
    [Fact]
    public void KnowledgeToolRegistration_ExposesExactlyThreeReadOnlyTools()
    {
        var registry = new ToolRegistry();
        var retrieval = new KnowledgeRetrievalService(new InMemoryDataPlaneProvider());

        EcosystemKnowledgeTools.Register(registry, retrieval);

        var definitions = registry.Definitions
            .Where(x => x.Name.StartsWith("ecosystem.", StringComparison.Ordinal))
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            ["ecosystem.get_document", "ecosystem.get_source", "ecosystem.search"],
            definitions.Select(x => x.Name).ToArray());

        Assert.All(definitions, x => Assert.Equal(ToolPermission.ReadOnly, x.Permission));
    }

    [Fact]
    public void KnowledgeToolAllowlist_ContainsNoWriteOperation()
    {
        Assert.Equal(
            ["ecosystem.get_document", "ecosystem.get_source", "ecosystem.search"],
            EcosystemKnowledgeTools.AllowedToolNames.OrderBy(x => x, StringComparer.Ordinal));
    }
}
