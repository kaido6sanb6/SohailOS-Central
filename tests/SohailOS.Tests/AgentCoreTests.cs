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


    [Fact]
    public async Task ToolExecutor_EnforcesPermissionPolicyBeforeExecution()
    {
        var registry = new ToolRegistry();
        registry.Register(new FakeTool("read", ToolPermission.ReadOnly));
        var executor = new ToolExecutor(registry, new DenyAllPermissionPolicy());

        var result = await executor.ExecuteAsync(new ToolCall("read", new Dictionary<string, object?>()));

        Assert.False(result.Executed);
        Assert.Equal("PERMISSION_POLICY_DENIED", result.Policy?.Code);
    }

    [Fact]
    public async Task ToolExecutor_RequiresExactApprovalForMutation()
    {
        var registry = new ToolRegistry();
        registry.Register(new FakeTool("write", ToolPermission.Write));
        var executor = new ToolExecutor(registry, new DefaultPermissionPolicy());
        var call = new ToolCall("write", new Dictionary<string, object?>(), "repo/a", "file:x", "change x");

        var blocked = await executor.ExecuteAsync(call);
        Assert.False(blocked.Executed);

        var approval = new ApprovalBinding(
            "write", "repo/a", "file:x", "change x",
            RequestId: "",
            Nonce: Guid.NewGuid().ToString("N"),
            IssuedAt: DateTimeOffset.UtcNow.AddMinutes(-1),
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(5),
            ScopeHash: ApprovalBinding.ScopeFingerprint("file:x"));

        var allowed = await executor.ExecuteAsync(call, approval);
        Assert.True(allowed.Executed);
        Assert.True(allowed.Result.Success);
    }

    private sealed class DenyAllPermissionPolicy : IPermissionPolicy
    {
        public bool RequiresConfirmation(ToolDefinition definition) => true;
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
