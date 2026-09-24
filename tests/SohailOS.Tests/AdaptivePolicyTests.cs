using SohailOS.Core;
using Xunit;

namespace SohailOS.Tests;

public sealed class AdaptiveExecutionPolicyContractTests
{
    [Fact]
    public void UnknownCapability_BlocksMutation()
    {
        var request = new ExecutionRequest(ActionClass.Write, "update", "repo/a", "file:x", "change x");
        var decision = AdaptiveExecutionPolicy.Evaluate(request, CapabilityStatus.Unknown);
        Assert.False(decision.Allowed);
        Assert.Equal("CAPABILITY_UNVERIFIED", decision.Code);
    }

    [Fact]
    public void ExactApproval_AllowsWrite()
    {
        var request = new ExecutionRequest(ActionClass.Write, "update", "repo/a", "file:x", "change x");
        var approval = new ApprovalBinding("update", "repo/a", "file:x", "change x");
        var decision = AdaptiveExecutionPolicy.Evaluate(request, CapabilityStatus.Verified, approval);
        Assert.True(decision.Allowed);
    }

    [Fact]
    public void MismatchedApproval_BlocksWrite()
    {
        var request = new ExecutionRequest(ActionClass.Write, "update", "repo/a", "file:x", "change x");
        var approval = new ApprovalBinding("update", "repo/b", "file:x", "change x");
        var decision = AdaptiveExecutionPolicy.Evaluate(request, CapabilityStatus.Verified, approval);
        Assert.False(decision.Allowed);
        Assert.Equal("APPROVAL_REQUIRED", decision.Code);
    }

    [Fact]
    public void IrreversibleAction_RequiresRiskAndRollback()
    {
        var request = new ExecutionRequest(ActionClass.Irreversible, "delete", "repo/a", "all", "delete repository");
        var approval = new ApprovalBinding("delete", "repo/a", "all", "delete repository");
        var decision = AdaptiveExecutionPolicy.Evaluate(request, CapabilityStatus.Verified, approval);
        Assert.False(decision.Allowed);
        Assert.Equal("IRREVERSIBLE_GUARD", decision.Code);
    }

    [Fact]
    public void RedTeamScopeMissingField_BlocksExecution()
    {
        var scope = new RedTeamScope("target", "probe", "", "10m", "stop", "authorized");
        var request = new ExecutionRequest(ActionClass.Analyze, "probe", "target", "test", "simulate", RedTeam: scope);
        var decision = AdaptiveExecutionPolicy.Evaluate(request, CapabilityStatus.Verified);
        Assert.False(decision.Allowed);
        Assert.Equal("REDTEAM_SCOPE_INCOMPLETE", decision.Code);
    }

    [Fact]
    public void RedTeamWithoutRuntimeToken_BlocksExecution()
    {
        var scope = new RedTeamScope("target", "probe", "none", "10m", "stop", "authorized");
        var request = new ExecutionRequest(ActionClass.Analyze, "probe", "target", "test", "simulate", RedTeam: scope);
        var decision = AdaptiveExecutionPolicy.Evaluate(request, CapabilityStatus.Verified);
        Assert.False(decision.Allowed);
    }

    [Fact]
    public void NonIdempotentUnknownOutcome_RequiresExternalVerification()
    {
        var request = new ExecutionRequest(ActionClass.Mutate, "publish", "repo/a", "release", "publish release", false, false);
        var decision = AdaptiveExecutionPolicy.EvaluateOutcome(request, true);
        Assert.False(decision.Allowed);
        Assert.Equal("EXTERNAL_VERIFY_REQUIRED", decision.Code);
    }

    [Fact]
    public void DryRunSupported_RequestsDryRunFirst()
    {
        var request = new ExecutionRequest(ActionClass.Write, "update", "repo/a", "file:x", "change x", DryRunSupported: true);
        var approval = new ApprovalBinding("update", "repo/a", "file:x", "change x");
        var decision = AdaptiveExecutionPolicy.Evaluate(request, CapabilityStatus.Verified, approval);
        Assert.True(decision.Allowed);
        Assert.True(decision.RequiresDryRun);
        Assert.Equal("DRY_RUN_FIRST", decision.Code);
    }
}
