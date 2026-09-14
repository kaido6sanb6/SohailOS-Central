using SohailOS.Agents;
using SohailOS.AI;
using SohailOS.Core;
using Xunit;

namespace SohailOS.Tests;

public class OrchestratorTests
{
    [Fact]
    public async Task RoutesResearchRequestToResearchModule()
    {
        var provider = new StubAiProvider();
        var agents = Enum.GetValues<SohailModule>().Select(m => (IModuleAgent)new ModuleAgent(m, provider, "test"));
        var orchestrator = new Orchestrator(agents);
        var result = await orchestrator.HandleAsync(new UserRequest("برای پایان نامه یک فرضیه پژوهشی طراحی کن", DateTimeOffset.UtcNow));
        Assert.Equal(SohailModule.Research, result.Route.PrimaryModule);
    }

    [Fact]
    public async Task RoutesStatisticsRequestToStatsModule()
    {
        var provider = new StubAiProvider();
        var agents = Enum.GetValues<SohailModule>().Select(m => (IModuleAgent)new ModuleAgent(m, provider, "test"));
        var orchestrator = new Orchestrator(agents);
        var result = await orchestrator.HandleAsync(new UserRequest("در SPSS رگرسیون را چگونه تحلیل کنم؟", DateTimeOffset.UtcNow));
        Assert.Equal(SohailModule.Stats, result.Route.PrimaryModule);
    }
}
