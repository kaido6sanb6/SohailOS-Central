using SohailOS.Core;

namespace SohailOS.AI;

public static class AiProviderFactory
{
    public static (IAiProvider Legacy, IAiCompletionProvider Completion) Create(HttpClient httpClient)
    {
        var provider = (Environment.GetEnvironmentVariable("SOHAILOS_AI_PROVIDER") ?? "openai-compatible").Trim().ToLowerInvariant();
        return provider switch
        {
            "anthropic" => CreateAnthropic(httpClient),
            _ => CreateOpenAiCompatible(httpClient)
        };
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
            ?? throw new InvalidOperationException("SOHAILOS_ANTHROPIC_API_KEY is required for the Anthropic provider.");
        var provider = new AnthropicProvider(httpClient, model, key);
        return (provider, provider);
    }
}
