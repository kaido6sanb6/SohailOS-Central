using Xunit;

namespace SohailOS.Tests;

public sealed class KnowledgePersistenceTests
{
    [Fact]
    public async Task Buffered_json_provider_flushes_once_after_batch_updates()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sohailos-test-{Guid.NewGuid():N}.json");

        try
        {
            var provider = new SohailOS.Ecosystem.JsonFileDataPlaneProvider(path, autoSave: false);

            await provider.UpsertRepositoriesAsync(
            [
                new SohailOS.Ecosystem.RepositoryIdentity(
                    "repo:1",
                    1,
                    "owner/repo",
                    "main",
                    false,
                    SohailOS.Ecosystem.KnowledgeTrustTier.Unverified,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow)
            ]);

            Assert.False(File.Exists(path));

            await provider.FlushAsync();

            Assert.True(File.Exists(path));
            Assert.Contains("owner/repo", await File.ReadAllTextAsync(path));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
