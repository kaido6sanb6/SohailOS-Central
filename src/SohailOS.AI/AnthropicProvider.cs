using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SohailOS.Core;

namespace SohailOS.AI;

public sealed class AnthropicProvider : IAiProvider, IAiCompletionProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly string _apiKey;

    public AnthropicProvider(HttpClient httpClient, string model, string apiKey)
    {
        _httpClient = httpClient;
        _model = model;
        _apiKey = apiKey;
    }

    public string Name => $"Anthropic:{_model}";

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        var completion = await CompleteAsync(systemPrompt, userPrompt, Array.Empty<ToolDefinition>(), cancellationToken);
        return completion.Content;
    }

    public async Task<AiCompletion> CompleteAsync(string systemPrompt, string userPrompt, IReadOnlyCollection<ToolDefinition> tools, CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = _model,
            ["max_tokens"] = 4096,
            ["system"] = systemPrompt,
            ["messages"] = new[] { new { role = "user", content = userPrompt } }
        };

        if (tools.Count > 0)
        {
            payload["tools"] = tools.Select(tool => new
            {
                name = tool.Name,
                description = tool.Description,
                input_schema = new
                {
                    type = "object",
                    properties = (tool.Parameters ?? new Dictionary<string, string>()).ToDictionary(
                        x => x.Key,
                        x => (object)new { type = "string", description = x.Value }),
                    additionalProperties = false
                }
            }).ToArray();
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Anthropic returned {(int)response.StatusCode}: {body}");

        using var document = JsonDocument.Parse(body);
        var content = new StringBuilder();
        var calls = new List<ToolCall>();
        if (document.RootElement.TryGetProperty("content", out var blocks))
        {
            foreach (var block in blocks.EnumerateArray())
            {
                var type = block.GetProperty("type").GetString();
                if (type == "text")
                    content.Append(block.GetProperty("text").GetString());
                else if (type == "tool_use")
                {
                    var name = block.GetProperty("name").GetString();
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    var arguments = new Dictionary<string, object?>();
                    if (block.TryGetProperty("input", out var input) && input.ValueKind == JsonValueKind.Object)
                        foreach (var property in input.EnumerateObject())
                            arguments[property.Name] = property.Value.Clone();
                    calls.Add(new ToolCall(name, arguments));
                }
            }
        }

        return new AiCompletion(content.ToString(), calls, calls.Count == 0);
    }
}
