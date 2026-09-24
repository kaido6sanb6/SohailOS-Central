using SohailOS.Core;

namespace SohailOS.Agents;

public sealed class ToolExecutor : IToolExecutor
{
    private readonly IToolRegistry _registry;
    private readonly IPermissionPolicy _policy;
    private readonly ICapabilityAttestor _attestor;
    private readonly IApprovalReplayGuard _replayGuard;
    private readonly IExecutionTelemetry? _telemetry;

    public ToolExecutor(
        IToolRegistry registry,
        IPermissionPolicy policy,
        ICapabilityAttestor? attestor = null,
        IApprovalReplayGuard? replayGuard = null,
        IExecutionTelemetry? telemetry = null)
    {
        _registry = registry;
        _policy = policy;
        _attestor = attestor ?? new CapabilityAttestor();
        _replayGuard = replayGuard ?? new InMemoryApprovalReplayGuard();
        _telemetry = telemetry;
    }

    public Task<ToolExecutionResult> ExecuteAsync(
        ToolCall call,
        bool confirmed = false,
        CancellationToken cancellationToken = default) =>
        ExecuteInternalAsync(call, null, cancellationToken);

    public Task<ToolExecutionResult> ExecuteAsync(
        ToolCall call,
        ApprovalBinding approval,
        CancellationToken cancellationToken = default) =>
        ExecuteInternalAsync(call, approval, cancellationToken);

    private async Task<ToolExecutionResult> ExecuteInternalAsync(
        ToolCall call,
        ApprovalBinding? approval,
        CancellationToken cancellationToken)
    {
        var requestId = approval?.RequestId ?? Guid.NewGuid().ToString("N");
        var tool = _registry.Get(call.Name);
        if (tool is null)
        {
            var blocked = new ToolExecutionResult(
                new ToolResult(call.Name, false, $"Unknown tool: {call.Name}"),
                false,
                false,
                new PolicyDecision(false, "TOOL_NOT_FOUND", "Tool is not registered."));
            _telemetry?.Record(new(requestId, null, ExecutionEventType.Blocked, DateTimeOffset.UtcNow, "TOOL_NOT_FOUND", blocked.Result.Content, new Dictionary<string, object?>()));
            return blocked;
        }

        var attestation = _attestor.Attest(tool.Definition);
        _telemetry?.Record(new(
            requestId, null, ExecutionEventType.CapabilityAttested, DateTimeOffset.UtcNow,
            attestation.Status.ToString(), $"Capability {attestation.Name} attested.",
            new Dictionary<string, object?> { ["fingerprint"] = attestation.SchemaFingerprint }));

        if (_policy.RequiresConfirmation(tool.Definition) && approval is null)
        {
            var confirmation = new ToolExecutionResult(
                new ToolResult(call.Name, false, "Tool execution requires an explicit approval binding.", true),
                false,
                true,
                new PolicyDecision(false, "PERMISSION_POLICY_DENIED", "Permission policy requires explicit confirmation before this tool can execute.", true));
            _telemetry?.Record(new(
                requestId, null, ExecutionEventType.ApprovalRejected, DateTimeOffset.UtcNow,
                "PERMISSION_POLICY_DENIED", confirmation.Result.Content,
                new Dictionary<string, object?>()));
            return confirmation;
        }

        var action = tool.Definition.Permission switch
        {
            ToolPermission.Destructive => ActionClass.Mutate,
            ToolPermission.Write => ActionClass.Write,
            _ => ActionClass.Read
        };

        var request = new ExecutionRequest(
            action,
            call.Name,
            call.Target ?? call.Name,
            call.Scope ?? "unspecified",
            call.IntendedEffect ?? tool.Definition.Description,
            RequestId: requestId);

        var decision = AdaptiveExecutionPolicy.Evaluate(request, attestation.Status, approval);
        if (!decision.Allowed)
        {
            var blocked = new ToolExecutionResult(
                new ToolResult(call.Name, false, decision.Reason, decision.RequiresApproval),
                false,
                decision.RequiresApproval,
                decision);
            _telemetry?.Record(new(requestId, null, ExecutionEventType.Blocked, DateTimeOffset.UtcNow, decision.Code, decision.Reason, new Dictionary<string, object?>()));
            return blocked;
        }

        // Re-attest immediately before any mutating execution to close the
        // capability TOCTOU window between approval and the side effect.
        var preExecutionTool = _registry.Get(call.Name);
        if (preExecutionTool is null)
        {
            var changed = new ToolExecutionResult(
                new ToolResult(call.Name, false, "Tool disappeared or changed before execution.", true),
                false,
                true,
                new PolicyDecision(false, "TOOL_CAPABILITY_CHANGED", "Tool capability could not be revalidated before execution."));
            _telemetry?.Record(new(requestId, null, ExecutionEventType.Blocked, DateTimeOffset.UtcNow,
                "TOOL_CAPABILITY_CHANGED", changed.Result.Content, new Dictionary<string, object?>()));
            return changed;
        }

        var preExecutionAttestation = _attestor.Attest(preExecutionTool.Definition);
        if (!string.Equals(attestation.SchemaFingerprint, preExecutionAttestation.SchemaFingerprint, StringComparison.Ordinal) ||
            attestation.Permission != preExecutionAttestation.Permission)
        {
            var changed = new ToolExecutionResult(
                new ToolResult(call.Name, false, "Tool capability changed after authorization and before execution.", true),
                false,
                true,
                new PolicyDecision(false, "TOOL_CAPABILITY_CHANGED", "Tool schema or permission changed after authorization; reapproval is required."));
            _telemetry?.Record(new(requestId, null, ExecutionEventType.Blocked, DateTimeOffset.UtcNow,
                "TOOL_CAPABILITY_CHANGED", changed.Result.Content,
                new Dictionary<string, object?> {
                    ["beforeFingerprint"] = attestation.SchemaFingerprint,
                    ["afterFingerprint"] = preExecutionAttestation.SchemaFingerprint
                }));
            return changed;
        }

        tool = preExecutionTool;
        attestation = preExecutionAttestation;

        if (approval is not null &&
            action is ActionClass.Write or ActionClass.Mutate or ActionClass.Irreversible &&
            approval.ExpiresAt is not null &&
            !_replayGuard.TryConsume(approval.Nonce, approval.ExpiresAt.Value))
        {
            var replay = new ToolExecutionResult(
                new ToolResult(call.Name, false, "Approval nonce has already been consumed or is invalid.", true),
                false,
                true,
                new PolicyDecision(false, "APPROVAL_REPLAY", "Approval is stale, expired, or already consumed."));
            _telemetry?.Record(new(requestId, null, ExecutionEventType.Blocked, DateTimeOffset.UtcNow, "APPROVAL_REPLAY", replay.Result.Content, new Dictionary<string, object?>()));
            return replay;
        }

        _telemetry?.Record(new(requestId, null, ExecutionEventType.Executing, DateTimeOffset.UtcNow, "EXECUTING", call.Name, new Dictionary<string, object?>()));
        var result = await tool.ExecuteAsync(call.Arguments, cancellationToken);
        var evidence = new EvidenceItem(
            $"ev:{Guid.NewGuid():N}",
            EvidenceKind.ToolResult,
            call.Name,
            result.Content,
            DateTimeOffset.UtcNow,
            Trusted: false);

        _telemetry?.Record(new(
            requestId, null,
            result.Success ? ExecutionEventType.Observed : ExecutionEventType.Failed,
            DateTimeOffset.UtcNow,
            result.Success ? "TOOL_RESULT_OBSERVED" : "TOOL_RESULT_FAILED",
            result.Content,
            new Dictionary<string, object?> { ["evidenceId"] = evidence.Id }));

        return new(result, true, false, decision, evidence);
    }
}
