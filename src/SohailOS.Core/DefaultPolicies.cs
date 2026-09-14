namespace SohailOS.Core;

public sealed class DefaultPermissionPolicy : IPermissionPolicy
{
    public bool RequiresConfirmation(ToolDefinition definition) =>
        definition.Permission is ToolPermission.Write or ToolPermission.Destructive;
}

public sealed class ToolRegistry : IToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<ToolDefinition> Definitions =>
        _tools.Values.Select(tool => tool.Definition).ToArray();

    public void Register(ITool tool) => _tools[tool.Definition.Name] = tool;

    public ITool? Get(string name) =>
        _tools.TryGetValue(name, out var tool) ? tool : null;
}
