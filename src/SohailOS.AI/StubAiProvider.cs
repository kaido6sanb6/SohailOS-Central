using SohailOS.Core;

namespace SohailOS.AI;

public sealed class StubAiProvider : IAiProvider
{
    public string Name => "Stub";

    public Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
        => Task.FromResult($"[AI provider not configured] Request received: {userPrompt}");
}
