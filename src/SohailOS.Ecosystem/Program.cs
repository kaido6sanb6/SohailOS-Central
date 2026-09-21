using System.Text.Json;
using System.Text.Json.Nodes;

namespace SohailOS.Ecosystem;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var options = ParseArguments(args);
            var manifestPath = GetOption(options, "manifest", Path.Combine("ecosystem", "ecosystem.json"));
            var reportPath = GetOption(options, "report", Path.Combine("ecosystem", "reconciliation.json"));
            var owner = GetOption(options, "owner", "kaido6sanb6");
            var write = options.ContainsKey("write");
            var failOnRemoval = options.ContainsKey("fail-on-removal");

            var manifest = JsonNode.Parse(
                await File.ReadAllTextAsync(manifestPath))
                as JsonObject
                ?? throw new InvalidDataException("Manifest root must be a JSON object.");

            IReadOnlyList<LiveRepository> liveRepositories;

            if (options.TryGetValue("inventory", out var inventoryPath))
            {
                liveRepositories = await LoadInventoryFixtureAsync(inventoryPath);
            }
            else
            {
                using var client = new HttpClient();
                var inventoryClient = new GitHubInventoryClient(client);
                liveRepositories = await inventoryClient.GetPublicOwnerRepositoriesAsync(owner);
            }

            var result = EcosystemReconciler.Reconcile(
                manifest,
                liveRepositories,
                owner,
                DateTimeOffset.UtcNow);

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);

            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };

            var addedCount = result.Report["added"]?.AsArray().Count ?? 0;
            var removedCount = result.Report["removed"]?.AsArray().Count ?? 0;
            var changedCount = result.Report["changed"]?.AsArray().Count ?? 0;
            var safeToWrite = write && result.HasChanges && !(failOnRemoval && removedCount > 0);

            result.Report["write_applied"] = safeToWrite;
            await File.WriteAllTextAsync(
                reportPath,
                result.Report.ToJsonString(jsonOptions));

            if (safeToWrite)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(manifestPath))!);
                await File.WriteAllTextAsync(
                    manifestPath,
                    result.Manifest.ToJsonString(jsonOptions));
            }

            Console.WriteLine(
                $"Ecosystem reconciliation: live={liveRepositories.Count}, added={addedCount}, changed={changedCount}, removed={removedCount}, write={(safeToWrite ? "applied" : "not-applied")}");

            if (failOnRemoval && removedCount > 0)
            {
                Console.WriteLine("Ecosystem reconciliation detected a repository removal; no manifest write was applied.");
                return 2;
            }

            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static async Task<IReadOnlyList<LiveRepository>> LoadInventoryFixtureAsync(string path)
    {
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path));

        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Inventory fixture root must be a JSON array.");

        var result = new List<LiveRepository>();

        foreach (var item in document.RootElement.EnumerateArray())
        {
            var fullName = item.GetProperty("full_name").GetString()
                ?? throw new InvalidDataException("Inventory entry is missing full_name.");
            var name = item.GetProperty("name").GetString()
                ?? throw new InvalidDataException("Inventory entry is missing name.");
            var branch = item.GetProperty("default_branch").GetString()
                ?? throw new InvalidDataException("Inventory entry is missing default_branch.");

            long? id = null;
            if (item.TryGetProperty("id", out var idElement) && idElement.TryGetInt64(out var parsedId))
                id = parsedId;

            var htmlUrl = item.TryGetProperty("html_url", out var urlElement)
                ? urlElement.GetString()
                : null;

            var isPrivate = item.TryGetProperty("private", out var privateElement) &&
                            privateElement.ValueKind == JsonValueKind.True;

            var isFork = item.TryGetProperty("fork", out var forkElement) &&
                         forkElement.ValueKind == JsonValueKind.True;

            result.Add(new LiveRepository(id, name, fullName, branch, htmlUrl, isPrivate, isFork));
        }

        return result;
    }

    private static Dictionary<string, string> ParseArguments(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (string.Equals(arg, "--write", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--fail-on-removal", StringComparison.OrdinalIgnoreCase))
            {
                result["write"] = "true";
                continue;
            }

            if (!arg.StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Unexpected argument: {arg}");

            if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Missing value for argument: {arg}");

            result[arg[2..]] = args[++i];
        }

        return result;
    }

    private static string GetOption(
        Dictionary<string, string> options,
        string key,
        string defaultValue)
        => options.TryGetValue(key, out var value) ? value : defaultValue;
}
