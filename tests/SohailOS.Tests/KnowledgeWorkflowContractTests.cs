using System.Text.Json;
using Xunit;

namespace SohailOS.Tests;

public sealed class KnowledgeWorkflowContractTests
{
    [Fact]
    public void ContinuousIndexWorkflow_UsesReadOnlyGitHubPermissions_AndPublishesArtifacts()
    {
        var path = FindRepositoryFile(".github/workflows/ecosystem-index.yml");
        Assert.True(File.Exists(path), $"Expected indexing workflow at {path}.");

        var yaml = File.ReadAllText(path);
        Assert.Contains("workflow_dispatch", yaml);
        Assert.Contains("schedule:", yaml);
        Assert.Contains("permissions:", yaml);
        Assert.Contains("contents: read", yaml);
        Assert.DoesNotContain("contents: write", yaml);
        Assert.Contains("continue-on-error: true", yaml);
        Assert.Contains("actions/upload-artifact@v4", yaml);
        Assert.DoesNotContain("git push", yaml);
        Assert.DoesNotContain("create_pull_request", yaml);
    }


    [Fact]
    public void EcosystemReconcileWorkflow_AutoCommitsInventoryChanges_AndDispatchesKnowledgeSync()
    {
        var path = FindRepositoryFile(".github/workflows/ecosystem-reconcile.yml");
        Assert.True(File.Exists(path), $"Expected reconciliation workflow at {path}.\");

        var yaml = File.ReadAllText(path);
        Assert.Contains("schedule:", yaml);
        Assert.Contains("*/15 * * * *", yaml);
        Assert.Contains("contents: write", yaml);
        Assert.Contains("git push origin", yaml);
        Assert.Contains("repository_dispatch", yaml);
        Assert.Contains("forks_reconciled", yaml);
        Assert.DoesNotContain("gh pr create", yaml);
    }

    [Fact]
    public void ManagedSearchWorkflow_AcceptsForkReconciliationDispatch()
    {
        var path = FindRepositoryFile(".github/workflows/ecosystem-knowledge-search.yml");
        Assert.True(File.Exists(path), $"Expected managed search workflow at {path}.\");

        var yaml = File.ReadAllText(path);
        Assert.Contains("repository_dispatch:", yaml);
        Assert.Contains("forks_reconciled", yaml);
    }

    [Fact]
    public void ManagedSearchWorkflow_Uses_ReadOnlyGitHub_And_AI_SearchSync()
    {
        var path = FindRepositoryFile(".github/workflows/ecosystem-knowledge-search.yml");
        Assert.True(File.Exists(path), $"Expected managed search workflow at {path}.");

        var yaml = File.ReadAllText(path);
        Assert.Contains("workflow_dispatch", yaml);
        Assert.Contains("schedule:", yaml);
        Assert.Contains("contents: read", yaml);
        Assert.DoesNotContain("contents: write", yaml);
        Assert.Contains("--export-search", yaml);
        Assert.Contains("sync-knowledge-ai-search.mjs", yaml);
        Assert.Contains("CLOUDFLARE_API_TOKEN", yaml);
        Assert.Contains("CLOUDFLARE_ACCOUNT_ID", yaml);
    }

    [Fact]
    public void GoldenEvaluationCorpus_IsVersionedAndHasUniqueCaseIds()
    {
        var path = FindRepositoryFile("ecosystem/evaluation/golden.json");
        Assert.True(File.Exists(path), $"Expected evaluation corpus at {path}.");

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        Assert.Equal("1", root.GetProperty("schema_version").GetString());

        var cases = root.GetProperty("cases").EnumerateArray().ToArray();
        Assert.NotEmpty(cases);

        var ids = cases.Select(x => x.GetProperty("id").GetString()!)
            .ToArray();

        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.All(cases, x =>
        {
            Assert.False(string.IsNullOrWhiteSpace(x.GetProperty("query").GetString()));
            Assert.NotEmpty(x.GetProperty("expected_paths").EnumerateArray());
        });
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
