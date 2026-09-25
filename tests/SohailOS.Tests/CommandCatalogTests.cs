using SohailOS.Core;
using Xunit;

namespace SohailOS.Tests;

public sealed class CommandCatalogTests
{
    [Fact]
    public void Catalog_HasUniqueCanonicalTriggers()
    {
        var catalog = CommandCatalog.CreateDefault();

        Assert.Equal(catalog.Count, catalog.Select(x => x.Trigger).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(catalog, x => x.Trigger == "/plan");
        Assert.Contains(catalog, x => x.Trigger == "/verify");
    }

    [Fact]
    public void MutationCommands_AreGoverned()
    {
        var catalog = CommandCatalog.CreateDefault();

        Assert.True(catalog.Single(x => x.Trigger == "/merge-main").RequiresGovernance);
        Assert.True(catalog.Single(x => x.Trigger == "/deploy").RequiresGovernance);
        Assert.True(catalog.Single(x => x.Trigger == "/commit").RequiresGovernance);
    }

    [Fact]
    public void ReadAndAnalysisCommands_DoNotGrantMutation()
    {
        var catalog = CommandCatalog.CreateDefault();

        Assert.False(catalog.Single(x => x.Trigger == "/audit").RequiresGovernance);
        Assert.False(catalog.Single(x => x.Trigger == "/research").RequiresGovernance);
        Assert.False(catalog.Single(x => x.Trigger == "/status").RequiresGovernance);
    }

    [Fact]
    public void UnknownCommand_IsExplicitlyUnknown()
    {
        var result = CommandCatalog.Resolve("/does-not-exist");

        Assert.False(result.Found);
        Assert.Equal("/does-not-exist", result.Trigger);
    }

    [Fact]
    public void ApproveCommand_DoesNotCreateAuthorizationBinding()
    {
        var command = CommandCatalog.Resolve("/approve");

        Assert.True(command.Found);
        Assert.True(command.IsApprovalIntent);
        Assert.False(command.GrantsAuthorization);
    }
}
