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
