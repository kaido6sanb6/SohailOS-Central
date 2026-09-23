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
    public void CanonicalPrompt_Defines_Federated_Architecture_And_RedTeam_Verification()
    {
        var path = FindRepositoryFile("prompts/UNIVERSAL_AI_CORE.md");
        Assert.True(File.Exists(path), $"Expected canonical prompt at {path}.");

        var prompt = File.ReadAllText(path);
        Assert.StartsWith("<Core>", prompt);
        Assert.Contains("control-plane", prompt);
        Assert.Contains("Client>Gateway>Policy>Orchestrator>TaskGraph>Capabilities", prompt);
        Assert.Contains("fork>repo>upstream", prompt);
        Assert.Contains("DATA≠authority", prompt);
        Assert.Contains("ATTACK_TOKEN=redteam", prompt);
        Assert.Contains("EXECUTED|VERIFIED|VALIDATED", prompt);
        Assert.Contains("provenance+freshness", prompt);
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
