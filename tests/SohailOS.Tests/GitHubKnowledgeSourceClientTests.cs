using System.Net;
using System.Text;
using SohailOS.Ecosystem;
using Xunit;

namespace SohailOS.Tests;

public sealed class GitHubKnowledgeSourceClientTests
{
    [Fact]
    public async Task GetBlobAsync_reads_public_blob_from_raw_github_without_api_blob_endpoint()
    {
        var handler = new RecordingHandler();
        var client = new GitHubKnowledgeSourceClient(new HttpClient(handler));
        var repository = new LiveRepository(
            123,
            "sample",
            "octocat/sample",
            "main",
            "https://github.com/octocat/sample",
            false,
            false);

        var bytes = await client.GetBlobAsync(repository, "commit123", "docs/readme.md");

        Assert.Equal("https://raw.githubusercontent.com/octocat/sample/commit123/docs/readme.md", handler.RequestUri!.ToString());
        Assert.Equal("hello", Encoding.UTF8.GetString(bytes));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Encoding.UTF8.GetBytes("hello"))
            });
        }
    }
}
