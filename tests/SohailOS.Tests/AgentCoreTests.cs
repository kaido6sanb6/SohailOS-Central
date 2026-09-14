using SohailOS.Agents;
using SohailOS.Core;
using Xunit;

namespace SohailOS.Tests;

public sealed class AgentCoreTests
{
    [Fact]
    public void DefaultPermissionPolicy_RequiresConfirmationForWrites()
    {
        var policy = new DefaultPermissionPolicy();

        Assert.False(policy.RequiresConfirmation(new ToolDefinition("read", "Read data", ToolPermission.ReadOnly)));
        Assert.True(policy.RequiresConfirmation(new ToolDefinition("write", "Write data", ToolPermission.Write)));
        Assert.True(policy.RequiresConfirmation(new ToolDefinition("delete", "Delete data", ToolPermission.Destructive)));
    }

    [Fact]
    public void ToolRegistry_ReplacesToolsByName_CaseInsensitively()
    {
        var registry = new ToolRegistry();
        registry.Register(new FakeTool("Memory.Read"));
        registry.Register(new FakeTool("memory.read"));

        Assert.Single(registry.Definitions);
        Assert.NotNull(registry.Get("MEMORY.READ"));
    }

    [Fact]
    public async Task ToolExecutor_BlocksWriteWithoutConfirmation()
    {
        var registry = new ToolRegistry();
        registry.Register(new FakeTool("write", ToolPermission.Write));
        var executor = new ToolExecutor(registry, new DefaultPermissionPolicy());

        var result = await executor.ExecuteAsync(new ToolCall("write", new Dictionary<string, object?>()));

        Assert.False(result.Executed);
        Assert.True(result.RequiresConfirmation);
    }

    [Fact]
    public async Task ToolExecutor_ExecutesReadOnlyAutomatically()
    {
        var registry = new ToolRegistry();
        registry.Register(new FakeTool("read", ToolPermission.ReadOnly));
        var executor = new ToolExecutor(registry, new DefaultPermissionPolicy());

        var result = await executor.ExecuteAsync(new ToolCall("read", new Dictionary<string, object?>()));

        Assert.True(result.Executed);
        Assert.True(result.Result.Success);
    }

    private sealed class FakeTool(string name, ToolPermission permission = ToolPermission.ReadOnly) : ITool
    {
        public ToolDefinition Definition { get; } = new(name, "test", permission);

        public Task<ToolResult> ExecuteAsync(
            IReadOnlyDictionary<string, object?> arguments,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ToolResult(Definition.Name, true, "ok"));
    }
}
