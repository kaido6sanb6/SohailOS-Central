using System.Text.Json;
using Xunit;

namespace SohailOS.Tests;

public class EcosystemRegistryDocumentTests
{
    private static string ManifestPath =>
        Path.Combine(AppContext.BaseDirectory, "ecosystem", "ecosystem.json");

    [Fact]
    public void Manifest_DeclaresExpectedTopLevelContract()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ManifestPath));
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("schema_version", out _));
        Assert.True(root.TryGetProperty("control_plane", out _));
        Assert.True(root.TryGetProperty("repositories", out var repositories));
        Assert.True(root.TryGetProperty("edges", out var edges));
        Assert.True(root.TryGetProperty("routing", out var routing));

        Assert.Equal(46, repositories.GetArrayLength());
        Assert.NotEqual(0, edges.GetArrayLength());
        Assert.NotEqual(0, routing.GetArrayLength());
        Assert.Equal("kaido6sanb6/SohailOS-Central", root.GetProperty("control_plane").GetString());
    }

    [Fact]
    public void Manifest_HasUniqueRepositories_AndIntegritySafeEdgesAndRoutes()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ManifestPath));
        var root = document.RootElement;

        var ids = root.GetProperty("repositories")
            .EnumerateArray()
            .Select(x => x.GetProperty("repo").GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(46, ids.Count);
        Assert.Contains("kaido6sanb6/SohailOS-Central", ids);

        foreach (var edge in root.GetProperty("edges").EnumerateArray())
        {
            Assert.Contains(edge.GetProperty("from").GetString()!, ids);
            Assert.Contains(edge.GetProperty("to").GetString()!, ids);
        }

        foreach (var route in root.GetProperty("routing").EnumerateArray())
        {
            foreach (var repo in route.GetProperty("repositories").EnumerateArray())
                Assert.Contains(repo.GetString()!, ids);
        }

        foreach (var repository in root.GetProperty("repositories").EnumerateArray())
        {
            var autoRoute = repository.GetProperty("auto_route").GetBoolean();
            var verification = repository.GetProperty("verification_status").GetString();
            if (autoRoute)
                Assert.NotEqual("unverified", verification);
        }
    }

    [Fact]
    public void Manifest_QuarantinesNonApplicationRepositories()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ManifestPath));
        var repositories = document.GetProperty("repositories")
            .EnumerateArray()
            .ToDictionary(
                x => x.GetProperty("repo").GetString()!,
                StringComparer.OrdinalIgnoreCase);

        foreach (var name in new[]
        {
            "kaido6sanb6/dddeu83",
            "kaido6sanb6/argo-pass",
            "kaido6sanb6/V2ray-for-Doprax"
        })
        {
            Assert.False(repositories[name].GetProperty("auto_route").GetBoolean());
        }
    }

    [Fact]
    public void RegistryReadme_IsDocumentedSeparatelyFromTheMachineManifest()
    {
        var readmePath = Path.Combine(
            Directory.GetParent(AppContext.BaseDirectory)!.Parent!.Parent!.Parent!.FullName,
            "ecosystem",
            "README.md");

        Assert.True(File.Exists(readmePath));
        Assert.Contains("ecosystem/ecosystem.json", File.ReadAllText(readmePath), StringComparison.Ordinal);
    }
}
