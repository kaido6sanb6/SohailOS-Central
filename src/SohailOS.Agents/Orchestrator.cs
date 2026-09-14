using SohailOS.Core;

namespace SohailOS.Agents;

public sealed class Orchestrator : IOrchestrator
{
    private readonly IReadOnlyDictionary<SohailModule, IModuleAgent> _agents;
    private readonly IContextBuilder? _contextBuilder;
    private readonly IConversationStore? _conversationStore;

    private static readonly IReadOnlyDictionary<SohailModule, string[]> Keywords =
        new Dictionary<SohailModule, string[]>
        {
            [SohailModule.Stats] = ["spss", "رگرسیون", "anova", "همبستگی", "آمار", "متغیر", "داده", "پایایی"],
            [SohailModule.Cinema] = ["سینما", "فیلم", "کارگردان", "موج نو", "فرانکفورت", "فرم فیلم"],
            [SohailModule.Code] = ["کد", "برنامه", "api", "c#", "python", "گیتهاب", "github", "دیباگ", "خطا"],
            [SohailModule.Product] = ["اپلیکیشن", "محصول", "mvp", "کاربر", "ux", "ui", "prd", "استارتاپ"],
            [SohailModule.Research] = ["پژوهش", "مقاله", "پایان نامه", "متاآنالیز", "فرضیه", "مرور نظام مند", "prisma", "پرسش پژوهش"],
            [SohailModule.Think] = ["هگل", "مارکس", "مارکسیسم", "دیالکتیک", "فلسفه", "ماکیاولی", "ایدئولوژی", "ازخودبیگانگی"],
            [SohailModule.Sociology] = ["جامعه شناسی", "جامعه", "طبقه", "هنجار", "نهاد", "اجتماعی", "قدرت"],
            [SohailModule.Ai] = ["هوش مصنوعی", "مدل زبانی", "llm", "ai", "پرامپت", "عامل", "agent", "embedding"],
            [SohailModule.Office] = ["اکسل", "excel", "word", "پاورپوینت", "office", "فرمول", "جدول"],
            [SohailModule.Operations] = ["اتوماسیون", "فرایند", "عملیات", "وظیفه", "todoist", "notion"],
            [SohailModule.Strategy] = ["استراتژی", "تصمیم", "مزیت رقابتی", "ریسک", "اولویت", "کسب و کار"],
            [SohailModule.Learning] = ["یادگیری", "آموزش", "درس", "تمرین", "مسیر یادگیری", "مطالعه"]
        };

    public Orchestrator(
        IEnumerable<IModuleAgent> agents,
        IContextBuilder? contextBuilder = null,
        IConversationStore? conversationStore = null)
    {
        _agents = agents.ToDictionary(a => a.Module);
        _contextBuilder = contextBuilder;
        _conversationStore = conversationStore;
    }

    public async Task<AgentResponse> HandleAsync(UserRequest request, CancellationToken cancellationToken = default)
    {
        var route = Route(request.Text);
        if (!_agents.TryGetValue(route.PrimaryModule, out var agent))
            throw new InvalidOperationException($"No agent registered for {route.PrimaryModule}.");

        if (_conversationStore is not null)
            await _conversationStore.AppendAsync(
                new ConversationTurn("user", request.Text, request.CreatedAt), cancellationToken);

        var executionRequest = request;
        if (_contextBuilder is not null)
        {
            var context = await _contextBuilder.BuildAsync(request, route, cancellationToken);
            executionRequest = request with { Text = context };
        }

        var content = await agent.ExecuteAsync(executionRequest, cancellationToken);

        if (_conversationStore is not null)
            await _conversationStore.AppendAsync(
                new ConversationTurn("assistant", content, DateTimeOffset.UtcNow), cancellationToken);

        return new AgentResponse(content, route, DateTimeOffset.UtcNow,
            new Dictionary<string, object?> { ["provider"] = "configured-provider" });
    }

    private static RouteDecision Route(string text)
    {
        var normalized = Normalize(text);
        var scores = Keywords.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Count(term => normalized.Contains(Normalize(term), StringComparison.Ordinal)));

        var ranked = scores.Where(x => x.Value > 0)
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Key)
            .ToList();

        if (ranked.Count == 0)
            return new(SohailModule.Think, [], "No specialist signal detected; general reasoning fallback.", .55);

        var primary = ranked[0].Key;
        var score = ranked[0].Value;
        var supporting = ranked.Skip(1).Take(2).Select(x => x.Key).ToArray();
        var confidence = Math.Min(.96, .55 + (.10 * score) + (supporting.Length > 0 ? .05 : 0));

        return new(primary, supporting, $"Matched {score} keyword signal(s) for {primary}.", confidence);
    }

    private static string Normalize(string value) => value
        .ToLowerInvariant()
        .Replace('ي', 'ی')
        .Replace('ك', 'ک')
        .Replace('\u200c', ' ')
        .Trim();
}
