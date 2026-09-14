using SohailOS.Core;

namespace SohailOS.AI;

public static class AiProviderFactory
{
    public static (IAiProvider Legacy, IAiCompletionProvider Completion) Create(HttpClient httpClient)
    {
        var provider = (Environment.GetEnvironmentVariable("SOHAILOS_AI_PROVIDER") ?? "openai-compatible")
            .Trim().ToLowerInvariant();

        return provider switch
        {
            "anthropic" or "claude" => CreateAnthropic(httpClient),
            "gemini" or "google" => CreateGemini(httpClient),
            "openai" => CreateOpenAi(httpClient),
            "local" or "openai-compatible" => CreateOpenAiCompatible(httpClient),
            _ => throw new InvalidOperationException($"Unsupported SOHAILOS_AI_PROVIDER: {provider}")
        };
    }

    private static (IAiProvider, IAiCompletionProvider) CreateOpenAi(HttpClient httpClient)
    {
        var endpoint = Environment.GetEnvironmentVariable("SOHAILOS_OPENAI_ENDPOINT")
            ?? "https://api.openai.com/v1";
        var model = Environment.GetEnvironmentVariable("SOHAILOS_OPENAI_MODEL")
            ?? "gpt-5";
        var key = Environment.GetEnvironmentVariable("SOHAILOS_OPENAI_API_KEY")
            ?? Environment.GetEnvironmentVariable("SOHAILOS_AI_API_KEY")
            ?? throw new InvalidOperationException("SOHAILOS_OPENAI_API_KEY is required for the OpenAI provider.");
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
            ?? throw new InvalidOperationException("SOHAILOS_GEMINI_API_KEY is required for the Gemini provider.");
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
            ?? throw new InvalidOperationException("SOHAILOS_ANTHROPIC_API_KEY is required for the Anthropic provider.");
        var provider = new AnthropicProvider(httpClient, model, key);
        return (provider, provider);
    }
}
