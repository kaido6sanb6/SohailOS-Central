namespace SohailOS.Ecosystem;

public enum RepositoryIntegrationMode
{
    Logical,
    Adapter,
    Plugin,
    Package,
    Service,
    ReferenceOnly,
    PhysicalMerge
}

public sealed record RepositoryIntegrationDescriptor(
    string Repository,
    RepositoryIntegrationMode Mode,
    IReadOnlyCollection<string> Capabilities,
    IReadOnlyCollection<string> Roles,
    KnowledgeTrustTier TrustTier,
    string? Upstream,
    string? LicenseStatus,
    bool AutoDiscoverFutureForks);

public static class RepositoryIntegrationPolicy
{
    public static RepositoryIntegrationDescriptor DefaultFor(
        string repository,
        KnowledgeTrustTier trustTier = KnowledgeTrustTier.Unverified)
        => new(
            repository,
            RepositoryIntegrationMode.Logical,
            Array.Empty<string>(),
            new[] { "KNOWLEDGE_SOURCE" },
            trustTier,
            null,
            null,
            true);
}
