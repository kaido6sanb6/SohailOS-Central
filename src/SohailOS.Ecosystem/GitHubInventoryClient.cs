using System.Net.Http.Headers;
using System.Text.Json;

namespace SohailOS.Ecosystem;

public sealed class GitHubInventoryClient
{
    private readonly HttpClient _httpClient;

    public GitHubInventoryClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("SohailOS-Ecosystem-Reconciler", "1.0"));
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public async Task<IReadOnlyList<LiveRepository>> GetPublicOwnerRepositoriesAsync(
        string owner,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(owner))
            throw new ArgumentException("GitHub owner is required.", nameof(owner));

        var repositories = new List<LiveRepository>();
        var page = 1;

        while (true)
        {
            var url = $"https://api.github.com/users/{Uri.EscapeDataString(owner)}/repos?per_page=100&page={page}&type=owner";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);

            var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException(
                    $"GitHub repository inventory request failed with {(int)response.StatusCode}: {body}");

            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("GitHub repository inventory response was not an array.");

            var countThisPage = 0;

            foreach (var item in document.RootElement.EnumerateArray())
            {
                countThisPage++;

                var fullName = item.TryGetProperty("full_name", out var fullNameElement)
                    ? fullNameElement.GetString()
                    : null;
                var name = item.TryGetProperty("name", out var nameElement)
                    ? nameElement.GetString()
                    : null;
                var defaultBranch = item.TryGetProperty("default_branch", out var branchElement)
                    ? branchElement.GetString()
                    : null;

                if (string.IsNullOrWhiteSpace(fullName) ||
                    string.IsNullOrWhiteSpace(name) ||
                    string.IsNullOrWhiteSpace(defaultBranch))
                {
                    throw new InvalidDataException(
                        "GitHub repository inventory contained a repository missing full_name, name, or default_branch.");
                }

                var id = item.TryGetProperty("id", out var idElement) && idElement.TryGetInt64(out var parsedId)
                    ? parsedId
                    : (long?)null;

                var htmlUrl = item.TryGetProperty("html_url", out var htmlElement)
                    ? htmlElement.GetString()
                    : null;

                var isPrivate = item.TryGetProperty("private", out var privateElement) &&
                                 privateElement.ValueKind == JsonValueKind.True;

                var isFork = item.TryGetProperty("fork", out var forkElement) &&
                             forkElement.ValueKind == JsonValueKind.True;

                repositories.Add(new LiveRepository(
                    id, name, fullName, defaultBranch, htmlUrl, isPrivate, isFork));
            }

            if (countThisPage < 100)
                break;

            page++;
        }

        return repositories;
    }
}
