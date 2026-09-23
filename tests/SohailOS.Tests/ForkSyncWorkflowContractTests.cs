using Xunit;

namespace SohailOS.Tests;

public sealed class ForkSyncWorkflowContractTests
{
    [Fact]
    public void ForkSyncWorkflow_RunsEvery15Minutes_AndUsesNonDestructiveConcurrency()
    {
        var path = FindRepositoryFile(".github/workflows/fork-sync.yml");
        Assert.True(File.Exists(path));

        var yaml = File.ReadAllText(path);

        Assert.Contains("*/15 * * * *", yaml);
        Assert.Contains("workflow_dispatch", yaml);
        Assert.Contains("cancel-in-progress: false", yaml);
        Assert.Contains("merge-upstream", yaml);
        Assert.DoesNotContain("--force", yaml);
        Assert.DoesNotContain("git push --force", yaml);
    }

    [Fact]
    public void ForkSyncWorkflow_RequiresExplicitCrossRepositoryCredential()
    {
        var path = FindRepositoryFile(".github/workflows/fork-sync.yml");
        var yaml = File.ReadAllText(path);

        Assert.Contains("SOHAILOS_GITHUB_SYNC_TOKEN", yaml);
        Assert.Contains("GITHUB_TOKEN", yaml);
    }

    [Fact]
    public void ForkInventoryScript_PreservesUpstreamAndSourceMetadata()
    {
        var path = FindRepositoryFile("scripts/build_fork_inventory.py");
        var source = File.ReadAllText(path);

        Assert.Contains(""upstream"", source);
        Assert.Contains(""source"", source);
        Assert.Contains(""fork_count"", source);
        Assert.Contains(""fork"", source);
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
