namespace SohailOS.Core;

public enum CapabilityStatus
{
    Unknown,
    Verified,
    Limited,
    Unavailable,
    Forbidden
}

public enum AuthorizationStatus
{
    NotRequired,
    Required,
    Approved,
    Expired,
    Revoked
}

public enum ExecutionStatus
{
    NotStarted,
    Running,
    Succeeded,
    Failed,
    Blocked
}

public enum VerificationStatus
{
    NotChecked,
    Verified,
    Failed,
    Unknown
}

public enum ValidationStatus
{
    NotChecked,
    Validated,
    Failed,
    Unknown
}

public enum ActionClass
{
    Read,
    Analyze,
    Draft,
    Write,
    Mutate,
    Irreversible
}

public sealed record ApprovalBinding(
    string Operation,
    string Target,
    string Scope,
    string IntendedEffect,
    bool RiskAcknowledged = false,
    string? RollbackPlan = null,
    bool Reversible = true)
{
    public bool Matches(ExecutionRequest request) =>
        string.Equals(Operation, request.Operation, StringComparison.Ordinal) &&
        string.Equals(Target, request.Target, StringComparison.Ordinal) &&
        string.Equals(Scope, request.Scope, StringComparison.Ordinal) &&
        string.Equals(IntendedEffect, request.IntendedEffect, StringComparison.Ordinal);
}

public sealed record RedTeamScope(
    string Target,
    string AllowedActions,
    string ForbiddenActions,
    string TimeWindow,
    string StopConditions,
    string LegalBasis,
    bool SimulationOnly = true,
    bool RuntimeTokenIssued = false)
{
    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(Target) &&
        !string.IsNullOrWhiteSpace(AllowedActions) &&
        !string.IsNullOrWhiteSpace(ForbiddenActions) &&
        !string.IsNullOrWhiteSpace(TimeWindow) &&
        !string.IsNullOrWhiteSpace(StopConditions) &&
        !string.IsNullOrWhiteSpace(LegalBasis);
}

public sealed record ExecutionRequest(
    ActionClass Action,
    string Operation,
    string Target,
    string Scope,
    string IntendedEffect,
    bool Idempotent = true,
    bool OutcomeKnown = true,
    bool DryRunSupported = false,
    RedTeamScope? RedTeam = null);

public sealed record PolicyDecision(
    bool Allowed,
    string Code,
    string Reason,
    bool RequiresApproval = false,
    bool RequiresDryRun = false);

public static class AdaptiveExecutionPolicy
{
    public static PolicyDecision Evaluate(
        ExecutionRequest request,
        CapabilityStatus capability,
        ApprovalBinding? approval = null)
    {
        if (capability is CapabilityStatus.Unknown or CapabilityStatus.Unavailable or CapabilityStatus.Forbidden)
            return new(false, "CAPABILITY_UNVERIFIED", "Capability must be runtime-verified before use.");

        if (request.RedTeam is not null)
        {
            if (!request.RedTeam.IsComplete || !request.RedTeam.RuntimeTokenIssued)
                return new(false, "REDTEAM_SCOPE_INCOMPLETE", "Red-team execution requires all six scope fields and a runtime-issued token.");
            if (request.RedTeam.SimulationOnly)
                return new(true, "SIMULATION_ONLY", "Red-team execution is explicitly limited to simulation.", RequiresDryRun: true);
        }

        var needsApproval = request.Action is ActionClass.Write or ActionClass.Mutate or ActionClass.Irreversible;
        if (!needsApproval)
            return new(true, "ALLOWED", "Non-mutating action.");

        if (approval is null || !approval.Matches(request))
            return new(false, "APPROVAL_REQUIRED", "Exact approval binding is required: operation, target, scope, intended effect.", RequiresApproval: true);

        if (request.Action == ActionClass.Irreversible &&
            (!approval.RiskAcknowledged || string.IsNullOrWhiteSpace(approval.RollbackPlan)))
            return new(false, "IRREVERSIBLE_GUARD", "Irreversible actions require explicit risk acknowledgement and a rollback plan.");

        if (request.DryRunSupported)
            return new(true, "DRY_RUN_FIRST", "Approval is valid; preview/dry-run should precede execution.", RequiresDryRun: true);

        return new(true, "APPROVED", "Exact approval binding verified.");
    }

    public static PolicyDecision EvaluateOutcome(ExecutionRequest request, bool executionSucceeded)
    {
        if (!executionSucceeded)
            return new(false, "EXECUTION_FAILED", "Execution did not succeed.");

        if (!request.OutcomeKnown && !request.Idempotent)
            return new(false, "EXTERNAL_VERIFY_REQUIRED", "Unknown outcome of a non-idempotent action requires external state verification before retry.");

        return new(true, "VERIFY", "Execution completed; independent verification is still required.");
    }
}
