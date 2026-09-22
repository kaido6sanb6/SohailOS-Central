namespace SohailOS.Ecosystem;

public enum KnowledgeContentClass
{
    Knowledge,
    UntrustedData
}

public sealed record KnowledgePolicyDecision(
    KnowledgeContentClass ContentClass,
    KnowledgeTrustTier TrustTier,
    bool MayOverrideAgentInstructions,
    bool MayBeRetrieved,
    bool RequiresContainment,
    IReadOnlyCollection<string> Flags);

public static class UniversalKnowledgePolicy
{
    public static KnowledgePolicyDecision Classify(
        string text,
        KnowledgeTrustTier trustTier)
    {
        var suspected = InstructionContainment.IsSuspected(text);

        if (suspected || trustTier is KnowledgeTrustTier.Unverified or KnowledgeTrustTier.ReferenceOnly)
        {
            return new(
                KnowledgeContentClass.UntrustedData,
                trustTier,
                false,
                trustTier != KnowledgeTrustTier.Tombstoned,
                true,
                suspected
                    ? new[] { "instruction-like-content", "data-not-instructions" }
                    : new[] { "unverified-source", "data-not-instructions" });
        }

        return new(
            KnowledgeContentClass.Knowledge,
            trustTier,
            false,
            trustTier != KnowledgeTrustTier.Tombstoned,
            false,
            new[] { "data-not-instructions" });
    }
}
