using System.Text.Json;
using SohailOS.Agents;
using SohailOS.AI;
using SohailOS.Core;
using SohailOS.Integrations;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
var apiToken = Environment.GetEnvironmentVariable("SOHAILOS_GATEWAY_TOKEN");
var endpoint = Environment.GetEnvironmentVariable("SOHAILOS_AI_ENDPOINT") ?? "http://localhost:8000/v1";
var model = Environment.GetEnvironmentVariable("SOHAILOS_AI_MODEL") ?? "Qwen/Qwen3.5-9B";
var apiKey = Environment.GetEnvironmentVariable("SOHAILOS_AI_API_KEY");

bool Authorized(HttpRequest request) =>
    !string.IsNullOrWhiteSpace(apiToken) &&
    request.Headers.Authorization.ToString().Equals($"Bearer {apiToken}", StringComparison.Ordinal);

var registry = new ToolRegistry();
var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
var allowedHosts = (Environment.GetEnvironmentVariable("SOHAILOS_WEB_ALLOWLIST") ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
registry.Register(new WebFetchTool(httpClient, allowedHosts));
var executor = new ToolExecutor(registry, new DefaultPermissionPolicy());
var provider = new OpenAiCompatibleProvider(httpClient, endpoint, model, apiKey);
var runtime = new AgentRuntime(provider, registry, executor);

app.MapGet("/health", () => Results.Ok(new
{
    service = "SohailOS.Gateway",
    status = "ok",
    utc = DateTimeOffset.UtcNow,
    provider = provider.Name,
    tools = registry.Definitions.Select(x => x.Name).ToArray()
}));

app.MapPost("/v1/agent/run", async (HttpRequest request, AgentRunRequest input, CancellationToken cancellationToken) =>
{
    if (!Authorized(request)) return Results.Unauthorized();
    if (string.IsNullOrWhiteSpace(input.Prompt)) return Results.BadRequest(new { error = "prompt is required" });
    var answer = await runtime.RunAsync(
        input.SystemPrompt ?? "You are SohailOS, a personal AI operating system. Use tools only when useful and never claim an action occurred without a successful tool result.",
        input.Prompt,
        input.ConfirmWrites,
        cancellationToken);
    return Results.Ok(new { output = answer, provider = provider.Name });
});

app.MapPost("/mcp", async (HttpRequest request, CancellationToken cancellationToken) =>
{
    if (!Authorized(request)) return Results.Unauthorized();
    var rpc = await JsonSerializer.DeserializeAsync<JsonRpcRequest>(request.Body, cancellationToken: cancellationToken);
    if (rpc is null) return Results.BadRequest();

    object result = rpc.Method switch
    {
        "initialize" => new { protocolVersion = "2025-06-18", capabilities = new { tools = new { } }, serverInfo = new { name = "SohailOS", version = "0.1.0" } },
        "tools/list" => new { tools = registry.Definitions.Select(d => new { name = d.Name, description = d.Description, inputSchema = new { type = "object", properties = d.Parameters ?? new Dictionary<string, string>() } }) },
        "tools/call" => await CallToolAsync(rpc.Params, executor, cancellationToken),
        _ => new { error = new { code = -32601, message = $"Method not found: {rpc.Method}" } }
    };

    return Results.Json(new { jsonrpc = "2.0", id = rpc.Id, result });
});

app.Run();

static async Task<object> CallToolAsync(JsonElement parameters, IToolExecutor executor, CancellationToken cancellationToken)
{
    if (!parameters.TryGetProperty("name", out var nameElement))
        return new { isError = true, content = new[] { new { type = "text", text = "Missing tool name." } } };

    var arguments = new Dictionary<string, object?>();
    if (parameters.TryGetProperty("arguments", out var argsElement) && argsElement.ValueKind == JsonValueKind.Object)
        foreach (var property in argsElement.EnumerateObject())
            arguments[property.Name] = property.Value.Clone();

    var result = await executor.ExecuteAsync(new ToolCall(nameElement.GetString()!, arguments), false, cancellationToken);
    return new
    {
        isError = !result.Result.Success,
        content = new[] { new { type = "text", text = result.Result.Content } },
        requiresConfirmation = result.RequiresConfirmation
    };
}

public sealed record AgentRunRequest(string Prompt, string? SystemPrompt, bool ConfirmWrites = false);
public sealed record JsonRpcRequest(string Jsonrpc, JsonElement Id, string Method, JsonElement Params);
