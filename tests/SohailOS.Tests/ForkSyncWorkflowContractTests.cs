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
        Assert.Contains("scripts/sync_forks.py", yaml);
        Assert.Contains("scripts/fork_ecosystem.py", yaml);
        Assert.Contains("scripts/apply_fork_plan.py", yaml);
        Assert.Contains("actions/upload-artifact@v4", yaml);
        Assert.DoesNotContain("--force", yaml);
        Assert.DoesNotContain("git push --force", yaml);
    }

    [Fact]
    public void ForkSyncWorkflow_UsesLeastPrivilegeRuntimeCredential()
    {
        var yaml = File.ReadAllText(FindRepositoryFile(".github/workflows/fork-sync.yml"));
        Assert.Contains("actions/create-github-app-token@v3", yaml);
        Assert.Contains("SOHAILOS_GITHUB_APP_ID", yaml);
        Assert.Contains("SOHAILOS_GITHUB_APP_PRIVATE_KEY", yaml);
        Assert.Contains("permission-contents: write", yaml);
        Assert.DoesNotContain("permission-workflows: write", yaml);
        Assert.DoesNotContain("SOHAILOS_GITHUB_SYNC_TOKEN", yaml);
    }

    [Fact]
    public void ForkInventoryAndPlanArePolicyBound()
    {
        var inventory = File.ReadAllText(FindRepositoryFile("scripts/build_fork_inventory.py"));
        var sync = File.ReadAllText(FindRepositoryFile("scripts/sync_forks.py"));
        var plan = File.ReadAllText(FindRepositoryFile("scripts/fork_ecosystem.py"));
        var policy = File.ReadAllText(FindRepositoryFile("ecosystem/policies/fork-propagation.json"));
        Assert.Contains("parent", inventory);
        Assert.Contains("source", inventory);
        Assert.Contains("behind", sync);
        Assert.Contains("diverged", sync);
        Assert.Contains("operation_id", plan);
        Assert.Contains("mutation", plan);
        Assert.Contains("never_literal_merge_all_forks", policy);
        Assert.Contains("irreversible_operations", policy);
    }

    private static string FindRepositoryFile(string relativePath)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, relativePath);
            if (File.Exists(candidate)) return candidate;
            current = current.Parent;
        }
        return Path.GetFullPath(relativePath);
    }
}
