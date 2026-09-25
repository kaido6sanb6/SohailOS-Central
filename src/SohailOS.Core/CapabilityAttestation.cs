using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SohailOS.Core;

public sealed record CapabilityAttestation(
    string CapabilityId,
    string Name,
    string Version,
    string SchemaFingerprint,
    ToolPermission Permission,
    CapabilityStatus Status,
    string Provenance,
    DateTimeOffset ObservedAt,
    DateTimeOffset? ExpiresAt = null)
{
    public bool IsUsable(DateTimeOffset now) =>
        Status == CapabilityStatus.Verified &&
        (ExpiresAt is null || ExpiresAt > now);

    public CapabilityAttestationContract ToContract(string evidenceDigest) =>
        new(CapabilityId, Version, SchemaFingerprint, [Permission.ToString()], evidenceDigest, ObservedAt, ExpiresAt);
}

public interface ICapabilityAttestor
{
    CapabilityAttestation Attest(ToolDefinition definition, DateTimeOffset? now = null);
}

public sealed class CapabilityAttestor : ICapabilityAttestor
{
    private readonly TimeSpan _ttl;

    public CapabilityAttestor(TimeSpan? ttl = null) => _ttl = ttl ?? TimeSpan.FromMinutes(10);

    public CapabilityAttestation Attest(ToolDefinition definition, DateTimeOffset? now = null)
    {
        var observed = now ?? DateTimeOffset.UtcNow;
        var schema = JsonSerializer.Serialize(new
        {
            definition.Name,
            definition.Description,
            definition.Permission,
            definition.Parameters
        });
        var fingerprint = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(schema))).ToLowerInvariant();

        return new CapabilityAttestation(
            CapabilityId: $"tool:{definition.Name.ToLowerInvariant()}",
            Name: definition.Name,
            Version: fingerprint[..12],
            SchemaFingerprint: fingerprint,
            Permission: definition.Permission,
            Status: CapabilityStatus.Verified,
            Provenance: "runtime-tool-registry",
            ObservedAt: observed,
            ExpiresAt: observed.Add(_ttl));
    }
}

public sealed record CapabilityAttestationContract(
    string CapabilityId,
    string Version,
    string SchemaFingerprint,
    IReadOnlyCollection<string> Permissions,
    string EvidenceDigest,
    DateTimeOffset IssuedAt,
    DateTimeOffset? ExpiresAt)
{
    public bool IsValid() =>
        !string.IsNullOrWhiteSpace(CapabilityId) &&
        !string.IsNullOrWhiteSpace(Version) &&
        !string.IsNullOrWhiteSpace(SchemaFingerprint) &&
        Permissions is { Count: > 0 } &&
        Permissions.All(x => !string.IsNullOrWhiteSpace(x)) &&
        !string.IsNullOrWhiteSpace(EvidenceDigest) &&
        ExpiresAt is not null &&
        IssuedAt < ExpiresAt;

    public bool IsFresh(DateTimeOffset now) =>
        IsValid() &&
        IssuedAt <= now &&
        ExpiresAt is not null &&
        ExpiresAt > now;

    public string Digest()
    {
        var permissions = string.Join(",", Permissions.OrderBy(x => x, StringComparer.Ordinal));
        var canonical = string.Join("\\n",
            CapabilityId,
            Version,
            SchemaFingerprint,
            permissions,
            EvidenceDigest,
            IssuedAt.UtcDateTime.ToString("O"),
            ExpiresAt?.UtcDateTime.ToString("O") ?? string.Empty);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}

public sealed class CapabilityRegistryContract
{
    private readonly Dictionary<string, CapabilityAttestationContract> _attestations =
        new(StringComparer.Ordinal);

    public void Register(CapabilityAttestationContract attestation)
    {
        ArgumentNullException.ThrowIfNull(attestation);
        if (!attestation.IsValid())
            throw new ArgumentException("Capability attestation is structurally invalid.", nameof(attestation));

        _attestations[attestation.CapabilityId] = attestation;
    }

    public CapabilityAttestationContract? Lookup(string capabilityId) =>
        _attestations.TryGetValue(capabilityId, out var value) ? value : null;

    public CapabilityAttestationContract? Probe(string capabilityId, DateTimeOffset now)
    {
        var value = Lookup(capabilityId);
        return value is not null && value.IsFresh(now) ? value : null;
    }

    public IReadOnlyCollection<CapabilityAttestationContract> ListActive(DateTimeOffset now) =>
        _attestations.Values.Where(x => x.IsFresh(now)).ToArray();
}
