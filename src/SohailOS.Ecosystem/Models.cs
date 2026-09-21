namespace SohailOS.Ecosystem;

public sealed record LiveRepository(
    long? Id,
    string Name,
    string FullName,
    string DefaultBranch,
    string? HtmlUrl,
    bool IsPrivate,
    bool IsFork);

public sealed record ReconciliationResult(
    System.Text.Json.Nodes.JsonObject Manifest,
    System.Text.Json.Nodes.JsonObject Report,
    bool HasChanges);
