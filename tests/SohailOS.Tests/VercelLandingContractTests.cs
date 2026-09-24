using System;
using System.IO;
using Xunit;

namespace SohailOS.Tests;

public sealed class VercelLandingContractTests
{
    private static string ReadLandingPage()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "index.html");
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Vercel root landing page index.html was not found.");
    }

    [Fact]
    public void Root_landing_page_contains_architecture_contract()
    {
        var html = ReadLandingPage();

        foreach (var marker in new[]
        {
            "SohailOS-Central", "Architecture", "Route", "Discover", "Probe",
            "Plan", "Preview", "Approve", "Exec", "Verify", "Validate",
            "Deliver", "mermaid"
        })
        {
            Assert.Contains(marker, html, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Landing_page_exposes_code_and_visual_views()
    {
        var html = ReadLandingPage();

        Assert.Contains("Architecture source", html, StringComparison.Ordinal);
        Assert.Contains("Architecture chart", html, StringComparison.Ordinal);
        Assert.Contains("id=\"mermaid\"", html, StringComparison.Ordinal);
        Assert.Contains("<svg", html, StringComparison.Ordinal);
    }
}
