using SohailOS.Core;

namespace SohailOS.Agents;

public enum LifecycleStage
{
    REQUEST,
    CLASSIFY,
    CAPABILITY_DISCOVERY,
    LEAST_PRIVILEGE_CHECK,
    TASK_GRAPH,
    PREVIEW_DRY_RUN,
    APPROVAL_BINDING,
    EXECUTE,
    RECONCILE,
    INDEPENDENT_VERIFY,
    VALIDATE,
    TERMINAL
}

public enum InvariantViolation
{
    UNKNOWN,
    UNAVAILABLE,
    FORBIDDEN,
    STALE,
    CONFLICT,
    PARTIAL,
    TIMEOUT,
    TOCTOU,
    SUCCESS_NOT_VERIFIED,
    VERIFIED_NOT_VALIDATED
}

public sealed class OrchestrationBoundary
{
    private readonly AgentRuntime? _runtime;
    private LifecycleStage _current = LifecycleStage.REQUEST;

    public OrchestrationBoundary(AgentRuntime? runtime = null) => _runtime = runtime;

    public LifecycleStage Current => _current;

    public void Advance(LifecycleStage to)
    {
        if (to <= _current)
            throw new InvalidOperationException($"Lifecycle transition must be monotonic: {_current} -> {to}.");
        _current = to;
    }

    public async Task<string> RunAsync(
        string systemPrompt,
        string userPrompt,
        ApprovalBinding? approval = null,
        string memoryKey = "global",
        bool persistMemory = false,
        CancellationToken cancellationToken = default)
    {
        if (_runtime is null)
            throw new InvalidOperationException("AgentRuntime is required for governed orchestration.");

        Advance(LifecycleStage.CLASSIFY);
        Advance(LifecycleStage.CAPABILITY_DISCOVERY);
        Advance(LifecycleStage.LEAST_PRIVILEGE_CHECK);
        Advance(LifecycleStage.TASK_GRAPH);
        if (approval is not null) Advance(LifecycleStage.APPROVAL_BINDING);
        Advance(LifecycleStage.EXECUTE);

        // AgentRuntime performs its own observation/verification/validation.
        // This boundary deliberately does not promote its returned text to proof.
        return await _runtime.RunAsync(
            systemPrompt, userPrompt, approval, memoryKey, persistMemory, cancellationToken);
    }

    public void MarkReconciled() => Advance(LifecycleStage.RECONCILE);
    public void MarkIndependentlyVerified() => Advance(LifecycleStage.INDEPENDENT_VERIFY);
    public void MarkValidated() => Advance(LifecycleStage.VALIDATE);
    public void MarkTerminal() => Advance(LifecycleStage.TERMINAL);

    public static string ResponseFor(InvariantViolation violation) => violation switch
    {
        InvariantViolation.UNKNOWN => "BLOCK_AND_PROBE",
        InvariantViolation.UNAVAILABLE => "BLOCK",
        InvariantViolation.FORBIDDEN => "BLOCK",
        InvariantViolation.STALE => "REPROBE",
        InvariantViolation.CONFLICT => "RECONCILE_OR_BLOCK",
        InvariantViolation.PARTIAL => "STOP",
        InvariantViolation.TIMEOUT => "EXTERNAL_VERIFY",
        InvariantViolation.TOCTOU => "REVALIDATE_AND_REAPPROVE",
        InvariantViolation.SUCCESS_NOT_VERIFIED => "DO_NOT_ADVANCE",
        InvariantViolation.VERIFIED_NOT_VALIDATED => "DO_NOT_ADVANCE",
        _ => "BLOCK"
    };
}
