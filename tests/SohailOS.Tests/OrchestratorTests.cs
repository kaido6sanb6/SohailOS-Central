using SohailOS.Agents;
using SohailOS.AI;
using SohailOS.Core;
using Xunit;

namespace SohailOS.Tests;

public class OrchestratorTests
{
    private static Orchestrator CreateOrchestrator()
    {
        var provider = new StubAiProvider();
        var agents = Enum.GetValues<SohailModule>()
            .Select(m => (IModuleAgent)new ModuleAgent(m, provider, "test"));
        return new Orchestrator(agents);
    }

    [Fact]
    public async Task RoutesResearchRequestToResearchModule()
    {
        var result = await CreateOrchestrator().HandleAsync(
            new UserRequest("برای پایان نامه یک فرضیه پژوهشی طراحی کن", DateTimeOffset.UtcNow));
        Assert.Equal(SohailModule.Research, result.Route.PrimaryModule);
    }

    [Fact]
    public async Task RoutesStatisticsRequestToStatsModule()
    {
        var result = await CreateOrchestrator().HandleAsync(
            new UserRequest("در SPSS رگرسیون را چگونه تحلیل کنم؟", DateTimeOffset.UtcNow));
        Assert.Equal(SohailModule.Stats, result.Route.PrimaryModule);
    }

    [Fact]
    public async Task RoutesCinemaRequestToCinemaModule()
    {
        var result = await CreateOrchestrator().HandleAsync(
            new UserRequest("تأثیر مکتب فرانکفورت بر موج نو فرانسه در سینما", DateTimeOffset.UtcNow));
        Assert.Equal(SohailModule.Cinema, result.Route.PrimaryModule);
    }

    [Fact]
    public async Task RoutesProductRequestToProductModule()
    {
        var result = await CreateOrchestrator().HandleAsync(
            new UserRequest("برای MVP اپلیکیشن اجتماعی PRD طراحی کن", DateTimeOffset.UtcNow));
        Assert.Equal(SohailModule.Product, result.Route.PrimaryModule);
    }

    [Fact]
    public async Task RoutesGeneralQuestionToThinkModule()
    {
        var result = await CreateOrchestrator().HandleAsync(
            new UserRequest("یک مسئله کاملاً عمومی را تحلیل کن", DateTimeOffset.UtcNow));
        Assert.Equal(SohailModule.Think, result.Route.PrimaryModule);
    }
}
