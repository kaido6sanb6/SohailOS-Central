using System.Net;
using System.Text.Json;
using SohailOS.Core;

namespace SohailOS.Integrations;

public sealed class WebFetchTool : ITool
{
    private readonly HttpClient _httpClient;
    private readonly HashSet<string> _allowedHosts;
    private readonly int _maxCharacters;

    public WebFetchTool(HttpClient httpClient, IEnumerable<string>? allowedHosts = null, int maxCharacters = 12000)
    {
        _httpClient = httpClient;
        _allowedHosts = new HashSet<string>(
            (allowedHosts ?? Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)),
            StringComparer.OrdinalIgnoreCase);
        _maxCharacters = Math.Max(1000, maxCharacters);
    }

    public ToolDefinition Definition => new(
        "web_fetch",
        "Fetch a public HTTPS webpage and return a bounded text representation.",
        ToolPermission.ReadOnly,
        new Dictionary<string, string> { ["url"] = "string HTTPS URL" });

    public async Task<ToolResult> ExecuteAsync(IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        if (!arguments.TryGetValue("url", out var raw) || raw is null || !Uri.TryCreate(raw.ToString(), UriKind.Absolute, out var uri))
            return new(Definition.Name, false, "A valid URL is required.");
        if (uri.Scheme != Uri.UriSchemeHttps)
            return new(Definition.Name, false, "Only HTTPS URLs are allowed.");
        if (_allowedHosts.Count > 0 && !_allowedHosts.Contains(uri.Host))
            return new(Definition.Name, false, $"Host '{uri.Host}' is not in the configured allowlist.");

        using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return new(Definition.Name, false, $"HTTP {(int)response.StatusCode}: {body[..Math.Min(body.Length, 1000)]}");

        var text = StripHtml(body);
        if (text.Length > _maxCharacters)
            text = text[.._maxCharacters] + "\n[truncated]";
        return new(Definition.Name, true, text);
    }

    private static string StripHtml(string html)
    {
        var text = System.Text.RegularExpressions.Regex.Replace(html, "<script[\\s\\S]*?</script>|<style[\\s\\S]*?</style>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", " ");
        return WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(text, "\\s+", " ")).Trim();
    }
}

public sealed class WebSearchTool : ITool
{
    private readonly HttpClient _httpClient;
    private readonly Uri _endpoint;
    private readonly string? _apiKey;

    public WebSearchTool(HttpClient httpClient, Uri endpoint, string? apiKey = null)
    {
        _httpClient = httpClient;
        _endpoint = endpoint;
        _apiKey = apiKey;
    }

    public ToolDefinition Definition => new(
        "web_search",
        "Search the live web through the configured search provider.",
        ToolPermission.ReadOnly,
        new Dictionary<string, string> { ["query"] = "string search query" });

    public async Task<ToolResult> ExecuteAsync(IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var query = arguments.TryGetValue("query", out var raw) ? raw?.ToString() : null;
        if (string.IsNullOrWhiteSpace(query))
            return new(Definition.Name, false, "A search query is required.");

        var url = new UriBuilder(_endpoint);
        var separator = string.IsNullOrEmpty(url.Query) ? "?" : "&";
        url.Query = url.Query.TrimStart('?') + separator.TrimStart('?') + "q=" + Uri.EscapeDataString(query);
        using var request = new HttpRequestMessage(HttpMethod.Get, url.Uri);
        if (!string.IsNullOrWhiteSpace(_apiKey))
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return new(Definition.Name, false, $"Search provider returned HTTP {(int)response.StatusCode}.");
        return new(Definition.Name, true, body);
    }
}
