using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SohailOS.Core;

namespace SohailOS.AI;

/// <summary>
/// Provider for OpenAI-compatible chat-completions endpoints, including local vLLM.
/// Credentials are read from the environment and never persisted by SohailOS.
/// </summary>
public sealed class OpenAiCompatibleProvider : IAiProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly string _model;
    private readonly string? _apiKey;

    public OpenAiCompatibleProvider(HttpClient httpClient, string endpoint, string model, string? apiKey = null)
    {
        _httpClient = httpClient;
        _endpoint = endpoint.TrimEnd('/');
        _model = model;
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey;
    }

    public string Name => $"OpenAI-compatible:{_model}";

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_endpoint}/chat/completions");
        if (_apiKey is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        var payload = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.2
        };

        request.Content = new StringContent(
            JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"AI provider returned {(int)response.StatusCode}: {body}");

        using var document = JsonDocument.Parse(body);
        var content = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return content ?? string.Empty;
    }
}
