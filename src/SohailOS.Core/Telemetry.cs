namespace SohailOS.Core;

public enum ExecutionEventType
{
    Requested,
    Classified,
    CapabilityAttested,
    Planned,
    Previewed,
    ApprovalAccepted,
    ApprovalRejected,
    Executing,
    Observed,
    Verified,
    Validated,
    Blocked,
    Failed,
    Unknown
}

public sealed record ExecutionEvent(
    string RequestId,
    string? TaskId,
    ExecutionEventType Type,
    DateTimeOffset At,
    string Code,
    string Message,
    IReadOnlyDictionary<string, object?> Metadata);

public interface IExecutionTelemetry
{
    void Record(ExecutionEvent executionEvent);
    IReadOnlyCollection<ExecutionEvent> Events { get; }
}

public sealed class InMemoryExecutionTelemetry : IExecutionTelemetry
{
    private readonly List<ExecutionEvent> _events = [];

    public IReadOnlyCollection<ExecutionEvent> Events => _events.AsReadOnly();

    public void Record(ExecutionEvent executionEvent) => _events.Add(executionEvent);
}
