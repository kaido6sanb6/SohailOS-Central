using System.Text.Json;
using SohailOS.Core;

namespace SohailOS.Agents;

public sealed class AgentRuntime
{
    private readonly IAiCompletionProvider _provider;
    private readonly IToolRegistry _registry;
    private readonly IToolExecutor _executor;
    private readonly IMemoryStore? _memory;
    private readonly IExecutionVerifier _verifier;
    private readonly IExecutionValidator _validator;
    private readonly IExecutionTelemetry? _telemetry;
    private readonly int _maxIterations;

    public AgentRuntime(
        IAiCompletionProvider provider,
        IToolRegistry registry,
        IToolExecutor executor,
        IMemoryStore? memory = null,
        int maxIterations = 6,
        IExecutionVerifier? verifier = null,
        IExecutionValidator? validator = null,
        IExecutionTelemetry? telemetry = null)
    {
        _provider = provider;
        _registry = registry;
        _executor = executor;
        _memory = memory;
        _verifier = verifier ?? new BasicExecutionVerifier();
        _validator = validator ?? new InvariantValidator();
        _telemetry = telemetry;
        _maxIterations = Math.Clamp(maxIterations, 1, 12);
    }

    public async Task<string> RunAsync(
        string systemPrompt,
        string userPrompt,
        ApprovalBinding? approval = null,
        string memoryKey = "global",
        bool persistMemory = false,
        CancellationToken cancellationToken = default)
    {
        var requestId = approval?.RequestId ?? Guid.NewGuid().ToString("N");
        var prompt = userPrompt;

        if (_memory is not null && !string.IsNullOrWhiteSpace(memoryKey))
        {
            var prior = await _memory.GetAsync(memoryKey, cancellationToken);
            if (!string.IsNullOrWhiteSpace(prior))
                prompt = $"Persistent memory for this conversation:\n{prior}\n\nCurrent user request:\n{userPrompt}";
        }

        _telemetry?.Record(new(requestId, null, ExecutionEventType.Requested, DateTimeOffset.UtcNow, "REQUESTED", "Agent run started.", new Dictionary<string, object?>()));

        for (var iteration = 0; iteration < _maxIterations; iteration++)
        {
            var completion = await _provider.CompleteAsync(
                systemPrompt, prompt, _registry.Definitions, cancellationToken);

            if (completion.ToolCalls.Count == 0)
            {
                if (persistMemory && _memory is not null && !string.IsNullOrWhiteSpace(memoryKey))
                {
                    if (approval is not null)
                    {
                        var snapshot = JsonSerializer.Serialize(new
                        {
                            updatedAt = DateTimeOffset.UtcNow,
                            user = userPrompt,
                            assistant = completion.Content,
                            provenance = "agent-runtime"
                        });
                        var transaction = new MemoryTransactionContract(memoryKey, approval);
                        if (transaction.IsValid(DateTimeOffset.UtcNow, requestId))
                            await transaction.PersistAsync(_memory, snapshot, requestId, cancellationToken);
                        else
                            _telemetry?.Record(new(
                                requestId, null, ExecutionEventType.Blocked, DateTimeOffset.UtcNow,
                                "MEMORY_APPROVAL_REQUIRED",
                                "Durable memory write was blocked because the exact durable transaction binding was invalid.",
                                new Dictionary<string, object?> { ["memoryKey"] = memoryKey }));
                    }
                    else
                    {
                        _telemetry?.Record(new(
                            requestId, null, ExecutionEventType.Blocked, DateTimeOffset.UtcNow,
                            "MEMORY_APPROVAL_REQUIRED",
                            "Durable memory write was blocked because an exact, non-expired approval binding was not supplied.",
                            new Dictionary<string, object?> { ["memoryKey"] = memoryKey }));
                    }
                }

                return completion.Content;
            }

            var results = new List<string>();
            foreach (var call in completion.ToolCalls)
            {
                var result = approval is null
                    ? await _executor.ExecuteAsync(call, cancellationToken: cancellationToken)
                    : await _executor.ExecuteAsync(call, approval, cancellationToken);

                var definition = _registry.Get(call.Name)?.Definition;
                var action = definition?.Permission switch
                {
                    ToolPermission.Destructive => ActionClass.Mutate,
                    ToolPermission.Write => ActionClass.Write,
                    _ => ActionClass.Read
                };

                var request = new ExecutionRequest(
                    action,
                    call.Name,
                    call.Target ?? call.Name,
                    call.Scope ?? "unspecified",
                    call.IntendedEffect ?? "tool execution",
                    RequestId: requestId);

                var evidence = result.Evidence is null
                    ? Array.Empty<EvidenceItem>()
                    : new[] { result.Evidence };
                var verification = _verifier.Verify(request, result, evidence);
                var validation = _validator.Validate(request, verification, evidence);

                _telemetry?.Record(new(requestId, null,
                    verification.Status == VerificationStatus.Verified ? ExecutionEventType.Verified : ExecutionEventType.Failed,
                    DateTimeOffset.UtcNow, verification.Code, verification.Reason,
                    new Dictionary<string, object?> { ["evidenceIds"] = verification.EvidenceIds }));

                if (validation.Status == ValidationStatus.Validated)
                    _telemetry?.Record(new(requestId, null, ExecutionEventType.Validated, DateTimeOffset.UtcNow, validation.Code, validation.Reason, new Dictionary<string, object?>()));

                results.Add(JsonSerializer.Serialize(new
                {
                    tool = call.Name,
                    executed = result.Executed,
                    requiresConfirmation = result.RequiresConfirmation,
                    success = result.Result.Success,
                    content = result.Result.Content,
                    evidenceId = result.Evidence?.Id,
                    verification = verification.Status.ToString(),
                    validation = validation.Status.ToString()
                }));
            }

            prompt = $"{prompt}\n\nTool results from the previous step:\n{string.Join("\n", results)}\n\nContinue the task. Execution success is not equivalent to verified outcome; do not claim a requested side effect is complete unless verification and validation evidence support it.";
        }

        _telemetry?.Record(new(requestId, null, ExecutionEventType.Blocked, DateTimeOffset.UtcNow, "ITERATION_LIMIT", "Maximum tool-execution iterations reached.", new Dictionary<string, object?>()));
        return "The agent stopped after reaching the maximum bounded tool-execution iterations; outcome remains unverified.";
    }
}
