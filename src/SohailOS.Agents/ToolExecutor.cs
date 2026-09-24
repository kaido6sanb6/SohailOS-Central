using SohailOS.Core;

namespace SohailOS.Agents;

public sealed class ToolExecutor : IToolExecutor
{
    private readonly IToolRegistry _registry;
    private readonly IPermissionPolicy _policy;

    public ToolExecutor(IToolRegistry registry, IPermissionPolicy policy)
    {
        _registry = registry;
        _policy = policy;
    }

    public Task<ToolExecutionResult> ExecuteAsync(
        ToolCall call,
        bool confirmed = false,
        CancellationToken cancellationToken = default) =>
        ExecuteInternalAsync(call, null, cancellationToken);

    public Task<ToolExecutionResult> ExecuteAsync(
        ToolCall call,
        ApprovalBinding approval,
        CancellationToken cancellationToken = default) =>
        ExecuteInternalAsync(call, approval, cancellationToken);

    private async Task<ToolExecutionResult> ExecuteInternalAsync(
        ToolCall call,
        ApprovalBinding? approval,
        CancellationToken cancellationToken)
    {
        var tool = _registry.Get(call.Name);
        if (tool is null)
            return new(new ToolResult(call.Name, false, $"Unknown tool: {call.Name}"), false, false);

        var requiresConfirmation = _policy.RequiresConfirmation(tool.Definition);
        if (requiresConfirmation)
        {
            var action = tool.Definition.Permission == ToolPermission.Destructive
                ? ActionClass.Mutate
                : ActionClass.Write;

            var request = new ExecutionRequest(
                action,
                call.Name,
                call.Target ?? call.Name,
                call.Scope ?? "unspecified",
                call.IntendedEffect ?? tool.Definition.Description);

            var decision = AdaptiveExecutionPolicy.Evaluate(
                request,
                CapabilityStatus.Verified,
                approval);

            if (!decision.Allowed)
            {
                return new(
                    new ToolResult(call.Name, false, decision.Reason, true),
                    false,
                    true);
            }
        }

        var result = await tool.ExecuteAsync(call.Arguments, cancellationToken);
        return new(result, true, false);
    }
}
