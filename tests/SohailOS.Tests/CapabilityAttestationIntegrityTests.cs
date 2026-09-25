using SohailOS.Core;
using Xunit;

namespace SohailOS.Tests;

public sealed class CapabilityAttestationIntegrityTests
{
    [Fact]
    public void InvalidContract_IsNotValid()
    {
        var now = DateTimeOffset.UtcNow;
        var value = new CapabilityAttestationContract("", "", "", Array.Empty<string>(), "", now, now.AddMinutes(5));
        Assert.False(value.IsValid());
    }

    [Fact]
    public void Contract_RejectsNonIncreasingLifetime()
    {
        var now = DateTimeOffset.UtcNow;
        var value = new CapabilityAttestationContract("tool:test", "1", "schema", ["read"], "evidence", now, now);
        Assert.False(value.IsValid());
    }

    [Fact]
    public void Contract_DigestChangesWhenEvidenceChanges()
    {
        var now = DateTimeOffset.UtcNow;
        var value = new CapabilityAttestationContract("tool:test", "1", "schema", ["read"], "evidence", now, now.AddMinutes(5));
        var tampered = value with { EvidenceDigest = "tampered" };

        Assert.NotEqual(value.Digest(), tampered.Digest());
    }

    [Fact]
    public void Registry_RejectsInvalidAttestation()
    {
        var registry = new CapabilityRegistryContract();
        var now = DateTimeOffset.UtcNow;
        var value = new CapabilityAttestationContract("tool:test", "", "schema", ["read"], "evidence", now, now.AddMinutes(5));

        Assert.Throws<ArgumentException>(() => registry.Register(value));
    }

    [Fact]
    public void Registry_StillRejectsStaleAttestation()
    {
        var registry = new CapabilityRegistryContract();
        var now = DateTimeOffset.UtcNow;
        var stale = new CapabilityAttestationContract("tool:test", "1", "schema", ["read"], "evidence", now.AddMinutes(-2), now.AddMinutes(-1));

        registry.Register(stale);

        Assert.Null(registry.Probe("tool:test", now));
    }
}
