using SohailOS.Core;

namespace SohailOS.Agents;

public sealed class ModuleAgent : IModuleAgent
{
    private readonly IAiProvider _provider;
    private readonly string _systemPrompt;

    public ModuleAgent(SohailModule module, IAiProvider provider, string systemPrompt)
    {
        Module = module;
        _provider = provider;
        _systemPrompt = systemPrompt;
    }

    public SohailModule Module { get; }

    public Task<string> ExecuteAsync(UserRequest request, CancellationToken cancellationToken = default) =>
        _provider.CompleteAsync(_systemPrompt, request.Text, cancellationToken);
}
