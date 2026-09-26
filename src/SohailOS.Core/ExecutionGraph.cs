namespace SohailOS.Core;

public enum TaskNodeStatus
{
    Planned,
    Ready,
    Running,
    Observed,
    Verified,
    Validated,
    Completed,
    Blocked,
    RetryableFailure,
    PermanentFailure,
    Unknown
}

public sealed record RetryPolicy(
    bool Idempotent,
    int MaxAttempts = 1,
    TimeSpan? Backoff = null)
{
    public int EffectiveMaxAttempts => Math.Clamp(MaxAttempts, 1, 8);
}

public sealed record TaskNode(
    string Id,
    string Goal,
    IReadOnlyList<string> Dependencies,
    ActionClass Action,
    RetryPolicy Retry,
    TaskNodeStatus Status = TaskNodeStatus.Planned);

public sealed class TaskGraph
{
    private readonly Dictionary<string, TaskNode> _nodes = new(StringComparer.Ordinal);

    public IReadOnlyCollection<TaskNode> Nodes => _nodes.Values;

    public void Add(TaskNode node)
    {
        if (!_nodes.TryAdd(node.Id, node))
            throw new InvalidOperationException($"Duplicate task node: {node.Id}.");
    }

    public IReadOnlyList<TaskNode> GetReadyNodes()
    {
        foreach (var node in _nodes.Values)
        {
            foreach (var dependency in node.Dependencies)
            {
                if (!_nodes.ContainsKey(dependency))
                    throw new InvalidOperationException($"Unknown dependency: {dependency}.");
            }
        }

        return _nodes.Values
            .Where(n => n.Status == TaskNodeStatus.Planned &&
                        n.Dependencies.All(d => _nodes[d].Status == TaskNodeStatus.Completed))
            .ToArray();
    }

    public IReadOnlyList<TaskNode> ClaimReadyNodes()
    {
        var ready = GetReadyNodes();
        foreach (var node in ready)
            SetStatus(node.Id, TaskNodeStatus.Ready);
        return ready.Select(node => node with { Status = TaskNodeStatus.Ready }).ToArray();
    }

    public void SetStatus(string nodeId, TaskNodeStatus status)
    {
        if (!_nodes.TryGetValue(nodeId, out var node))
            throw new KeyNotFoundException($"Unknown task node: {nodeId}.");

        if (node.Status == status)
            return;

        if (!IsAllowedTransition(node.Status, status))
            throw new InvalidOperationException(
                $"Invalid task-node transition: {node.Status} -> {status}.");

        _nodes[nodeId] = node with { Status = status };
    }

    private static bool IsAllowedTransition(TaskNodeStatus from, TaskNodeStatus to) => (from, to) switch
    {
        (TaskNodeStatus.Planned, TaskNodeStatus.Ready) => true,
        (TaskNodeStatus.Planned, TaskNodeStatus.Blocked) => true,
        (TaskNodeStatus.Ready, TaskNodeStatus.Running) => true,
        (TaskNodeStatus.Ready, TaskNodeStatus.Blocked) => true,
        (TaskNodeStatus.Running, TaskNodeStatus.Observed) => true,
        (TaskNodeStatus.Running, TaskNodeStatus.RetryableFailure) => true,
        (TaskNodeStatus.Running, TaskNodeStatus.PermanentFailure) => true,
        (TaskNodeStatus.Running, TaskNodeStatus.Blocked) => true,
        (TaskNodeStatus.Running, TaskNodeStatus.Unknown) => true,
        (TaskNodeStatus.Observed, TaskNodeStatus.Verified) => true,
        (TaskNodeStatus.Observed, TaskNodeStatus.RetryableFailure) => true,
        (TaskNodeStatus.Observed, TaskNodeStatus.PermanentFailure) => true,
        (TaskNodeStatus.Observed, TaskNodeStatus.Unknown) => true,
        (TaskNodeStatus.Verified, TaskNodeStatus.Validated) => true,
        (TaskNodeStatus.Verified, TaskNodeStatus.PermanentFailure) => true,
        (TaskNodeStatus.Verified, TaskNodeStatus.Unknown) => true,
        (TaskNodeStatus.Validated, TaskNodeStatus.Completed) => true,
        (TaskNodeStatus.Validated, TaskNodeStatus.PermanentFailure) => true,
        (TaskNodeStatus.RetryableFailure, TaskNodeStatus.Ready) => true,
        (TaskNodeStatus.RetryableFailure, TaskNodeStatus.Blocked) => true,
        (TaskNodeStatus.Blocked, TaskNodeStatus.Ready) => true,
        (TaskNodeStatus.Unknown, TaskNodeStatus.Ready) => true,
        (TaskNodeStatus.Unknown, TaskNodeStatus.Blocked) => true,
        _ => false
    };

    public bool IsComplete => _nodes.Count > 0 &&
        _nodes.Values.All(n => n.Status == TaskNodeStatus.Completed);

    public static void ValidateAcyclic(TaskGraph graph)
    {
        var state = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var node in graph.Nodes)
            Visit(node.Id);

        void Visit(string id)
        {
            if (!state.TryGetValue(id, out var current))
                current = 0;

            if (current == 1)
                throw new InvalidOperationException("Task graph contains a cycle.");
            if (current == 2)
                return;

            state[id] = 1;
            if (!graph._nodes.TryGetValue(id, out var node))
                throw new InvalidOperationException($"Unknown dependency: {id}.");

            foreach (var dependency in node.Dependencies)
                Visit(dependency);

            state[id] = 2;
        }
    }
}
