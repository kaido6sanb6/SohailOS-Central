using System.Collections.Concurrent;
using System.Text.Json;
using SohailOS.Agents;
using SohailOS.AI;
using SohailOS.Core;
using SohailOS.Integrations;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
var app = builder.Build();
app.UseCors();

const string ProtocolVersion = "2025-06-18";
var apiToken = Environment.GetEnvironmentVariable("SOHAILOS_GATEWAY_TOKEN");

bool Authorized(HttpRequest request) =>
    !string.IsNullOrWhiteSpace(apiToken) &&
    request.Headers.Authorization.ToString().Equals($"Bearer {apiToken}", StringComparison.Ordinal);

var registry = new ToolRegistry();
var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
var allowedHosts = (Environment.GetEnvironmentVariable("SOHAILOS_WEB_ALLOWLIST") ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
registry.Register(new WebFetchTool(httpClient, allowedHosts));
var executor = new ToolExecutor(registry, new DefaultPermissionPolicy());
var (_, completionProvider) = AiProviderFactory.Create(httpClient);
var runtime = new AgentRuntime(completionProvider, registry, executor);
var sessions = new ConcurrentDictionary<string, DateTimeOffset>();

app.MapGet("/health", () => Results.Ok(new
{
    service = "SohailOS.Gateway",
    status = "ok",
    utc = DateTimeOffset.UtcNow,
    provider = completionProvider.Name,
    protocolVersion = ProtocolVersion,
    tools = registry.Definitions.Select(x => x.Name).ToArray(),
    mcpSessions = sessions.Count
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
    return Results.Ok(new { output = answer, provider = completionProvider.Name });
});

app.MapPost("/mcp", async (HttpRequest request, HttpResponse response, CancellationToken cancellationToken) =>
{
    if (!Authorized(request)) return Results.Unauthorized();
    if (!request.Headers.Accept.Any(v => v.Contains("application/json", StringComparison.OrdinalIgnoreCase) ||
                                         v.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase)))
        return Results.BadRequest(new { error = "Accept must include application/json or text/event-stream." });

    var rpc = await JsonSerializer.DeserializeAsync<JsonRpcRequest>(request.Body, cancellationToken: cancellationToken);
    if (rpc is null) return Results.BadRequest();

    var isInitialize = rpc.Method == "initialize";
    if (!isInitialize)
    {
        var sessionId = request.Headers["Mcp-Session-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(sessionId) || !sessions.ContainsKey(sessionId))
            return Results.StatusCode(StatusCodes.Status404NotFound);
        sessions[sessionId] = DateTimeOffset.UtcNow;
    }

    object result = rpc.Method switch
    {
        "initialize" => new
        {
            protocolVersion = ProtocolVersion,
            capabilities = new { tools = new { } },
            serverInfo = new { name = "SohailOS", version = "0.1.0" }
        },
        "notifications/initialized" => new { },
        "ping" => new { },
        "tools/list" => new
        {
            tools = registry.Definitions.Select(d => new
            {
                name = d.Name,
                description = d.Description,
                inputSchema = new { type = "object", properties = d.Parameters ?? new Dictionary<string, string>() }
            })
        },
        "tools/call" => await CallToolAsync(rpc.Params, executor, cancellationToken),
        _ => new { error = new { code = -32601, message = $"Method not found: {rpc.Method}" } }
    };

    if (isInitialize)
    {
        var newSession = Guid.NewGuid().ToString("N");
        sessions[newSession] = DateTimeOffset.UtcNow;
        response.Headers["Mcp-Session-Id"] = newSession;
    }

    response.Headers["MCP-Protocol-Version"] = ProtocolVersion;
    response.Headers["Cache-Control"] = "no-store";
    return Results.Json(new { jsonrpc = "2.0", id = rpc.Id, result });
});

app.MapGet("/mcp", (HttpRequest request) =>
{
    if (!Authorized(request)) return Results.Unauthorized();
    return Results.StatusCode(StatusCodes.Status405MethodNotAllowed);
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
