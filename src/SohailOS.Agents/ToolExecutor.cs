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

    public async Task<ToolExecutionResult> ExecuteAsync(
        ToolCall call,
        bool confirmed = false,
        CancellationToken cancellationToken = default)
    {
        var tool = _registry.Get(call.Name);
        if (tool is null)
            return new(new ToolResult(call.Name, false, $"Unknown tool: {call.Name}"), false, false);

        var requiresConfirmation = _policy.RequiresConfirmation(tool.Definition);
        if (requiresConfirmation && !confirmed)
        {
            return new(
                new ToolResult(call.Name, false,
                    $"Tool '{call.Name}' requires user confirmation before execution.", true),
                false,
                true);
        }

        var result = await tool.ExecuteAsync(call.Arguments, cancellationToken);
        return new(result, true, false);
    }
}
