using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SohailOS.Core;

namespace SohailOS.Memory;

/// <summary>
/// Server-side persistent memory backed by Supabase PostgREST.
/// Configure SOHAILOS_SUPABASE_URL and SOHAILOS_SUPABASE_SERVICE_ROLE_KEY to enable it.
/// </summary>
public sealed class SupabaseMemoryStore : IMemoryStore
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _serviceRoleKey;
    private readonly string _table;

    public SupabaseMemoryStore(HttpClient httpClient, string baseUrl, string serviceRoleKey, string table = "sohailos_memory")
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl.TrimEnd('/');
        _serviceRoleKey = serviceRoleKey;
        _table = string.IsNullOrWhiteSpace(table) ? "sohailos_memory" : table;
    }

    public async Task SaveAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Memory key is required.", nameof(key));

        var payload = JsonSerializer.Serialize(new
        {
            key,
            value,
            updated_at = DateTimeOffset.UtcNow
        });

        using var request = CreateRequest(HttpMethod.Post);
        request.Headers.Add("Prefer", "resolution=merge-duplicates,return=minimal");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        var url = $"{Endpoint}?select=value&key=eq.{Uri.EscapeDataString(key)}&limit=1";
        using var request = CreateRequest(HttpMethod.Get, url);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var rows = await JsonSerializer.DeserializeAsync<List<MemoryRow>>(stream, cancellationToken: cancellationToken);
        return rows?.FirstOrDefault()?.Value;
    }

    private string Endpoint => $"{_baseUrl}/rest/v1/{_table}";

    private HttpRequestMessage CreateRequest(HttpMethod method, string? url = null)
    {
        var request = new HttpRequestMessage(method, url ?? Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _serviceRoleKey);
        request.Headers.Add("apikey", _serviceRoleKey);
        return request;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException($"Supabase memory request failed with {(int)response.StatusCode}: {body}");
    }

    private sealed record MemoryRow(string Value);
}
