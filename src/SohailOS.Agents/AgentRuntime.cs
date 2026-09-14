using System.Text.Json;
using SohailOS.Core;

namespace SohailOS.Agents;

public sealed class AgentRuntime
{
    private readonly IAiCompletionProvider _provider;
    private readonly IToolRegistry _registry;
    private readonly IToolExecutor _executor;
    private readonly IMemoryStore? _memory;
    private readonly int _maxIterations;

    public AgentRuntime(
        IAiCompletionProvider provider,
        IToolRegistry registry,
        IToolExecutor executor,
        IMemoryStore? memory = null,
        int maxIterations = 6)
    {
        _provider = provider;
        _registry = registry;
        _executor = executor;
        _memory = memory;
        _maxIterations = Math.Clamp(maxIterations, 1, 12);
    }

    public async Task<string> RunAsync(
        string systemPrompt,
        string userPrompt,
        bool confirmWrites = false,
        string memoryKey = "global",
        CancellationToken cancellationToken = default)
    {
        var prompt = userPrompt;

        if (_memory is not null && !string.IsNullOrWhiteSpace(memoryKey))
        {
            var prior = await _memory.GetAsync(memoryKey, cancellationToken);
            if (!string.IsNullOrWhiteSpace(prior))
                prompt = $"Persistent memory for this conversation:\n{prior}\n\nCurrent user request:\n{userPrompt}";
        }

        for (var iteration = 0; iteration < _maxIterations; iteration++)
        {
            var completion = await _provider.CompleteAsync(
                systemPrompt, prompt, _registry.Definitions, cancellationToken);

            if (completion.ToolCalls.Count == 0)
            {
                if (_memory is not null && !string.IsNullOrWhiteSpace(memoryKey))
                {
                    var snapshot = JsonSerializer.Serialize(new
                    {
                        updatedAt = DateTimeOffset.UtcNow,
                        user = userPrompt,
                        assistant = completion.Content
                    });
                    await _memory.SaveAsync(memoryKey, snapshot, cancellationToken);
                }

                return completion.Content;
            }

            var results = new List<string>();
            foreach (var call in completion.ToolCalls)
            {
                var result = await _executor.ExecuteAsync(call, confirmWrites, cancellationToken);
                results.Add(JsonSerializer.Serialize(new
                {
                    tool = call.Name,
                    executed = result.Executed,
                    requiresConfirmation = result.RequiresConfirmation,
                    success = result.Result.Success,
                    content = result.Result.Content
                }));
            }

            prompt = $"{prompt}\n\nTool results from the previous step:\n{string.Join("\n", results)}\n\nContinue the task. Do not claim a tool action succeeded unless the tool result says success=true.";
        }

        return "The agent stopped after reaching the maximum tool-execution iterations. Review the tool results and continue with a narrower request if necessary.";
    }
}
