using SohailOS.Core;

namespace SohailOS.Agents;

public sealed class Orchestrator : IOrchestrator
{
    private readonly IReadOnlyDictionary<SohailModule, IModuleAgent> _agents;

    public Orchestrator(IEnumerable<IModuleAgent> agents)
    {
        _agents = agents.ToDictionary(a => a.Module);
    }

    public async Task<AgentResponse> HandleAsync(UserRequest request, CancellationToken cancellationToken = default)
    {
        var route = Route(request.Text);
        if (!_agents.TryGetValue(route.PrimaryModule, out var agent))
            throw new InvalidOperationException($"No agent registered for {route.PrimaryModule}.");

        var content = await agent.ExecuteAsync(request, cancellationToken);
        return new AgentResponse(content, route, DateTimeOffset.UtcNow,
            new Dictionary<string, object?> { ["provider"] = "configured-provider" });
    }

    private static RouteDecision Route(string text)
    {
        var t = text.ToLowerInvariant();
        if (ContainsAny(t, "spss", "رگرسیون", "آمار", "متغیر", "داده"))
            return new(SohailModule.Stats, [SohailModule.Research], "Statistical/data task detected.", .90);
        if (ContainsAny(t, "سینما", "فیلم", "موج نو", "فرانکفورت"))
            return new(SohailModule.Cinema, [SohailModule.Sociology, SohailModule.Think], "Cinema/cultural analysis detected.", .88);
        if (ContainsAny(t, "کد", "برنامه", "api", "c#", "python", "گیتهاب"))
            return new(SohailModule.Code, [SohailModule.Ai, SohailModule.Product], "Software task detected.", .86);
        if (ContainsAny(t, "اپلیکیشن", "محصول", "mvp", "کاربر", "ux"))
            return new(SohailModule.Product, [SohailModule.Strategy, SohailModule.Code], "Product task detected.", .86);
        if (ContainsAny(t, "پژوهش", "مقاله", "پایان نامه", "متاآنالیز", "فرضیه"))
            return new(SohailModule.Research, [SohailModule.Stats], "Research task detected.", .84);
        if (ContainsAny(t, "هگل", "مارکس", "مارکسیسم", "دیالکتیک", "فلسفه"))
            return new(SohailModule.Think, [SohailModule.Sociology], "Philosophy/social theory task detected.", .84);
        return new(SohailModule.Think, [], "General reasoning fallback.", .55);
    }

    private static bool ContainsAny(string text, params string[] terms) => terms.Any(text.Contains);
}
