using SohailOS.Core;

namespace SohailOS.AI;

public sealed class KeyPoolCompletionProvider : IAiCompletionProvider, IAiProvider
{
    private readonly string _name;
    private readonly IReadOnlyList<IAiCompletionProvider> _providers;
    private int _nextIndex;

    public KeyPoolCompletionProvider(string name, IReadOnlyList<IAiCompletionProvider> providers)
    {
        if (providers.Count == 0) throw new InvalidOperationException($"No {name} API keys are configured.");
        _name = name;
        _providers = providers;
    }

    public string Name => $"{_name}:keys={_providers.Count}";

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
        => (await CompleteAsync(systemPrompt, userPrompt, Array.Empty<ToolDefinition>(), cancellationToken)).Content;

    public async Task<AiCompletion> CompleteAsync(string systemPrompt, string userPrompt, IReadOnlyCollection<ToolDefinition> tools, CancellationToken cancellationToken = default)
    {
        var start = Math.Abs(Interlocked.Increment(ref _nextIndex) - 1) % _providers.Count;
        Exception? lastError = null;

        for (var offset = 0; offset < _providers.Count; offset++)
        {
            var provider = _providers[(start + offset) % _providers.Count];
            try { return await provider.CompleteAsync(systemPrompt, userPrompt, tools, cancellationToken); }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                lastError = ex;
            }
        }

        throw lastError ?? new InvalidOperationException($"All {_name} API keys failed.");
    }
}
