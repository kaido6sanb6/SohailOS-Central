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
        return _nodes.Values
            .Where(n => n.Status == TaskNodeStatus.Planned &&
                        n.Dependencies.All(d => _nodes.TryGetValue(d, out var dep) &&
                            dep.Status == TaskNodeStatus.Completed))
            .Select(n => n with { Status = TaskNodeStatus.Ready })
            .ToArray();
    }

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
