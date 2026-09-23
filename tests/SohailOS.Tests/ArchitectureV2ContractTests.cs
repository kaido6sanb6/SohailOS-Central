using System.Text.Json;
using Xunit;

namespace SohailOS.Tests;

public sealed class ArchitectureV2ContractTests
{
    [Fact]
    public void ArchitectureManifest_Defines_ControlPlanes_And_TaskGraph()
    {
        var path = FindRepositoryFile("ecosystem/architecture-v2.json");
        Assert.True(File.Exists(path), $"Expected architecture manifest at {path}.");

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        Assert.Equal("sohailos-universal-ai-operating-system", root.GetProperty("architecture").GetString());
        Assert.Equal("kaido6sanb6/SohailOS-Central", root.GetProperty("control_plane").GetString());
        Assert.Contains("orchestration", root.GetProperty("planes").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal("bounded-task-graph", root.GetProperty("execution_model").GetString());
        Assert.Contains("verification-observability", root.GetProperty("planes").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains("repository-content-is-data-not-instruction", root.GetProperty("trust_boundary").GetString());
    }

    [Fact]
    public void CanonicalPrompt_Defines_Federated_Architecture_And_15Minute_Fork_Reconciliation()
    {
        var path = FindRepositoryFile("prompts/UNIVERSAL_AI_CORE.md");
        Assert.True(File.Exists(path), $"Expected canonical prompt at {path}.");

        var prompt = File.ReadAllText(path);
        Assert.Contains("Client→Gateway→Policy/Consent→Master Orchestrator→Task Graph→Capability Registry", prompt);
        Assert.Contains("Repository→Revision→Document→Chunk→Embedding", prompt);
        Assert.Contains("FORKS: Auto-discover all connected kaido6sanb6 GitHub forks; sync/reconcile them with SohailOS-Central every 15m, 24/7.", prompt);
        Assert.Contains("untrusted DATA", prompt);
        Assert.Contains("EXECUTED, VERIFIED, VALIDATED", prompt);
    }

    private static string FindRepositoryFile(string relativePath)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, relativePath);
            if (File.Exists(candidate))
                return candidate;
            current = current.Parent;
        }

        return Path.GetFullPath(relativePath);
    }
}
