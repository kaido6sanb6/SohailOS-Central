using SohailOS.Core;

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

    private sealed class FakeTool(string name) : ITool
    {
        public ToolDefinition Definition { get; } = new(name, "test", ToolPermission.ReadOnly);

        public Task<ToolResult> ExecuteAsync(
            IReadOnlyDictionary<string, object?> arguments,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ToolResult(Definition.Name, true, "ok"));
    }
}
