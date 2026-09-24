using SohailOS.Agents;
using SohailOS.Core;
using Xunit;

namespace SohailOS.Tests;

public sealed class AdaptivePolicyTests
{
    [Fact]
    public async Task WriteWithLegacyConfirmationButNoExactApproval_IsBlocked()
    {
        var registry = new ToolRegistry();
        registry.Register(new FakeTool("write", ToolPermission.Write));
        var executor = new ToolExecutor(registry, new DefaultPermissionPolicy());

        var result = await executor.ExecuteAsync(
            new ToolCall("write", new Dictionary<string, object?>()),
            confirmed: true);

        Assert.False(result.Executed);
        Assert.Contains("approval", result.Result.Content, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeTool(string name, ToolPermission permission) : ITool
    {
        public ToolDefinition Definition { get; } = new(name, "test", permission);

        public Task<ToolResult> ExecuteAsync(
            IReadOnlyDictionary<string, object?> arguments,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ToolResult(Definition.Name, true, "ok"));
    }
}
