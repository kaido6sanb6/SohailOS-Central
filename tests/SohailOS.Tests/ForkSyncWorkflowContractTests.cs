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
        Assert.Contains("actions/setup-dotnet@v4", yaml);
        Assert.Contains("--fail-on-removal", yaml);
        Assert.DoesNotContain("--force", yaml);
        Assert.DoesNotContain("git push --force", yaml);
    }

    [Fact]
    public void ForkSyncWorkflow_RequiresExplicitCrossRepositoryCredential()
    {
        var path = FindRepositoryFile(".github/workflows/fork-sync.yml");
        var yaml = File.ReadAllText(path);

        Assert.Contains("actions/create-github-app-token@v3", yaml);
        Assert.Contains("SOHAILOS_GITHUB_APP_ID", yaml);
        Assert.Contains("SOHAILOS_GITHUB_APP_PRIVATE_KEY", yaml);
        Assert.Contains("steps.app-token.outputs.token", yaml);
        Assert.Contains("owner: ${{ github.repository_owner }}", yaml);
        Assert.Contains("permission-contents: write", yaml);
        Assert.Contains("permission-workflows: write", yaml);
        Assert.DoesNotContain("SOHAILOS_GITHUB_SYNC_TOKEN", yaml);
        Assert.Contains("id: sync-gate", yaml);
        Assert.Contains("if: always()", yaml);
        Assert.Contains("steps.reconcile.outcome", yaml);
        Assert.Contains("if: steps.sync-gate.outcome == 'success'", yaml);
    }

    [Fact]
    public void ForkSyncWorkflow_NormalizesAndValidatesPrivateKeyBeforeTokenGeneration()
    {
        var path = FindRepositoryFile(".github/workflows/fork-sync.yml");
        var yaml = File.ReadAllText(path);

        Assert.Contains("Validate GitHub App private key", yaml);
        Assert.Contains("RAW_PRIVATE_KEY", yaml);
        Assert.Contains("base64 --decode", yaml);
        Assert.Contains("openssl pkey -in", yaml);
        Assert.Contains("private-key: ${{ secrets.SOHAILOS_GITHUB_APP_PRIVATE_KEY }}", yaml);
        Assert.Contains("app-id: ${{ vars.SOHAILOS_GITHUB_APP_ID }}", yaml);
        Assert.DoesNotContain("client-id: ${{ vars.SOHAILOS_GITHUB_APP_ID }}", yaml);
    }

    [Fact]
    public void ForkInventoryScript_PreservesUpstreamAndSourceMetadata()
    {
        var path = FindRepositoryFile("scripts/build_fork_inventory.py");
        var source = File.ReadAllText(path);

        Assert.Contains("\"upstream\"", source);
        Assert.Contains("\"source\"", source);
        Assert.Contains("\"fork_count\"", source);
        Assert.Contains("\"fork\"", source);

        var syncScript = FindRepositoryFile("scripts/sync_forks.py");
        Assert.True(File.Exists(syncScript));
        var sync = File.ReadAllText(syncScript);
        Assert.Contains("merge-upstream", sync);
        Assert.Contains("GH_TOKEN", sync);
        Assert.DoesNotContain("--force", sync);
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
