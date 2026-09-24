using SohailOS.Core;
using Xunit;

namespace SohailOS.Tests;

public sealed class EcosystemIntegrationContractTests
{
    [Fact]
    public void RemoteMcpReadCapability_IsAllowedWhenSourceIsAttested()
    {
        var descriptor = new ExternalCapabilityDescriptor(
            "smythos-sre",
            "remote.mcp",
            "1",
            ExternalTransport.Mcp,
            ToolPermission.ReadOnly,
            TrustedSource: true);

        var decision = ExternalEcosystemPolicy.Evaluate(descriptor, ActionClass.Read, approved: false);

        Assert.True(decision.Allowed);
        Assert.Equal("REMOTE_READ_ALLOWED", decision.Code);
    }

    [Fact]
    public void RemoteMcpMutation_RequiresApprovalEvenWhenSourceIsTrusted()
    {
        var descriptor = new ExternalCapabilityDescriptor(
            "n8n-chatgpt-mcp",
            "workflow.execute",
            "1",
            ExternalTransport.Mcp,
            ToolPermission.Write,
            TrustedSource: true);

        var decision = ExternalEcosystemPolicy.Evaluate(descriptor, ActionClass.Write, approved: false);

        Assert.False(decision.Allowed);
        Assert.Equal("EXTERNAL_APPROVAL_REQUIRED", decision.Code);
    }

    [Fact]
    public void UntrustedExternalCapability_CannotExecuteMutation()
    {
        var descriptor = new ExternalCapabilityDescriptor(
            "g4f-proxy",
            "llm.complete",
            "1",
            ExternalTransport.OpenAiCompatible,
            ToolPermission.ReadOnly,
            TrustedSource: false);

        var decision = ExternalEcosystemPolicy.Evaluate(descriptor, ActionClass.Mutate, approved: true);

        Assert.False(decision.Allowed);
        Assert.Equal("EXTERNAL_SOURCE_UNTRUSTED", decision.Code);
    }
}
