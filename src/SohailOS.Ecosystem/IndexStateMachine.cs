namespace SohailOS.Ecosystem;

public static class IndexStateMachine
{
    private static readonly IReadOnlyDictionary<KnowledgeIndexState, KnowledgeIndexState[]> Allowed =
        new Dictionary<KnowledgeIndexState, KnowledgeIndexState[]>
        {
            [KnowledgeIndexState.Discovered] = [KnowledgeIndexState.Authorized, KnowledgeIndexState.FailedPermanent],
            [KnowledgeIndexState.Authorized] = [KnowledgeIndexState.Queued, KnowledgeIndexState.FailedPermanent],
            [KnowledgeIndexState.Queued] = [KnowledgeIndexState.Fetching, KnowledgeIndexState.FailedRetryable],
            [KnowledgeIndexState.Fetching] = [KnowledgeIndexState.Parsing, KnowledgeIndexState.FailedRetryable, KnowledgeIndexState.Partial],
            [KnowledgeIndexState.Parsing] = [KnowledgeIndexState.Chunking, KnowledgeIndexState.FailedPermanent, KnowledgeIndexState.Partial],
            [KnowledgeIndexState.Chunking] = [KnowledgeIndexState.Embedding, KnowledgeIndexState.Upserting, KnowledgeIndexState.Partial],
            [KnowledgeIndexState.Embedding] = [KnowledgeIndexState.Upserting, KnowledgeIndexState.Partial, KnowledgeIndexState.FailedRetryable],
            [KnowledgeIndexState.Upserting] = [KnowledgeIndexState.Committed, KnowledgeIndexState.Partial, KnowledgeIndexState.FailedRetryable],
            [KnowledgeIndexState.Committed] = [KnowledgeIndexState.Superseded, KnowledgeIndexState.Tombstoned],
            [KnowledgeIndexState.FailedRetryable] = [KnowledgeIndexState.Queued, KnowledgeIndexState.Partial, KnowledgeIndexState.FailedPermanent],
            [KnowledgeIndexState.FailedPermanent] = [KnowledgeIndexState.Tombstoned],
            [KnowledgeIndexState.Superseded] = [KnowledgeIndexState.Tombstoned],
            [KnowledgeIndexState.Partial] = [KnowledgeIndexState.Queued, KnowledgeIndexState.Committed, KnowledgeIndexState.Tombstoned],
            [KnowledgeIndexState.Tombstoned] = []
        };

    public static void EnsureTransition(KnowledgeIndexState from, KnowledgeIndexState to)
    {
        if (!Allowed.TryGetValue(from, out var targets) || !targets.Contains(to))
            throw new InvalidOperationException($"Invalid index-state transition: {from} -> {to}.");
    }

    public static bool IsTerminal(KnowledgeIndexState state) =>
        state is KnowledgeIndexState.Committed
            or KnowledgeIndexState.FailedPermanent
            or KnowledgeIndexState.Superseded
            or KnowledgeIndexState.Tombstoned;
}
