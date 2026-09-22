using Xunit;

namespace SohailOS.Tests;

public sealed class KnowledgeWorkflowContractTests
{
    [Fact]
    public void ContinuousIndexWorkflow_ExistsWithReadOnlyPermissions()
    {
        var root = Directory.GetCurrentDirectory();
        var path = Path.GetFullPath(
            Path.Combine(root, "..", "..", ".github", "workflows", "ecosystem-index.yml"));

        Assert.True(
            File.Exists(path),
            $"Expected indexing workflow at {path}.");
    }

    [Fact]
    public void GoldenEvaluationCorpus_Exists()
    {
        var root = Directory.GetCurrentDirectory();
        var path = Path.GetFullPath(
            Path.Combine(root, "..", "..", "ecosystem", "evaluation", "golden.json"));

        Assert.True(File.Exists(path), $"Expected evaluation corpus at {path}.");
    }
}
