namespace SohailOS.Core;

public enum EvidenceKind
{
    ToolResult,
    Observation,
    Verification,
    Validation,
    Audit
}

public sealed record EvidenceItem(
    string Id,
    EvidenceKind Kind,
    string Source,
    string Content,
    DateTimeOffset CreatedAt,
    string? ParentEvidenceId = null,
    bool Trusted = false);

public sealed record VerificationResult(
    VerificationStatus Status,
    string Code,
    string Reason,
    IReadOnlyList<string> EvidenceIds);

public sealed record ValidationResult(
    ValidationStatus Status,
    string Code,
    string Reason,
    IReadOnlyList<string> EvidenceIds);

public interface IExecutionVerifier
{
    VerificationResult Verify(
        ExecutionRequest request,
        ToolExecutionResult result,
        IReadOnlyCollection<EvidenceItem> evidence);
}

public interface IExecutionValidator
{
    ValidationResult Validate(
        ExecutionRequest request,
        VerificationResult verification,
        IReadOnlyCollection<EvidenceItem> evidence);
}

public sealed class BasicExecutionVerifier : IExecutionVerifier
{
    public VerificationResult Verify(
        ExecutionRequest request,
        ToolExecutionResult result,
        IReadOnlyCollection<EvidenceItem> evidence)
    {
        if (!result.Executed)
            return new(VerificationStatus.Failed, "NOT_EXECUTED", "Execution did not occur.", []);

        var matching = evidence.Where(e => e.ParentEvidenceId == result.Evidence?.Id).Select(e => e.Id).ToArray();
        if (result.Result.Success && result.Evidence is not null)
            return new(VerificationStatus.Verified, "OBSERVED_TOOL_RESULT", "Tool success was independently observed as evidence.", matching.Append(result.Evidence.Id).ToArray());

        return new(VerificationStatus.Failed, "RESULT_NOT_VERIFIED", "Tool output is not sufficient evidence of the requested outcome.", matching);
    }
}

public sealed class InvariantValidator : IExecutionValidator
{
    public ValidationResult Validate(
        ExecutionRequest request,
        VerificationResult verification,
        IReadOnlyCollection<EvidenceItem> evidence)
    {
        if (verification.Status != VerificationStatus.Verified)
            return new(ValidationStatus.Failed, "VERIFICATION_REQUIRED", "Validation cannot pass before independent verification.", verification.EvidenceIds);

        return new(ValidationStatus.Validated, "INVARIANTS_PASS", "Required execution invariants passed.", verification.EvidenceIds);
    }
}


public enum MemoryDurability
{
    Ephemeral,
    Session,
    Durable
}

public sealed class MemoryTransactionContract
{
    private readonly string _key;
    private readonly ApprovalBinding _binding;
    private readonly MemoryDurability _durability;

    public MemoryTransactionContract(
        string key,
        ApprovalBinding binding,
        MemoryDurability durability = MemoryDurability.Durable)
    {
        _key = key;
        _binding = binding;
        _durability = durability;
    }

    public bool IsValid(DateTimeOffset now, string requestId = "")
    {
        if (_durability != MemoryDurability.Durable ||
            string.IsNullOrWhiteSpace(_binding.Principal) ||
            string.IsNullOrWhiteSpace(_binding.Digest) ||
            !_binding.MatchesDigest() ||
            string.IsNullOrWhiteSpace(_binding.Nonce) ||
            _binding.IssuedAt is null ||
            _binding.ExpiresAt is null ||
            _binding.IssuedAt > now ||
            _binding.ExpiresAt <= now ||
            !string.Equals(_binding.Operation, "memory_write", StringComparison.Ordinal) ||
            !string.Equals(_binding.Target, "SohailOS.Memory", StringComparison.Ordinal) ||
            !string.Equals(_binding.Scope, _key, StringComparison.Ordinal) ||
            !string.Equals(_binding.IntendedEffect, "persist memory", StringComparison.Ordinal) ||
            (!string.IsNullOrWhiteSpace(_binding.RequestId) &&
             !string.Equals(_binding.RequestId, requestId, StringComparison.Ordinal)))
            return false;

        return string.IsNullOrWhiteSpace(_binding.ScopeHash) ||
               string.Equals(_binding.ScopeHash, ApprovalBinding.ScopeFingerprint(_key), StringComparison.Ordinal);
    }

    public async Task PersistAsync(
        IMemoryStore store,
        string value,
        string requestId = "",
        CancellationToken cancellationToken = default)
    {
        if (!IsValid(DateTimeOffset.UtcNow, requestId))
            throw new InvalidOperationException("Durable memory transaction has an invalid or expired approval binding.");

        await store.SaveAsync(_key, value, cancellationToken);
    }
}
