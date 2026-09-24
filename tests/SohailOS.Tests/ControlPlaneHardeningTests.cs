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
