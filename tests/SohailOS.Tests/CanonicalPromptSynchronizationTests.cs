using Xunit;

namespace SohailOS.Tests;

public sealed class CanonicalPromptSynchronizationTests
{
    [Fact]
    public void CanonicalPrompt_File_Matches_DotNet_Runtime_Artifact()
    {
        var root = FindRepositoryRoot();
        var canonical = File.ReadAllText(Path.Combine(root, "prompts", "SohailOS-SuperPrompt.xlm")).Trim();
        Assert.Equal(SohailOS.Core.CanonicalPrompt.Compact, canonical);
        var expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        Assert.Equal(SohailOS.Core.CanonicalPrompt.Sha256, expectedHash);
    }

    [Fact]
    public void CanonicalPrompt_File_Matches_Cloudflare_Runtime_Artifact()
    {
        var root = FindRepositoryRoot();
        var canonical = File.ReadAllText(Path.Combine(root, "prompts", "SohailOS-SuperPrompt.xlm")).Trim();
        var source = File.ReadAllText(Path.Combine(root, "cloudflare", "sohailos-gateway", "src", "canonical-prompt.ts"));

        var prefix = "export const CANONICAL_SUPERPROMPT = ";
        var start = source.IndexOf(prefix, StringComparison.Ordinal);
        Assert.True(start >= 0);
        start += prefix.Length;
        var suffix = "\" as const;";
        var end = source.IndexOf(suffix, start, StringComparison.Ordinal);
        Assert.True(end > start);
        var literal = source[start..(end + 1)];

        var decoded = System.Text.Json.JsonSerializer.Deserialize<string>(literal);
        Assert.Equal(canonical, decoded);
    }

    [Fact]
    public void RuntimeEntrypoints_Reference_The_Canonical_Prompt()
    {
        var root = FindRepositoryRoot();
        var gateway = File.ReadAllText(Path.Combine(root, "src", "SohailOS.Gateway", "Program.cs"));
        var app = File.ReadAllText(Path.Combine(root, "src", "SohailOS.App", "MainWindow.xaml.cs"));
        var worker = File.ReadAllText(Path.Combine(root, "cloudflare", "sohailos-gateway", "src", "index.ts"));

        Assert.Contains("CanonicalPrompt.Compact", gateway, StringComparison.Ordinal);
        Assert.Contains("CanonicalPrompt.Compact", app, StringComparison.Ordinal);
        Assert.Contains("CANONICAL_SUPERPROMPT", worker, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "SohailOS.sln")))
            current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
