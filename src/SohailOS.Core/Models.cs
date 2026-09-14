namespace SohailOS.Core;

public enum SohailModule
{
    Think, Sociology, Cinema, Research, Stats, Ai, Code, Product, Office, Operations, Strategy, Learning
}

public sealed record UserRequest(string Text, DateTimeOffset CreatedAt);

public sealed record RouteDecision(
    SohailModule PrimaryModule,
    IReadOnlyList<SohailModule> SupportingModules,
    string Reason,
    double Confidence);

public sealed record AgentResponse(
    string Content,
    RouteDecision Route,
    DateTimeOffset CreatedAt,
    IReadOnlyDictionary<string, object?> Metadata);
