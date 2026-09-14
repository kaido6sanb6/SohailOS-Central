using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SohailOS.Core;

namespace SohailOS.AI;

public sealed class OpenAiCompatibleProvider : IAiProvider, IAiCompletionProvider
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

    public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default) =>
        CompleteTextAsync(systemPrompt, userPrompt, cancellationToken);

    public async Task<AiCompletion> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        IReadOnlyCollection<ToolDefinition> tools,
        CancellationToken cancellationToken = default)
    {
        using var request = BuildRequest(systemPrompt, userPrompt, tools);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"AI provider returned {(int)response.StatusCode}: {body}");

        using var document = JsonDocument.Parse(body);
        var message = document.RootElement.GetProperty("choices")[0].GetProperty("message");
        var content = message.TryGetProperty("content", out var contentElement)
            ? contentElement.GetString() ?? string.Empty
            : string.Empty;
        var calls = new List<ToolCall>();

        if (message.TryGetProperty("tool_calls", out var toolCalls) && toolCalls.ValueKind == JsonValueKind.Array)
        {
            foreach (var call in toolCalls.EnumerateArray())
            {
                var function = call.GetProperty("function");
                var name = function.GetProperty("name").GetString();
                if (string.IsNullOrWhiteSpace(name)) continue;
                var arguments = new Dictionary<string, object?>();
                if (function.TryGetProperty("arguments", out var argsElement))
                {
                    try
                    {
                        using var argsDoc = JsonDocument.Parse(argsElement.GetString() ?? "{}");
                        if (argsDoc.RootElement.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var property in argsDoc.RootElement.EnumerateObject())
                                arguments[property.Name] = property.Value.Clone();
                        }
                    }
                    catch (JsonException) { }
                }
                calls.Add(new ToolCall(name, arguments));
            }
        }

        return new AiCompletion(content, calls, calls.Count == 0);
    }

    private async Task<string> CompleteTextAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken)
    {
        using var request = BuildRequest(systemPrompt, userPrompt, Array.Empty<ToolDefinition>());
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"AI provider returned {(int)response.StatusCode}: {body}");

        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
    }

    private HttpRequestMessage BuildRequest(string systemPrompt, string userPrompt, IReadOnlyCollection<ToolDefinition> tools)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = _model,
            ["messages"] = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            ["temperature"] = 0.2
        };

        if (tools.Count > 0)
        {
            payload["tools"] = tools.Select(tool => new
            {
                type = "function",
                function = new
                {
                    name = tool.Name,
                    description = tool.Description,
                    parameters = new
                    {
                        type = "object",
                        properties = (tool.Parameters ?? new Dictionary<string, string>()).ToDictionary(
                            x => x.Key,
                            x => (object)new { type = "string", description = x.Value }),
                        additionalProperties = false
                    }
                }
            }).ToArray();
            payload["tool_choice"] = "auto";
        }

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_endpoint}/chat/completions");
        if (_apiKey is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        return request;
    }
}
