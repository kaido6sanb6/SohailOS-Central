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
}
