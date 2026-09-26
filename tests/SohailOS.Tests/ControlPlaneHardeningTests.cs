using SohailOS.Agents;
using SohailOS.Core;
using Xunit;

namespace SohailOS.Tests;

public sealed class ControlPlaneHardeningTests
{
    [Fact]
    public void CapabilityAttestation_FingerprintsContract()
    {
        var attestor = new CapabilityAttestor();
        var definition = new ToolDefinition("write", "Write data", ToolPermission.Write);
        var first = attestor.Attest(definition, DateTimeOffset.UtcNow);
        var second = attestor.Attest(definition, DateTimeOffset.UtcNow.AddSeconds(1));

        Assert.Equal(first.SchemaFingerprint, second.SchemaFingerprint);
        Assert.True(first.IsUsable(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void CapabilityRegistry_ProbeRejectsStaleAttestation()
    {
        var registry = new CapabilityRegistryContract();
        var now = DateTimeOffset.UtcNow;
        registry.Register(new CapabilityAttestationContract("tool:test", "1.0", "schema", ["read"], "evidence", now.AddMinutes(-2), now.AddMinutes(-1)));
        Assert.Null(registry.Probe("tool:test", now));
    }

    [Fact]
    public void ApprovalBinding_CreateComputesStableDigest()
    {
        var expiry = DateTimeOffset.UtcNow.AddHours(1);
        var approval = ApprovalBinding.Create("owner", "op", "target", "scope", "effect", expiry, "nonce");
        Assert.False(string.IsNullOrWhiteSpace(approval.Digest));
        Assert.True(approval.MatchesDigest());
    }

    [Fact]
    public async Task MutationTransaction_BlocksReplayAndUnverifiedCompletion()
    {
        var approval = ApprovalBinding.Create("owner", "op", "target", "scope", "effect", DateTimeOffset.UtcNow.AddHours(1), Guid.NewGuid().ToString("N"));
        var request = new ExecutionRequest(ActionClass.Mutate, "op", "target", "scope", "effect");
        var tx = new MutationTransactionContract(approval, request);
        await Assert.ThrowsAsync<InvalidOperationException>(() => tx.ExecuteAsync(() => Task.CompletedTask));
        tx.Preview();
        tx.ScheduleIndependentVerifier(() => Task.FromResult(true));
        var result = await tx.ExecuteAsync(() => Task.CompletedTask);
        Assert.True(result.Executed);
        Assert.Equal(VerificationStatus.Unknown, result.VerificationStatus);
        var verified = await tx.VerifyAsync();
        Assert.Equal(VerificationStatus.Verified, verified);
        await Assert.ThrowsAsync<InvalidOperationException>(() => tx.ExecuteAsync(() => Task.CompletedTask));
    }

    [Fact]
    public void OrchestrationBoundary_RejectsBackwardTransition()
    {
        var boundary = new OrchestrationBoundary();
        boundary.Advance(LifecycleStage.CLASSIFY);
        Assert.Throws<InvalidOperationException>(() => boundary.Advance(LifecycleStage.REQUEST));
        Assert.Equal(LifecycleStage.CLASSIFY, boundary.Current);
    }

    [Fact]
    public void OrchestrationBoundary_DoesNotEquateExecuteWithVerification()
    {
        var boundary = new OrchestrationBoundary();
        boundary.Advance(LifecycleStage.CLASSIFY);
        boundary.Advance(LifecycleStage.EXECUTE);
        Assert.Equal(LifecycleStage.EXECUTE, boundary.Current);
    }

    [Fact]
    public void ApprovalReplayGuard_ConsumesNonceOnce()
    {
        var guard = new InMemoryApprovalReplayGuard();
        var expiry = DateTimeOffset.UtcNow.AddMinutes(5);

        Assert.True(guard.TryConsume("n1", expiry));
        Assert.False(guard.TryConsume("n1", expiry));
    }

    [Fact]
    public void TaskGraph_RejectsCycles()
    {
        var graph = new TaskGraph();
        graph.Add(new TaskNode("a", "A", ["b"], ActionClass.Read, new RetryPolicy(true)));
        graph.Add(new TaskNode("b", "B", ["a"], ActionClass.Read, new RetryPolicy(true)));

        Assert.Throws<InvalidOperationException>(() => TaskGraph.ValidateAcyclic(graph));
    }

    [Fact]
    public void TaskGraph_RejectsUnknownDependenciesWhenScheduling()
    {
        var graph = new TaskGraph();
        graph.Add(new TaskNode("a", "A", ["missing"], ActionClass.Read, new RetryPolicy(true)));

        Assert.Throws<InvalidOperationException>(() => graph.GetReadyNodes());
    }

    [Fact]
    public void TaskGraph_PersistsReadyToRunningStatus()
    {
        var graph = new TaskGraph();
        graph.Add(new TaskNode("a", "A", [], ActionClass.Read, new RetryPolicy(true)));

        var eligible = Assert.Single(graph.GetReadyNodes());
        Assert.Equal(TaskNodeStatus.Planned, eligible.Status);
        Assert.Equal(TaskNodeStatus.Planned, Assert.Single(graph.Nodes).Status);

        var ready = Assert.Single(graph.ClaimReadyNodes());
        Assert.Equal(TaskNodeStatus.Ready, ready.Status);
        Assert.Equal(TaskNodeStatus.Ready, Assert.Single(graph.Nodes).Status);

        graph.SetStatus("a", TaskNodeStatus.Running);

        Assert.Equal(TaskNodeStatus.Running, Assert.Single(graph.Nodes).Status);
    }

    [Fact]
    public async Task MutationTransaction_MarksFailedWhenMutationThrows()
    {
        var approval = ApprovalBinding.Create(
            "owner", "op", "target", "scope", "effect",
            DateTimeOffset.UtcNow.AddHours(1),
            Guid.NewGuid().ToString("N"));

        var request = new ExecutionRequest(ActionClass.Mutate, "op", "target", "scope", "effect");
        var tx = new MutationTransactionContract(approval, request);
        tx.Preview();
        tx.ScheduleIndependentVerifier(() => Task.FromResult(true));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tx.ExecuteAsync(() => throw new InvalidOperationException("boom")));

        Assert.Equal(MutationTransactionState.Failed, tx.State);
    }

    [Fact]
    public void MutationTransaction_RejectsApprovalScopeMismatch()
    {
        var approval = ApprovalBinding.Create(
            "owner", "op", "target", "scope-a", "effect",
            DateTimeOffset.UtcNow.AddHours(1),
            Guid.NewGuid().ToString("N"));

        var request = new ExecutionRequest(ActionClass.Mutate, "op", "target", "scope-b", "effect");
        var tx = new MutationTransactionContract(approval, request);

        Assert.Throws<InvalidOperationException>(() => tx.Preview());
        Assert.Equal(MutationTransactionState.Created, tx.State);
    }

    [Fact]
    public void EvidencePipeline_DoesNotVerifyMutationsFromTheirOwnToolOutput()
    {
        var request = new ExecutionRequest(ActionClass.Mutate, "write", "target", "scope", "change");
        var evidence = new EvidenceItem("ev:1", EvidenceKind.ToolResult, "write", "ok", DateTimeOffset.UtcNow);
        var result = new ToolExecutionResult(
            new ToolResult("write", true, "ok"),
            true,
            false,
            new PolicyDecision(true, "APPROVED", "ok"),
            evidence);

        var verification = new BasicExecutionVerifier().Verify(request, result, [evidence]);

        Assert.Equal(VerificationStatus.Unknown, verification.Status);
        Assert.Equal("EXTERNAL_OUTCOME_VERIFICATION_REQUIRED", verification.Code);
    }

    [Fact]
    public void EvidencePipeline_DistinguishesExecutionFromValidation()
    {
        var request = new ExecutionRequest(ActionClass.Read, "read", "target", "scope", "observe");
        var evidence = new EvidenceItem("ev:1", EvidenceKind.ToolResult, "read", "ok", DateTimeOffset.UtcNow);
        var result = new ToolExecutionResult(
            new ToolResult("read", true, "ok"),
            true,
            false,
            new PolicyDecision(true, "ALLOWED", "ok"),
            evidence);

        var verification = new BasicExecutionVerifier().Verify(request, result, [evidence]);
        var validation = new InvariantValidator().Validate(request, verification, [evidence]);

        Assert.Equal(VerificationStatus.Verified, verification.Status);
        Assert.Equal(ValidationStatus.Validated, validation.Status);
    }
}
