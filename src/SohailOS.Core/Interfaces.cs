namespace SohailOS.Core;

public interface IOrchestrator
{
    Task<AgentResponse> HandleAsync(UserRequest request, CancellationToken cancellationToken = default);
}

public interface IModuleAgent
{
    SohailModule Module { get; }
    Task<string> ExecuteAsync(UserRequest request, CancellationToken cancellationToken = default);
}

public interface IAiProvider
{
    string Name { get; }
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);
}

public interface IMemoryStore
{
    Task SaveAsync(string key, string value, CancellationToken cancellationToken = default);
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);
}
