using System.Collections.Concurrent;
using System.Text.Json;
using SohailOS.Agents;
using SohailOS.AI;
using SohailOS.Core;
using SohailOS.Integrations;
using SohailOS.Memory;
using SohailOS.Ecosystem;
using SohailOS.Gateway;

var builder = WebApplication.CreateBuilder(args);
var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    var origins = (Environment.GetEnvironmentVariable("SOHAILOS_CORS_ORIGINS") ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (origins.Length == 0)
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    else
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
}));

var app = builder.Build();
app.UseCors();

const string ProtocolVersion = "2025-06-18";
var apiToken = Environment.GetEnvironmentVariable("SOHAILOS_GATEWAY_TOKEN");
var sessionTtl = TimeSpan.FromMinutes(GetPositiveInt("SOHAILOS_MCP_SESSION_TTL_MINUTES", 60));

bool Authorized(HttpRequest request) =>
    !string.IsNullOrWhiteSpace(apiToken) &&
    request.Headers.Authorization.ToString().Equals($"Bearer {apiToken}", StringComparison.Ordinal);

var registry = new ToolRegistry();
var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
var researchEngine = new ResearchEngine(httpClient);
var knowledgeIndexPath = Environment.GetEnvironmentVariable("SOHAILOS_KNOWLEDGE_INDEX_PATH")
    ?? Path.Combine(AppContext.BaseDirectory, "App_Data", "sohailos-knowledge-index.json");
DataPlaneProvider knowledgeProvider = new JsonFileDataPlaneProvider(knowledgeIndexPath);
IEmbeddingProvider knowledgeEmbeddingProvider;
var embeddingsBaseUrl = Environment.GetEnvironmentVariable("SOHAILOS_EMBEDDINGS_BASE_URL");
var embeddingsModel = Environment.GetEnvironmentVariable("SOHAILOS_EMBEDDINGS_MODEL");
var embeddingsKey = Environment.GetEnvironmentVariable("SOHAILOS_EMBEDDINGS_API_KEY");
var embeddingsDimensions = GetPositiveInt("SOHAILOS_EMBEDDINGS_DIMENSIONS", 1536);
if (!string.IsNullOrWhiteSpace(embeddingsBaseUrl) && !string.IsNullOrWhiteSpace(embeddingsModel))
{
    knowledgeEmbeddingProvider = new OpenAICompatibleEmbeddingProvider(
        httpClient,
        embeddingsBaseUrl,
        embeddingsModel,
        embeddingsKey,
        embeddingsDimensions);
}
else
{
    knowledgeEmbeddingProvider = new NullEmbeddingProvider();
}
var knowledgeRetrieval = new KnowledgeRetrievalService(
    knowledgeProvider,
    knowledgeEmbeddingProvider);
var allowedHosts = (Environment.GetEnvironmentVariable("SOHAILOS_WEB_ALLOWLIST") ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
registry.Register(new WebFetchTool(httpClient, allowedHosts));
EcosystemKnowledgeTools.Register(registry, knowledgeRetrieval);
var executor = new ToolExecutor(registry, new DefaultPermissionPolicy());
var (_, completionProvider) = AiProviderFactory.Create(httpClient);
var memory = CreateMemoryStore(httpClient);
var runtime = new AgentRuntime(completionProvider, registry, executor, memory);
var sessions = new ConcurrentDictionary<string, DateTimeOffset>();

app.MapPost("/v1/research/search", async (HttpRequest request, ResearchSearchRequest input, CancellationToken cancellationToken) =>
{
    if (!Authorized(request)) return Results.Unauthorized();
    if (string.IsNullOrWhiteSpace(input.Query))
        return Results.BadRequest(new { error = "query is required" });
    var result = await researchEngine.SearchAsync(input, cancellationToken);
    return Results.Ok(result);
});

app.MapGet("/health", () => Results.Ok(new
{
    service = "SohailOS.Gateway",
    status = "ok",
    utc = DateTimeOffset.UtcNow,
    provider = completionProvider.Name,
    memory = memory.GetType().Name,
    protocolVersion = ProtocolVersion,
    tools = registry.Definitions.Select(x => x.Name).ToArray(),
    mcpSessions = sessions.Count
}));

app.MapPost("/v1/agent/run", async (HttpRequest request, AgentRunRequest input, CancellationToken cancellationToken) =>
{
    if (!Authorized(request)) return Results.Unauthorized();
    if (string.IsNullOrWhiteSpace(input.Prompt)) return Results.BadRequest(new { error = "prompt is required" });
    var systemPrompt = $"{CanonicalPrompt.Compact}\n\n{input.SystemPrompt ?? "You are SohailOS, a personal AI operating system. Use tools only when useful and never claim an action occurred without a successful tool result."}";
    var answer = await runtime.RunAsync(
        systemPrompt,
        input.Prompt,
        input.Approval,
        input.MemoryKey ?? "global",
        input.PersistMemory,
        cancellationToken);
    return Results.Ok(new { output = answer, provider = completionProvider.Name, memoryKey = input.MemoryKey ?? "global" });
});

app.MapPost("/mcp", async (HttpRequest request, HttpResponse response, CancellationToken cancellationToken) =>
{
    if (!Authorized(request)) return Results.Unauthorized();
    if (!request.Headers.Accept.Any(v => v is not null &&
                                         (v.Contains("application/json", StringComparison.OrdinalIgnoreCase) ||
                                          v.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase))))
        return Results.BadRequest(new { error = "Accept must include application/json or text/event-stream." });

    ExpireSessions();

    var rpc = await JsonSerializer.DeserializeAsync<JsonRpcRequest>(request.Body, cancellationToken: cancellationToken);
    if (rpc is null || !string.Equals(rpc.Jsonrpc, "2.0", StringComparison.Ordinal))
        return Results.BadRequest(new { error = "A JSON-RPC 2.0 request is required." });

    var isInitialize = rpc.Method == "initialize";
    if (!isInitialize)
    {
        var sessionId = request.Headers["Mcp-Session-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(sessionId) || !sessions.ContainsKey(sessionId))
            return Results.StatusCode(StatusCodes.Status404NotFound);
        sessions[sessionId] = DateTimeOffset.UtcNow;
    }

    // JSON-RPC notifications do not receive a JSON-RPC response body.
    if (rpc.Method == "notifications/initialized")
        return Results.StatusCode(StatusCodes.Status202Accepted);

    object result = rpc.Method switch
    {
        "initialize" => new
        {
            protocolVersion = ProtocolVersion,
            capabilities = new { tools = new { } },
            serverInfo = new { name = "SohailOS", version = "0.1.0" }
        },
        "ping" => new { },
        "tools/list" => new
        {
            tools = registry.Definitions.Select(ToMcpTool).ToArray()
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

static IMemoryStore CreateMemoryStore(HttpClient httpClient)
{
    var url = Environment.GetEnvironmentVariable("SOHAILOS_SUPABASE_URL");
    var key = Environment.GetEnvironmentVariable("SOHAILOS_SUPABASE_SERVICE_ROLE_KEY");
    if (!string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(key))
    {
        var table = Environment.GetEnvironmentVariable("SOHAILOS_SUPABASE_MEMORY_TABLE") ?? "sohailos_memory";
        return new SupabaseMemoryStore(httpClient, url, key, table);
    }

    return new InMemoryStore();
}

void ExpireSessions()
{
    var cutoff = DateTimeOffset.UtcNow - sessionTtl;
    foreach (var pair in sessions)
        if (pair.Value < cutoff)
            sessions.TryRemove(pair.Key, out _);
}

static int GetPositiveInt(string variableName, int fallback)
{
    return int.TryParse(Environment.GetEnvironmentVariable(variableName), out var value) && value > 0
        ? value
        : fallback;
}

static object ToMcpTool(ToolDefinition definition)
{
    var properties = (definition.Parameters ?? new Dictionary<string, string>())
        .ToDictionary(
            pair => pair.Key,
            pair => (object)new
            {
                type = "string",
                description = pair.Value
            },
            StringComparer.Ordinal);

    return new
    {
        name = definition.Name,
        description = definition.Description,
        inputSchema = new
        {
            type = "object",
            properties,
            additionalProperties = false
        },
        annotations = new
        {
            readOnlyHint = definition.Permission == ToolPermission.ReadOnly
        }
    };
}

static async Task<object> CallToolAsync(JsonElement parameters, IToolExecutor executor, CancellationToken cancellationToken)
{
    if (!parameters.TryGetProperty("name", out var nameElement) || nameElement.ValueKind != JsonValueKind.String)
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

public sealed record AgentRunRequest(
    string Prompt,
    string? SystemPrompt,
    ApprovalBinding? Approval = null,
    string? MemoryKey = "global",
    bool PersistMemory = false);
public sealed record JsonRpcRequest(string Jsonrpc, JsonElement Id, string Method, JsonElement Params);