using System.Net;
using System.Net.Http;
using SohailOS.Integrations;
using Xunit;

namespace SohailOS.Tests;

public sealed class WebFetchEgressSecurityTests
{
    [Theory]
    [InlineData("https://127.0.0.1/")]
    [InlineData("https://localhost/")]
    [InlineData("https://[::1]/")]
    [InlineData("https://169.254.169.254/")]
    public async Task WebFetchTool_Blocks_PrivateOrLoopbackTargets(string url)
    {
        var tool = new WebFetchTool(new HttpClient());
        var result = await tool.ExecuteAsync(
            new Dictionary<string, object?> { ["url"] = url });

        Assert.False(result.Success);
        Assert.Contains("egress", result.Content, StringComparison.OrdinalIgnoreCase);
    }
}
