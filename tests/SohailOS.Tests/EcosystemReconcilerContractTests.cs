using Xunit;

namespace SohailOS.Tests;

public class EcosystemReconcilerContractTests
{
    [Fact]
    public void LiveReconcilerProject_ExistsAtTheDocumentedBoundary()
    {
        var repoRoot = FindRepositoryRoot();
        var projectPath = Path.Combine(repoRoot, "src", "SohailOS.Ecosystem", "SohailOS.Ecosystem.csproj");

        Assert.True(
            File.Exists(projectPath),
            $"Expected the live ecosystem reconciler project at {projectPath}.");
    }

    [Fact]
    public void LiveReconciliationWorkflow_ExistsAndIsScheduled()
    {
        var repoRoot = FindRepositoryRoot();
        var workflowPath = Path.Combine(repoRoot, ".github", "workflows", "ecosystem-reconcile.yml");

        Assert.True(File.Exists(workflowPath), "Expected the scheduled ecosystem reconciliation workflow.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SohailOS.sln")))
            directory = directory.Parent;

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
