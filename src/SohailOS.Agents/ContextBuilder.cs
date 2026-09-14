using System.Text;
using SohailOS.Core;

namespace SohailOS.Agents;

public sealed class ContextBuilder : IContextBuilder
{
    private readonly IConversationStore _conversationStore;
    private readonly IMemoryStore _memoryStore;
    private readonly int _recentTurns;

    public ContextBuilder(
        IConversationStore conversationStore,
        IMemoryStore memoryStore,
        int recentTurns = 8)
    {
        _conversationStore = conversationStore;
        _memoryStore = memoryStore;
        _recentTurns = Math.Max(0, recentTurns);
    }

    public async Task<string> BuildAsync(
        UserRequest request,
        RouteDecision route,
        CancellationToken cancellationToken = default)
    {
        var builder = new StringBuilder();
        builder.AppendLine("ROUTING");
        builder.AppendLine($"Primary module: {route.PrimaryModule}");
        builder.AppendLine($"Supporting modules: {string.Join(", ", route.SupportingModules)}");
        builder.AppendLine($"Reason: {route.Reason}");
        builder.AppendLine($"Confidence: {route.Confidence:0.00}");

        var recent = await _conversationStore.GetRecentAsync(_recentTurns, cancellationToken);
        if (recent.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("RECENT CONVERSATION");
            foreach (var turn in recent)
                builder.AppendLine($"{turn.Role}: {turn.Content}");
        }

        var lastResponse = await _memoryStore.GetAsync("last.response", cancellationToken);
        if (!string.IsNullOrWhiteSpace(lastResponse))
        {
            builder.AppendLine();
            builder.AppendLine("PERSISTED CONTEXT");
            builder.AppendLine($"last.response: {lastResponse}");
        }

        builder.AppendLine();
        builder.AppendLine("CURRENT REQUEST");
        builder.AppendLine(request.Text);
        return builder.ToString();
    }
}
