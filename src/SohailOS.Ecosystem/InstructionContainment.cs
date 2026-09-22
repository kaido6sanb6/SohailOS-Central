namespace SohailOS.Ecosystem;

public static class InstructionContainment
{
    private static readonly string[] StrongPatterns =
    [
        "ignore previous instructions",
        "ignore all previous instructions",
        "system prompt",
        "developer message",
        "call tools with credentials",
        "send credentials",
        "exfiltrate",
        "tool_calls",
        "<|system|>",
        "<|developer|>",
        "you are chatgpt",
        "sudo "
    ];

    public static bool IsSuspected(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var normalized = text.ToLowerInvariant();
        return StrongPatterns.Any(pattern =>
            normalized.Contains(pattern, StringComparison.Ordinal));
    }
}
