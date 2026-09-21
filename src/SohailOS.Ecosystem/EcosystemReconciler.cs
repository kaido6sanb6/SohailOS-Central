using System.Text.Json.Nodes;

namespace SohailOS.Ecosystem;

public static class EcosystemReconciler
{
    public static ReconciliationResult Reconcile(
        JsonObject manifest,
        IReadOnlyList<LiveRepository> liveRepositories,
        string owner,
        DateTimeOffset observedAt)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(liveRepositories);

        if (string.IsNullOrWhiteSpace(owner))
            throw new ArgumentException("Owner is required.", nameof(owner));

        if (liveRepositories.Count == 0)
            throw new InvalidDataException("Live repository inventory is empty.");

        var repositories = manifest["repositories"] as JsonArray
            ?? throw new InvalidDataException("Manifest.repositories must be an array.");

        var registryRepositoryCountBefore = repositories.Count;
        var existing = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in repositories)
        {
            if (node is not JsonObject repository)
                throw new InvalidDataException("Manifest.repositories contains a non-object entry.");

            var repo = repository["repo"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(repo))
                throw new InvalidDataException("Every manifest repository must declare repo.");

            if (!existing.TryAdd(repo, repository))
                throw new InvalidDataException($"Duplicate repository in manifest: {repo}");

            var autoRoute = repository["auto_route"]?.GetValue<bool>() ?? false;
            var verification = repository["verification_status"]?.GetValue<string>();
            if (autoRoute && string.Equals(verification, "unverified", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Unsafe manifest entry: {repo} is auto-routed while unverified.");
        }

        var liveByName = new Dictionary<string, LiveRepository>(StringComparer.OrdinalIgnoreCase);
        foreach (var live in liveRepositories)
        {
            if (!live.FullName.StartsWith(owner + "/", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!liveByName.TryAdd(live.FullName, live))
                throw new InvalidDataException($"Duplicate live repository: {live.FullName}");
        }

        var added = new JsonArray();
        var changed = new JsonArray();
        var liveNames = new HashSet<string>(liveByName.Keys, StringComparer.OrdinalIgnoreCase);

        foreach (var live in liveByName.Values.OrderBy(x => x.FullName, StringComparer.OrdinalIgnoreCase))
        {
            if (existing.TryGetValue(live.FullName, out var current))
            {
                var currentBranch = current["default_branch"]?.GetValue<string>();
                if (!string.Equals(currentBranch, live.DefaultBranch, StringComparison.Ordinal))
                {
                    current["default_branch"] = live.DefaultBranch;
                    changed.Add(new JsonObject
                    {
                        ["repo"] = live.FullName,
                        ["fields"] = new JsonArray("default_branch")
                    });
                }

                continue;
            }

            var newRepository = new JsonObject
            {
                ["repo"] = live.FullName,
                ["default_branch"] = live.DefaultBranch,
                ["role"] = "unclassified",
                ["capabilities"] = new JsonArray(),
                ["verification_status"] = "unverified",
                ["auto_route"] = false,
                ["evidence"] = new JsonArray(
                    live.HtmlUrl ?? $"https://github.com/{live.FullName}"),
                ["notes"] = "Discovered by live ecosystem reconciliation; capability classification requires evidence review.",
                ["inventory"] = new JsonObject
                {
                    ["id"] = live.Id,
                    ["private"] = live.IsPrivate,
                    ["fork"] = live.IsFork,
                    ["last_seen_at"] = observedAt.UtcDateTime.ToString("O")
                }
            };

            repositories.Add(newRepository);
            existing.Add(live.FullName, newRepository);
            added.Add(live.FullName);
        }

        var removed = new JsonArray(
            existing.Keys
                .Where(repo => !liveNames.Contains(repo))
                .OrderBy(repo => repo, StringComparer.OrdinalIgnoreCase)
                .Select(repo => (JsonNode?)repo)
                .ToArray());

        var hasChanges = added.Count > 0 || changed.Count > 0;

        if (hasChanges)
        {
            manifest["sync"] = new JsonObject
            {
                ["owner"] = owner,
                ["scope"] = "public-owner-inventory",
                ["last_reconciled_at"] = observedAt.UtcDateTime.ToString("O"),
                ["live_repository_count"] = liveByName.Count,
                ["registry_repository_count"] = repositories.Count
            };
        }

        var report = new JsonObject
        {
            ["schema_version"] = "1.0",
            ["generated_at"] = observedAt.UtcDateTime.ToString("O"),
            ["owner"] = owner,
            ["registry_repository_count_before"] = registryRepositoryCountBefore,
            ["registry_repository_count_after"] = repositories.Count,
            ["live_repository_count"] = liveByName.Count,
            ["added"] = added,
            ["removed"] = removed,
            ["changed"] = changed,
            ["unchanged_count"] = Math.Max(0, liveByName.Count - added.Count - changed.Count),
            ["write_applied"] = false
        };

        return new ReconciliationResult(manifest, report, hasChanges);
    }
}
