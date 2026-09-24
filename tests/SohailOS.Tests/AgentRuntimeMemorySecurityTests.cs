using SohailOS.Agents;
using SohailOS.Core;
using Xunit;

namespace SohailOS.Tests;

public sealed class AgentRuntimeMemorySecurityTests
{
    [Fact]
    public async Task DurableMemoryWrite_IsBlockedWithoutExplicitApproval()
    {
        var memory = new RecordingMemoryStore();
        var runtime = new AgentRuntime(new FinalProvider(), new ToolRegistry(), new StubToolExecutor(), memory);

        await runtime.RunAsync(
            "system",
            "remember this",
            approval: null,
            memoryKey: "user-test",
            persistMemory: true);

        Assert.Empty(memory.Saves);
    }

    [Fact]
    public async Task DurableMemoryWrite_RequiresExactMemoryScopeApproval()
    {
        var memory = new RecordingMemoryStore();
        var runtime = new AgentRuntime(new FinalProvider(), new ToolRegistry(), new StubToolExecutor(), memory);
        var approval = new ApprovalBinding(
            "memory_write",
            "SohailOS.Memory",
            "user-test",
            "persist memory",
            RequestId: "",
            Nonce: Guid.NewGuid().ToString("N"),
            IssuedAt: DateTimeOffset.UtcNow.AddMinutes(-1),
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(5),
            ScopeHash: ApprovalBinding.ScopeFingerprint("user-test"));

        await runtime.RunAsync(
            "system",
            "remember this",
            approval: approval,
            memoryKey: "user-test",
            persistMemory: true);

        Assert.Single(memory.Saves);
        Assert.Equal("user-test", memory.Saves[0].Key);
    }

    private sealed class FinalProvider : IAiCompletionProvider
    {
        public string Name => "test";
        public Task<AiCompletion> CompleteAsync(
            string systemPrompt,
            string userPrompt,
            IReadOnlyCollection<ToolDefinition> tools,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AiCompletion("final", Array.Empty<ToolCall>()));
    }

    private sealed class StubToolExecutor : IToolExecutor
    {
        public Task<ToolExecutionResult> ExecuteAsync(ToolCall call, bool confirmed = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ToolExecutionResult(new ToolResult(call.Name, false, "not used"), false, false));

        public Task<ToolExecutionResult> ExecuteAsync(ToolCall call, ApprovalBinding approval, CancellationToken cancellationToken = default) =>
            ExecuteAsync(call, false, cancellationToken);
    }

    private sealed class RecordingMemoryStore : IMemoryStore
    {
        public List<(string Key, string Value)> Saves { get; } = [];

        public Task SaveAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            Saves.Add((key, value));
            return Task.CompletedTask;
        }

        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }
}
