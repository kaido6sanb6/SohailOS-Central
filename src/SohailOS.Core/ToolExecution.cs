namespace SohailOS.Core;

public sealed record ToolExecutionResult(
    ToolResult Result,
    bool Executed,
    bool RequiresConfirmation);

public interface IToolExecutor
{
    Task<ToolExecutionResult> ExecuteAsync(
        ToolCall call,
        bool confirmed = false,
        CancellationToken cancellationToken = default);

    Task<ToolExecutionResult> ExecuteAsync(
        ToolCall call,
        ApprovalBinding approval,
        CancellationToken cancellationToken = default);
}
