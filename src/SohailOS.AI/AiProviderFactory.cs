using SohailOS.Core;

namespace SohailOS.AI;

public static class AiProviderFactory
{
    public static (IAiProvider Legacy, IAiCompletionProvider Completion) Create(HttpClient httpClient)
    {
        var provider = (Environment.GetEnvironmentVariable("SOHAILOS_AI_PROVIDER") ?? "openai")
            .Trim().ToLowerInvariant();

        return provider switch
        {
            "anthropic" or "claude" => CreateAnthropic(httpClient),
            "gemini" or "google" => CreateGemini(httpClient),
            "openai" => CreateOpenAi(httpClient),
            "local" or "openai-compatible" => CreateOpenAiCompatible(httpClient),
            "auto" => CreateAuto(httpClient),
            _ => throw new InvalidOperationException($"Unsupported SOHAILOS_AI_PROVIDER: {provider}")
        };
    }

    private static (IAiProvider, IAiCompletionProvider) CreateAuto(HttpClient httpClient)
    {
        var providers = new List<NamedProvider>();

        TryAdd(providers, "openai", () => CreateOpenAi(httpClient).Completion);
        TryAdd(providers, "anthropic", () => CreateAnthropic(httpClient).Completion);
        TryAdd(providers, "gemini", () => CreateGemini(httpClient).Completion);
        TryAdd(providers, "local", () => CreateOpenAiCompatible(httpClient).Completion);

        if (providers.Count == 0)
            throw new InvalidOperationException("No AI provider is configured. Configure OpenAI, Anthropic, Gemini, or a local OpenAI-compatible endpoint.");

        var router = new RoutedCompletionProvider(providers);
        return (router, router);
    }

    private static void TryAdd(List<NamedProvider> providers, string name, Func<IAiCompletionProvider> factory)
    {
        try
        {
            providers.Add(new NamedProvider(name, factory()));
        }
        catch (InvalidOperationException)
        {
            // A provider without credentials is simply unavailable in auto mode.
        }
    }

    private static (IAiProvider, IAiCompletionProvider) CreateOpenAi(HttpClient httpClient)
    {
        var endpoint = Environment.GetEnvironmentVariable("SOHAILOS_OPENAI_ENDPOINT")
            ?? "https://api.openai.com/v1";
        var model = Environment.GetEnvironmentVariable("SOHAILOS_OPENAI_MODEL")
            ?? "gpt-5";
        var key = Environment.GetEnvironmentVariable("SOHAILOS_OPENAI_API_KEY")
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? Environment.GetEnvironmentVariable("SOHAILOS_AI_API_KEY")
            ?? throw new InvalidOperationException("OpenAI API key is required for the OpenAI provider.");
        var provider = new OpenAiCompatibleProvider(httpClient, endpoint, model, key);
        return (provider, provider);
    }

    private static (IAiProvider, IAiCompletionProvider) CreateGemini(HttpClient httpClient)
    {
        var endpoint = Environment.GetEnvironmentVariable("SOHAILOS_GEMINI_ENDPOINT")
            ?? "https://generativelanguage.googleapis.com/v1beta/openai";
        var model = Environment.GetEnvironmentVariable("SOHAILOS_GEMINI_MODEL")
            ?? "gemini-3.8-flash";
        var key = Environment.GetEnvironmentVariable("SOHAILOS_GEMINI_API_KEY")
            ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY")
            ?? throw new InvalidOperationException("Gemini API key is required for the Gemini provider.");
        var provider = new OpenAiCompatibleProvider(httpClient, endpoint, model, key);
        return (provider, provider);
    }

    private static (IAiProvider, IAiCompletionProvider) CreateOpenAiCompatible(HttpClient httpClient)
    {
        var endpoint = Environment.GetEnvironmentVariable("SOHAILOS_AI_ENDPOINT") ?? "http://localhost:8000/v1";
        var model = Environment.GetEnvironmentVariable("SOHAILOS_AI_MODEL") ?? "Qwen/Qwen3.5-9B";
        var key = Environment.GetEnvironmentVariable("SOHAILOS_AI_API_KEY");
        var provider = new OpenAiCompatibleProvider(httpClient, endpoint, model, key);
        return (provider, provider);
    }

    private static (IAiProvider, IAiCompletionProvider) CreateAnthropic(HttpClient httpClient)
    {
        var model = Environment.GetEnvironmentVariable("SOHAILOS_ANTHROPIC_MODEL") ?? "claude-sonnet-5";
        var key = Environment.GetEnvironmentVariable("SOHAILOS_ANTHROPIC_API_KEY")
            ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
            ?? throw new InvalidOperationException("Anthropic API key is required for the Anthropic provider.");
        var provider = new AnthropicProvider(httpClient, model, key);
        return (provider, provider);
    }

    private sealed record NamedProvider(string Name, IAiCompletionProvider Provider);

    private sealed class RoutedCompletionProvider : IAiCompletionProvider, IAiProvider
    {
        private readonly IReadOnlyList<NamedProvider> _providers;

        public RoutedCompletionProvider(IReadOnlyList<NamedProvider> providers) => _providers = providers;

        public string Name => "auto:" + string.Join(",", _providers.Select(x => x.Name));

        public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
            => CompleteAsync(systemPrompt, userPrompt, Array.Empty<ToolDefinition>(), cancellationToken)
                .ContinueWith(t => t.Result.Content, cancellationToken);

        public async Task<AiCompletion> CompleteAsync(
            string systemPrompt,
            string userPrompt,
            IReadOnlyCollection<ToolDefinition> tools,
            CancellationToken cancellationToken = default)
        {
            var selected = Select(userPrompt);
            try
            {
                return await selected.Provider.CompleteAsync(systemPrompt, userPrompt, tools, cancellationToken);
            }
            catch when (!cancellationToken.IsCancellationRequested)
            {
                foreach (var fallback in _providers.Where(x => !ReferenceEquals(x, selected)))
                {
                    try
                    {
                        return await fallback.Provider.CompleteAsync(systemPrompt, userPrompt, tools, cancellationToken);
                    }
                    catch when (!cancellationToken.IsCancellationRequested)
                    {
                    }
                }

                throw;
            }
        }

        private NamedProvider Select(string prompt)
        {
            var text = prompt.ToLowerInvariant();
            if (ContainsAny(text, "کدنویسی", "کد", "github", "debug", "c#", "python", "برنامه نویسی"))
                return Find("anthropic") ?? Find("openai") ?? _providers[0];
            if (ContainsAny(text, "پژوهش", "مقاله", "research", "منبع", "literature", "مرور نظام مند", "متاآنالیز"))
                return Find("gemini") ?? Find("openai") ?? _providers[0];
            if (ContainsAny(text, "تصویر", "چندوجهی", "multimodal", "gemini"))
                return Find("gemini") ?? Find("openai") ?? _providers[0];
            if (ContainsAny(text, "هگل", "مارکس", "فلسفه", "تحلیل عمیق", "استدلال", "reasoning"))
                return Find("anthropic") ?? Find("openai") ?? _providers[0];
            return Find("openai") ?? _providers[0];
        }

        private NamedProvider? Find(string name) => _providers.FirstOrDefault(x => x.Name == name);

        private static bool ContainsAny(string text, params string[] terms) =>
            terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}
