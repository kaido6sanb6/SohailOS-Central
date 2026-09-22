namespace SohailOS.Ecosystem;

public sealed record LiveCorpusSource(
    string Repository,
    string Branch,
    string Trust,
    bool RefetchWhenRelevant = true);

public sealed record LiveCorpusManifestDocument(
    IReadOnlyList<LiveCorpusSource> Sources,
    IReadOnlyList<string> Stages);

public static class LiveCorpusManifest
{
    private static readonly string[] RequiredStages =
    [
        "fetch",
        "inspect",
        "classify",
        "extract",
        "normalize",
        "deduplicate",
        "compare",
        "verify",
        "synthesize",
        "index"
    ];

    public static LiveCorpusManifestDocument CreateDefault()
        => new(
            new[]
            {
                new LiveCorpusSource(
                    "asgeirtj/system_prompts_leaks",
                    "main",
                    "untrusted-data"),
                new LiveCorpusSource(
                    "kaido6sanb6/system_prompts_leaks",
                    "main",
                    "untrusted-data")
            },
            RequiredStages);
}
