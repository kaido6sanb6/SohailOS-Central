using SohailOS.Ecosystem;

namespace SohailOS.Tests;

public sealed class KnowledgeSearchExportTests
{
    [Fact]
    public async Task Export_Writes_Provenance_And_Bounds_Files()
    {
        var provider = new InMemoryDataPlaneProvider();
        const long repoNumericId = 12345;
        const string repoId = "gh:repo:12345";
        const string gitOid = "0123456789abcdef0123456789abcdef01234567";
        const string path = "src/example.cs";

        await provider.UpsertRepositoriesAsync(
        [
            new RepositoryIdentity(
                repoId,
                repoNumericId,
                "kaido6sanb6/example",
                "main",
                false,
                KnowledgeTrustTier.Verified,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow)
        ]);

        var revisionId = KnowledgeIdentity.RevisionId(gitOid, path);
        await provider.UpsertRevisionsAsync(
        [
            new SourceRevision(
                revisionId,
                repoId,
                gitOid,
                path,
                "blobsha",
                "contenthash",
                20,
                DateTimeOffset.UtcNow,
                KnowledgeIndexState.Committed)
        ]);

        var docId = KnowledgeIdentity.DocumentId(repoNumericId, path);
        await provider.UpsertDocumentsAsync(
        [
            new DocumentRecord(
                docId,
                repoId,
                path,
                revisionId,
                "csharp",
                false,
                false,
                null)
        ]);

        await provider.UpsertChunksAsync(
        [
            new ChunkRecord(
                KnowledgeIdentity.ChunkId("hello world", 0),
                docId,
                revisionId,
                0,
                0,
                11,
                "contenthash",
                "hello world",
                "csharp")
        ]);

        var output = Path.Combine(Path.GetTempPath(), $"sohailos-kf-{Guid.NewGuid():N}");
        try
        {
            var result = await KnowledgeSearchExportService.ExportAsync(provider, output, 1024);

            Assert.Single(result.Files);
            var file = await File.ReadAllTextAsync(result.Files[0]);
            Assert.Contains("repo_id: gh:repo:12345", file);
            Assert.Contains("git_oid: 0123456789abcdef0123456789abcdef01234567", file);
            Assert.Contains("path: src/example.cs", file);
            Assert.Contains("hello world", file);
            Assert.True(new FileInfo(result.Files[0]).Length <= 1024);
        }
        finally
        {
            if (Directory.Exists(output))
                Directory.Delete(output, recursive: true);
        }
    }
}
