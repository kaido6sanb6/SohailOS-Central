using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace SohailOS.Tests;

public class EcosystemReconcilerEndToEndTests
{
    [Fact]
    public void ReconcileFixture_RegistersNewRepositoryWithoutEnablingAutoRoute()
    {
        var repoRoot = FindRepositoryRoot();
        var projectPath = Path.Combine(repoRoot, "src", "SohailOS.Ecosystem", "SohailOS.Ecosystem.csproj");
        var tempRoot = Path.Combine(Path.GetTempPath(), "sohailos-ecosystem-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        var manifestPath = Path.Combine(tempRoot, "ecosystem.json");
        var inventoryPath = Path.Combine(tempRoot, "inventory.json");
        var reportPath = Path.Combine(tempRoot, "reconciliation.json");

        try
        {
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(new
            {
                schema_version = "1.0",
                control_plane = "kaido6sanb6/SohailOS-Central",
                repositories = new[]
                {
                    new
                    {
                        repo = "kaido6sanb6/SohailOS-Central",
                        default_branch = "main",
                        role = "control-plane",
                        capabilities = new[] { "orchestration" },
                        verification_status = "verified",
                        auto_route = true,
                        evidence = new[] { "https://github.com/kaido6sanb6/SohailOS-Central" },
                        notes = "preserve"
                    }
                },
                edges = Array.Empty<object>(),
                routing = Array.Empty<object>()
            }, new JsonSerializerOptions { WriteIndented = true }));

            File.WriteAllText(inventoryPath, JsonSerializer.Serialize(new[]
            {
                new
                {
                    id = 1,
                    name = "SohailOS-Central",
                    full_name = "kaido6sanb6/SohailOS-Central",
                    default_branch = "main",
                    html_url = "https://github.com/kaido6sanb6/SohailOS-Central"
                },
                new
                {
                    id = 2,
                    name = "glowing-rotary-phone",
                    full_name = "kaido6sanb6/glowing-rotary-phone",
                    default_branch = "main",
                    html_url = "https://github.com/kaido6sanb6/glowing-rotary-phone"
                }
            }, new JsonSerializerOptions { WriteIndented = true }));

            var result = RunDotnet(repoRoot, projectPath,
                $"--owner kaido6sanb6 --manifest \"{manifestPath}\" --inventory \"{inventoryPath}\" --report \"{reportPath}\" --write");

            Assert.True(
                result.ExitCode == 0,
                $"Expected reconciler success. stdout: {result.StdOut}{Environment.NewLine}stderr: {result.StdErr}");

            using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var repositories = manifest.RootElement.GetProperty("repositories");

            Assert.Equal(2, repositories.GetArrayLength());

            var added = repositories.EnumerateArray()
                .Single(x => x.GetProperty("repo").GetString() == "kaido6sanb6/glowing-rotary-phone");

            Assert.Equal("unclassified", added.GetProperty("role").GetString());
            Assert.Equal("unverified", added.GetProperty("verification_status").GetString());
            Assert.False(added.GetProperty("auto_route").GetBoolean());

            var existing = repositories.EnumerateArray()
                .Single(x => x.GetProperty("repo").GetString() == "kaido6sanb6/SohailOS-Central");

            Assert.Equal("control-plane", existing.GetProperty("role").GetString());
            Assert.Equal("orchestration", existing.GetProperty("capabilities")[0].GetString());

            using var report = JsonDocument.Parse(File.ReadAllText(reportPath));
            Assert.Equal(1, report.RootElement.GetProperty("added").GetArrayLength());
            Assert.Equal("kaido6sanb6/glowing-rotary-phone",
                report.RootElement.GetProperty("added")[0].GetString());
            Assert.Equal(1, report.RootElement.GetProperty("registry_repository_count_before").GetInt32());
            Assert.Equal(2, report.RootElement.GetProperty("registry_repository_count_after").GetInt32());
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }


    [Fact]
    public void ReconcileWithFailOnRemoval_ReportsRemovalAndDoesNotWrite()
    {
        var repoRoot = FindRepositoryRoot();
        var projectPath = Path.Combine(repoRoot, "src", "SohailOS.Ecosystem", "SohailOS.Ecosystem.csproj");
        var tempRoot = Path.Combine(Path.GetTempPath(), "sohailos-ecosystem-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        var manifestPath = Path.Combine(tempRoot, "ecosystem.json");
        var inventoryPath = Path.Combine(tempRoot, "inventory.json");
        var reportPath = Path.Combine(tempRoot, "reconciliation.json");

        try
        {
            var original = JsonSerializer.Serialize(new
            {
                schema_version = "1.0",
                control_plane = "kaido6sanb6/SohailOS-Central",
                repositories = new[]
                {
                    new
                    {
                        repo = "kaido6sanb6/retired-repository",
                        default_branch = "main",
                        role = "reference",
                        capabilities = new[] { "reference" },
                        verification_status = "verified",
                        auto_route = false,
                        evidence = new[] { "https://github.com/kaido6sanb6/retired-repository" },
                        notes = "must remain for human review"
                    }
                },
                edges = Array.Empty<object>(),
                routing = Array.Empty<object>()
            }, new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(manifestPath, original);

            File.WriteAllText(
                inventoryPath,
                JsonSerializer.Serialize(new[]
                {
                    new
                    {
                        id = 2,
                        name = "glowing-rotary-phone",
                        full_name = "kaido6sanb6/glowing-rotary-phone",
                        default_branch = "main",
                        html_url = "https://github.com/kaido6sanb6/glowing-rotary-phone"
                    }
                }, new JsonSerializerOptions { WriteIndented = true }));

            var result = RunDotnet(
                repoRoot,
                projectPath,
                $"--owner kaido6sanb6 --manifest \"{manifestPath}\" --inventory \"{inventoryPath}\" --report \"{reportPath}\" --write --fail-on-removal");

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("removal", result.StdOut, StringComparison.OrdinalIgnoreCase);

            Assert.Equal(original, File.ReadAllText(manifestPath));

            using var report = JsonDocument.Parse(File.ReadAllText(reportPath));
            Assert.Equal(1, report.RootElement.GetProperty("removed").GetArrayLength());
            Assert.False(report.RootElement.GetProperty("write_applied").GetBoolean());
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static (int ExitCode, string StdOut, string StdErr) RunDotnet(
        string workingDirectory,
        string projectPath,
        string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = workingDirectory,
            Arguments = $"run --project \"{projectPath}\" -- {arguments}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start dotnet.");

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, stdout, stderr);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SohailOS.sln")))
            directory = directory.Parent;

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
