namespace SohailOS.Core;

public enum MutationTransactionState
{
    Created,
    Previewed,
    VerifierScheduled,
    Executed,
    Verified,
    Failed
}

public sealed record MutationExecutionResult(
    bool Executed,
    string BindingDigest,
    VerificationStatus VerificationStatus,
    string Code);

public sealed class MutationTransactionContract
{
    private readonly ApprovalBinding _approval;
    private readonly ExecutionRequest _request;
    private readonly IApprovalReplayGuard _replayGuard;
    private Func<Task<bool>>? _verifier;
    private MutationTransactionState _state = MutationTransactionState.Created;

    public MutationTransactionContract(
        ApprovalBinding approval,
        ExecutionRequest request,
        IApprovalReplayGuard? replayGuard = null)
    {
        ArgumentNullException.ThrowIfNull(approval);
        ArgumentNullException.ThrowIfNull(request);
        _approval = approval;
        _request = request;
        _replayGuard = replayGuard ?? new InMemoryApprovalReplayGuard();
    }

    public MutationTransactionState State => _state;

    public void Preview()
    {
        EnsureState(MutationTransactionState.Created);
        if (string.IsNullOrWhiteSpace(_approval.Principal) ||
            string.IsNullOrWhiteSpace(_approval.Digest) ||
            !_approval.MatchesDigest())
            throw new InvalidOperationException("Mutation approval binding is incomplete or has an invalid digest.");
        var now = DateTimeOffset.UtcNow;
        if (_approval.ExpiresAt is null || _approval.ExpiresAt <= now)
            throw new InvalidOperationException("Mutation approval binding is expired.");
        if (!_approval.Matches(_request, now))
            throw new InvalidOperationException("Mutation approval binding does not match the requested operation, target, scope, effect, or request identity.");
        _state = MutationTransactionState.Previewed;
    }

    public void ScheduleIndependentVerifier(Func<Task<bool>> verifier)
    {
        ArgumentNullException.ThrowIfNull(verifier);
        if (_state != MutationTransactionState.Previewed)
            throw new InvalidOperationException("Mutation verifier must be scheduled after preview and before execution.");
        _verifier = verifier;
        _state = MutationTransactionState.VerifierScheduled;
    }

    public async Task<MutationExecutionResult> ExecuteAsync(Func<Task> mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        if (_state != MutationTransactionState.VerifierScheduled || _verifier is null)
            throw new InvalidOperationException("Mutation requires preview and a scheduled independent verifier before execution.");
        if (!_replayGuard.TryConsume(_approval.Nonce, _approval.ExpiresAt!.Value))
        {
            _state = MutationTransactionState.Failed;
            throw new InvalidOperationException("Mutation approval nonce has already been consumed or is invalid.");
        }

        try
        {
            await mutation();
            _state = MutationTransactionState.Executed;
            return new(true, _approval.Digest, VerificationStatus.Unknown, "EXECUTED_NOT_VERIFIED");
        }
        catch
        {
            _state = MutationTransactionState.Failed;
            throw;
        }
    }

    public async Task<VerificationStatus> VerifyAsync()
    {
        if (_state != MutationTransactionState.Executed || _verifier is null)
            throw new InvalidOperationException("Only an executed mutation can be independently verified.");

        var verified = await _verifier();
        _state = verified ? MutationTransactionState.Verified : MutationTransactionState.Failed;
        return verified ? VerificationStatus.Verified : VerificationStatus.Failed;
    }

    private void EnsureState(MutationTransactionState expected)
    {
        if (_state != expected)
            throw new InvalidOperationException($"Invalid mutation transaction state: {_state}; expected {expected}.");
    }
}
