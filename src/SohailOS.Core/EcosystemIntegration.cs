namespace SohailOS.Core;

public enum ExternalTransport
{
    Mcp,
    OpenAiCompatible,
    Http,
    Workflow,
    LocalProcess
}

public sealed record ExternalCapabilityDescriptor(
    string Source,
    string Capability,
    string Version,
    ExternalTransport Transport,
    ToolPermission Permission,
    bool TrustedSource,
    string Provenance = "external");

public sealed record ExternalPolicyDecision(
    bool Allowed,
    string Code,
    string Reason,
    bool RequiresApproval = false);

public static class ExternalEcosystemPolicy
{
    public static ExternalPolicyDecision Evaluate(
        ExternalCapabilityDescriptor descriptor,
        ActionClass action,
        bool approved)
    {
        if (!descriptor.TrustedSource && action is ActionClass.Write or ActionClass.Mutate or ActionClass.Irreversible)
            return new(false, "EXTERNAL_SOURCE_UNTRUSTED", "Untrusted external capabilities cannot perform mutating actions.");

        if (action is ActionClass.Write or ActionClass.Mutate or ActionClass.Irreversible)
        {
            if (!approved)
                return new(false, "EXTERNAL_APPROVAL_REQUIRED", "External mutation requires explicit approval bound to the operation and target.", true);

            return new(true, "EXTERNAL_MUTATION_APPROVED", "External mutation is approved.");
        }

        return new(true, "REMOTE_READ_ALLOWED", "Attested external read/analyze capability is allowed.");
    }
}

public interface IExternalCapabilitySource
{
    ExternalCapabilityDescriptor Descriptor { get; }
}

public interface IExternalWorkflowInvoker
{
    Task<ToolResult> InvokeAsync(
        ExternalCapabilityDescriptor capability,
        IReadOnlyDictionary<string, object?> input,
        ApprovalBinding? approval = null,
        CancellationToken cancellationToken = default);
}
