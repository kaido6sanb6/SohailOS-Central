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

            if (options.ContainsKey("index"))
                return await RunIndexAsync(options, manifestPath, owner);

            if (options.ContainsKey("export-search"))
                return await RunExportSearchAsync(options);

            return await RunReconciliationAsync(
                options,
                manifestPath,
                reportPath,
                owner);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static async Task<int> RunReconciliationAsync(
        Dictionary<string, string> options,
        string manifestPath,
        string reportPath,
        string owner)
    {
        var write = options.ContainsKey("write");
        var failOnRemoval = options.ContainsKey("fail-on-removal");
        var manifest = await LoadManifestAsync(manifestPath);
        var liveRepositories = await LoadLiveRepositoriesAsync(options, owner);

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
        var safeToWrite =
            write &&
            result.HasChanges &&
            !(failOnRemoval && removedCount > 0);

        result.Report["write_applied"] = safeToWrite;
        await File.WriteAllTextAsync(
            reportPath,
            result.Report.ToJsonString(jsonOptions));

        if (safeToWrite)
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(Path.GetFullPath(manifestPath))!);

            await File.WriteAllTextAsync(
                manifestPath,
                result.Manifest.ToJsonString(jsonOptions));
        }

        Console.WriteLine(
            $"Ecosystem reconciliation: live={liveRepositories.Count}, added={addedCount}, changed={changedCount}, removed={removedCount}, write={(safeToWrite ? "applied" : "not-applied")}");

        if (failOnRemoval && removedCount > 0)
        {
            Console.WriteLine(
                "Ecosystem reconciliation detected a repository removal; no manifest write was applied.");
            return 2;
        }

        return 0;
    }

    private static async Task<int> RunIndexAsync(
        Dictionary<string, string> options,
        string manifestPath,
        string owner)
    {
        var indexPath = GetOption(
            options,
            "index-path",
            Path.Combine("App_Data", "sohailos-knowledge-index.json"));
        var reportPath = GetOption(
            options,
            "index-report",
            Path.Combine("ecosystem", "index-report.json"));
        var knowledgeGraphPath = GetOption(
            options,
            "knowledge-graph",
            Path.Combine("ecosystem", "generated", "knowledge-graph.json"));

        var manifest = await LoadManifestAsync(manifestPath);
        var liveRepositories = await LoadLiveRepositoriesAsync(options, owner);

        var provider = new JsonFileDataPlaneProvider(indexPath, autoSave: false);
        var sourceClient = new GitHubKnowledgeSourceClient(
            new HttpClient { Timeout = TimeSpan.FromSeconds(60) });

        var embeddingProvider = CreateEmbeddingProvider();
        var indexer = new GitHubKnowledgeIndexer(
            sourceClient,
            provider,
            embeddingProvider);

        var runResults = new List<KnowledgeRunRecord>();

        foreach (var repository in liveRepositories
                     .OrderBy(x => x.FullName, StringComparer.OrdinalIgnoreCase))
        {
            if (repository.IsPrivate &&
                !string.Equals(
                    Environment.GetEnvironmentVariable("SOHAILOS_KNOWLEDGE_ALLOW_PRIVATE"),
                    "true",
                    StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(
                    $"Skipping private repository {repository.FullName}: private indexing is disabled.");
                continue;
            }

            var trustTier = GetTrustTier(manifest, repository);

            if (trustTier == KnowledgeTrustTier.Tombstoned)
            {
                Console.WriteLine(
                    $"Skipping tombstoned repository {repository.FullName}.");
                continue;
            }

            var result = await indexer.IndexRepositoryAsync(
                repository,
                trustTier);

            runResults.Add(result);

            Console.WriteLine(
                $"Indexed {repository.FullName}: state={result.State}, counts={JsonSerializer.Serialize(result.Counts)}, degraded={string.Join(",", result.DegradedFlags)}");
        }

        var inventoryForGraph = options.TryGetValue("inventory", out var inventoryPathForGraph)
            ? await LoadInventoryJsonAsync(inventoryPathForGraph)
            : new JsonArray();

        var graph = KnowledgeGraphBuilder.Build(liveRepositories, inventoryForGraph);
        Directory.CreateDirectory(
            Path.GetDirectoryName(Path.GetFullPath(knowledgeGraphPath))!);
        await File.WriteAllTextAsync(
            knowledgeGraphPath,
            JsonSerializer.Serialize(
                graph,
                new JsonSerializerOptions { WriteIndented = true }));

        var report = new
        {
            schema_version = "1.0",
            generated_at = DateTimeOffset.UtcNow,
            owner,
            live_repository_count = liveRepositories.Count,
            indexed_repository_count = runResults.Count,
            committed_count = runResults.Count(x => x.State == KnowledgeIndexState.Committed),
            partial_count = runResults.Count(x => x.State == KnowledgeIndexState.Partial),
            retryable_failure_count = runResults.Count(x => x.State == KnowledgeIndexState.FailedRetryable),
            permanent_failure_count = runResults.Count(x => x.State == KnowledgeIndexState.FailedPermanent),
            degraded_flags = runResults
                .SelectMany(x => x.DegradedFlags)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray(),
            runs = runResults
        };

        Directory.CreateDirectory(
            Path.GetDirectoryName(Path.GetFullPath(reportPath))!);

        await provider.FlushAsync();

        await File.WriteAllTextAsync(
            reportPath,
            JsonSerializer.Serialize(
                report,
                new JsonSerializerOptions { WriteIndented = true }));

        return runResults.Any(x =>
            x.State is KnowledgeIndexState.FailedPermanent) ? 2 : 0;
    }


    private static async Task<int> RunExportSearchAsync(
        Dictionary<string, string> options)
    {
        var indexPath = GetOption(
            options,
            "index-path",
            Path.Combine("App_Data", "sohailos-knowledge-index.json"));
        var outputPath = GetOption(
            options,
            "search-export-path",
            Path.Combine("App_Data", "sohailos-search-export"));
        var maxFileBytes = GetPositiveInt(
            options,
            "search-max-file-bytes",
            3_500_000);

        var provider = new JsonFileDataPlaneProvider(indexPath);
        var result = await KnowledgeSearchExportService.ExportAsync(
            provider,
            outputPath,
            maxFileBytes);

        Console.WriteLine(
            $"Knowledge search export: files={result.Files.Count}, chunks={result.ChunkCount}, bytes={result.TotalBytes}, output={outputPath}");

        return 0;
    }

    private static async Task<JsonArray> LoadInventoryJsonAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        return await JsonNode.ParseAsync(stream) as JsonArray
            ?? throw new InvalidDataException("Inventory root must be a JSON array.");
    }

    private static async Task<JsonObject> LoadManifestAsync(string path)
    {
        await using var stream = File.OpenRead(path);

        return await JsonNode.ParseAsync(stream)
            as JsonObject
            ?? throw new InvalidDataException(
                "Manifest root must be a JSON object.");
    }

    private static async Task<IReadOnlyList<LiveRepository>> LoadLiveRepositoriesAsync(
        Dictionary<string, string> options,
        string owner)
    {
        if (options.TryGetValue("inventory", out var inventoryPath))
            return await LoadInventoryFixtureAsync(inventoryPath);

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        var inventoryClient = new GitHubInventoryClient(client);
        return await inventoryClient.GetPublicOwnerRepositoriesAsync(owner);
    }

    private static KnowledgeTrustTier GetTrustTier(
        JsonObject manifest,
        LiveRepository repository)
    {
        if (string.Equals(
                repository.FullName,
                "kaido6sanb6/SohailOS-Central",
                StringComparison.OrdinalIgnoreCase))
            return KnowledgeTrustTier.Authoritative;

        var repositories = manifest["repositories"] as JsonArray;
        if (repositories is null)
            return KnowledgeTrustTier.Unverified;

        foreach (var node in repositories.OfType<JsonObject>())
        {
            var repo = node["repo"]?.GetValue<string>();
            if (!string.Equals(
                    repo,
                    repository.FullName,
                    StringComparison.OrdinalIgnoreCase))
                continue;

            var verification = node["verification_status"]?.GetValue<string>();

            return verification?.ToLowerInvariant() switch
            {
                "verified" => KnowledgeTrustTier.Verified,
                "reference_only" => KnowledgeTrustTier.ReferenceOnly,
                "unverified" => KnowledgeTrustTier.Unverified,
                "tombstoned" => KnowledgeTrustTier.Tombstoned,
                _ => KnowledgeTrustTier.Unverified
            };
        }

        return KnowledgeTrustTier.Unverified;
    }

    private static IEmbeddingProvider CreateEmbeddingProvider()
    {
        var baseUrl = Environment.GetEnvironmentVariable("SOHAILOS_EMBEDDINGS_BASE_URL");
        var model = Environment.GetEnvironmentVariable("SOHAILOS_EMBEDDINGS_MODEL");

        if (string.IsNullOrWhiteSpace(baseUrl) ||
            string.IsNullOrWhiteSpace(model))
            return new NullEmbeddingProvider();

        var apiKey = Environment.GetEnvironmentVariable("SOHAILOS_EMBEDDINGS_API_KEY");
        var dimensions = GetPositiveInt(
            "SOHAILOS_EMBEDDINGS_DIMENSIONS",
            1536);

        return new OpenAICompatibleEmbeddingProvider(
            new HttpClient { Timeout = TimeSpan.FromSeconds(60) },
            baseUrl,
            model,
            apiKey,
            dimensions);
    }

    private static async Task<IReadOnlyList<LiveRepository>> LoadInventoryFixtureAsync(
        string path)
    {
        using var document = JsonDocument.Parse(
            await File.ReadAllTextAsync(path));

        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException(
                "Inventory fixture root must be a JSON array.");

        var result = new List<LiveRepository>();

        foreach (var item in document.RootElement.EnumerateArray())
        {
            var fullName = item.GetProperty("full_name").GetString()
                ?? throw new InvalidDataException(
                    "Inventory entry is missing full_name.");
            var name = item.GetProperty("name").GetString()
                ?? throw new InvalidDataException(
                    "Inventory entry is missing name.");
            var branch = item.GetProperty("default_branch").GetString()
                ?? throw new InvalidDataException(
                    "Inventory entry is missing default_branch.");

            long? id = null;
            if (item.TryGetProperty("id", out var idElement) &&
                idElement.TryGetInt64(out var parsedId))
                id = parsedId;

            var htmlUrl = item.TryGetProperty(
                    "html_url",
                    out var urlElement)
                ? urlElement.GetString()
                : null;

            var isPrivate =
                item.TryGetProperty("private", out var privateElement) &&
                privateElement.ValueKind == JsonValueKind.True;

            var isFork =
                item.TryGetProperty("fork", out var forkElement) &&
                forkElement.ValueKind == JsonValueKind.True;

            result.Add(
                new LiveRepository(
                    id,
                    name,
                    fullName,
                    branch,
                    htmlUrl,
                    isPrivate,
                    isFork));
        }

        return result;
    }

    private static Dictionary<string, string> ParseArguments(string[] args)
    {
        var result = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (string.Equals(
                    arg,
                    "--write",
                    StringComparison.OrdinalIgnoreCase))
            {
                result["write"] = "true";
                continue;
            }

            if (string.Equals(
                    arg,
                    "--fail-on-removal",
                    StringComparison.OrdinalIgnoreCase))
            {
                result["fail-on-removal"] = "true";
                continue;
            }

            if (string.Equals(
                    arg,
                    "--index",
                    StringComparison.OrdinalIgnoreCase))
            {
                result["index"] = "true";
                continue;
            }

            if (!arg.StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException(
                    $"Unexpected argument: {arg}");

            if (i + 1 >= args.Length ||
                args[i + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException(
                    $"Missing value for argument: {arg}");

            result[arg[2..]] = args[++i];
        }

        return result;
    }

    private static string GetOption(
        Dictionary<string, string> options,
        string key,
        string defaultValue)
        => options.TryGetValue(key, out var value)
            ? value
            : defaultValue;

    private static int GetPositiveInt(
        Dictionary<string, string> options,
        string key,
        int fallback)
        => options.TryGetValue(key, out var optionValue) && int.TryParse(optionValue, out var optionParsed) && optionParsed > 0
            ? optionParsed
            : fallback;

    private static int GetPositiveInt(
        string variableName,
        int fallback)
        => int.TryParse(
                Environment.GetEnvironmentVariable(variableName),
                out var value) &&
            value > 0
            ? value
            : fallback;
}
