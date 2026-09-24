namespace SohailOS.Core;

public enum ToolPermission
{
    ReadOnly,
    Write,
    Destructive
}

public sealed record ToolDefinition(
    string Name,
    string Description,
    ToolPermission Permission,
    IReadOnlyDictionary<string, string>? Parameters = null);

public sealed record ToolCall(
    string Name,
    IReadOnlyDictionary<string, object?> Arguments,
    string? Target = null,
    string? Scope = null,
    string? IntendedEffect = null);

public sealed record ToolResult(
    string ToolName,
    bool Success,
    string Content,
    bool RequiresConfirmation = false);

public interface ITool
{
    ToolDefinition Definition { get; }
    Task<ToolResult> ExecuteAsync(
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default);
}

public interface IToolRegistry
{
    IReadOnlyCollection<ToolDefinition> Definitions { get; }
    ITool? Get(string name);
}

public interface IPermissionPolicy
{
    bool RequiresConfirmation(ToolDefinition definition);
}

public sealed record ConversationTurn(
    string Role,
    string Content,
    DateTimeOffset CreatedAt);

public interface IConversationStore
{
    Task<IReadOnlyList<ConversationTurn>> GetRecentAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task AppendAsync(
        ConversationTurn turn,
        CancellationToken cancellationToken = default);
}

public interface IContextBuilder
{
    Task<string> BuildAsync(
        UserRequest request,
        RouteDecision route,
        CancellationToken cancellationToken = default);
}

public sealed record AiCompletion(
    string Content,
    IReadOnlyList<ToolCall> ToolCalls,
    bool IsFinal = true);

public interface IAiCompletionProvider
{
    string Name { get; }
    Task<AiCompletion> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        IReadOnlyCollection<ToolDefinition> tools,
        CancellationToken cancellationToken = default);
}
