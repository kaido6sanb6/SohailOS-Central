using System.Net.Http.Headers;
using System.Text.Json;

namespace SohailOS.Ecosystem;

public sealed record GitHubRepositorySnapshot(
    long Id,
    string FullName,
    string DefaultBranch,
    string GitOid,
    bool IsPrivate);

public sealed record GitHubTreeEntry(
    string Path,
    string Sha,
    long Size,
    string Type);

public sealed class GitHubKnowledgeSourceClient
{
    private readonly HttpClient _httpClient;
    private readonly string? _token;

    public GitHubKnowledgeSourceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");

        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
            _httpClient.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("SohailOS-KnowledgeIndexer", "1.0"));

        if (!_httpClient.DefaultRequestHeaders.Accept.Any())
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        if (!_httpClient.DefaultRequestHeaders.Contains("X-GitHub-Api-Version"))
            _httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2026-03-10");
    }

    public async Task<GitHubRepositorySnapshot> GetRepositorySnapshotAsync(
        LiveRepository repository,
        CancellationToken cancellationToken = default)
    {
        var (owner, name) = SplitRepositoryName(repository.FullName);
        using var repositoryResponse = await SendAsync(
            $"/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(name)}",
            cancellationToken);

        using var repositoryJson = await JsonDocument.ParseAsync(
            await repositoryResponse.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        var root = repositoryJson.RootElement;
        var id = root.GetProperty("id").GetInt64();
        var fullName = root.GetProperty("full_name").GetString()
            ?? throw new InvalidDataException("GitHub repository has no full_name.");
        var defaultBranch = root.GetProperty("default_branch").GetString()
            ?? throw new InvalidDataException("GitHub repository has no default_branch.");
        var isPrivate = root.TryGetProperty("private", out var privateElement) &&
                        privateElement.ValueKind == JsonValueKind.True;

        using var branchResponse = await SendAsync(
            $"/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(name)}/branches/{Uri.EscapeDataString(defaultBranch)}",
            cancellationToken);

        using var branchJson = await JsonDocument.ParseAsync(
            await branchResponse.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        var gitOid = branchJson.RootElement
            .GetProperty("commit")
            .GetProperty("sha")
            .GetString()
            ?? throw new InvalidDataException("GitHub branch response has no commit SHA.");

        return new GitHubRepositorySnapshot(id, fullName, defaultBranch, gitOid, isPrivate);
    }

    public async Task<(IReadOnlyList<GitHubTreeEntry> Entries, bool Truncated)> GetTreeAsync(
        LiveRepository repository,
        string gitOid,
        CancellationToken cancellationToken = default)
    {
        var (owner, name) = SplitRepositoryName(repository.FullName);
        using var response = await SendAsync(
            $"/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(name)}/git/trees/{Uri.EscapeDataString(gitOid)}?recursive=1",
            cancellationToken);

        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        var truncated = json.RootElement.TryGetProperty("truncated", out var truncatedElement) &&
                        truncatedElement.ValueKind == JsonValueKind.True;

        var entries = new List<GitHubTreeEntry>();
        foreach (var item in json.RootElement.GetProperty("tree").EnumerateArray())
        {
            var type = item.TryGetProperty("type", out var typeElement)
                ? typeElement.GetString()
                : null;
            if (!string.Equals(type, "blob", StringComparison.OrdinalIgnoreCase))
                continue;

            var path = item.GetProperty("path").GetString()
                ?? throw new InvalidDataException("GitHub tree entry has no path.");
            var sha = item.GetProperty("sha").GetString()
                ?? throw new InvalidDataException($"GitHub tree entry has no SHA: {path}");
            var size = item.TryGetProperty("size", out var sizeElement)
                && sizeElement.TryGetInt64(out var parsedSize)
                ? parsedSize
                : 0;

            entries.Add(new GitHubTreeEntry(path, sha, size, "blob"));
        }

        return (entries, truncated);
    }

    public async Task<byte[]> GetBlobAsync(
        LiveRepository repository,
        string sha,
        CancellationToken cancellationToken = default)
    {
        var (owner, name) = SplitRepositoryName(repository.FullName);
        using var response = await SendAsync(
            $"/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(name)}/git/blobs/{Uri.EscapeDataString(sha)}",
            cancellationToken);

        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        var encoding = json.RootElement.GetProperty("encoding").GetString();
        if (!string.Equals(encoding, "base64", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"GitHub blob {sha} used unsupported encoding '{encoding}'.");

        var content = json.RootElement.GetProperty("content").GetString() ?? string.Empty;
        return Convert.FromBase64String(content);
    }

    private async Task<HttpResponseMessage> SendAsync(string relativePath, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://api.github.com{relativePath}");

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2026-03-10");

        if (!string.IsNullOrWhiteSpace(_token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
            return response;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.Dispose();

        if ((int)response.StatusCode == 429 || (int)response.StatusCode == 403 &&
            body.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
        {
            throw new GitHubRateLimitException(body);
        }

        if ((int)response.StatusCode is 401 or 403)
            throw new GitHubAuthorizationException(body);

        if ((int)response.StatusCode == 404)
            throw new FileNotFoundException($"GitHub resource was not found: {relativePath}");

        throw new HttpRequestException(
            $"GitHub source request failed with {(int)response.StatusCode}: {body}");
    }

    private static (string Owner, string Name) SplitRepositoryName(string fullName)
    {
        var parts = fullName.Split('/', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            throw new ArgumentException($"Repository name must be owner/name: {fullName}");

        return (parts[0], parts[1]);
    }
}

public sealed class GitHubRateLimitException(string message) : HttpRequestException(message);
public sealed class GitHubAuthorizationException(string message) : HttpRequestException(message);
