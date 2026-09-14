using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace SohailOS.App;

public sealed record UpdateManifest(string Version, string PackageUrl, string Sha256);

public sealed class SelfUpdateService
{
    private readonly HttpClient _httpClient;
    private readonly string _owner;
    private readonly string _repo;
    private readonly string _currentVersion;

    public SelfUpdateService(HttpClient httpClient, string currentVersion, string owner = "kaido6sanb6", string repo = "SohailOS-Central")
    {
        _httpClient = httpClient;
        _currentVersion = currentVersion;
        _owner = owner;
        _repo = repo;
    }

    public async Task<bool> CheckAndStageAsync(CancellationToken cancellationToken = default)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("SOHAILOS_UPDATES_ENABLED"), "true", StringComparison.OrdinalIgnoreCase))
            return false;

        var manifestUrl = $"https://raw.githubusercontent.com/{_owner}/{_repo}/main/deploy/update-manifest.json";
        var manifest = await _httpClient.GetFromJsonAsync<UpdateManifest>(manifestUrl, cancellationToken);
        if (manifest is null || !Version.TryParse(manifest.Version, out var remote) || !Version.TryParse(_currentVersion, out var local) || remote <= local)
            return false;

        var bytes = await _httpClient.GetByteArrayAsync(manifest.PackageUrl, cancellationToken);
        var actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (!string.Equals(actual, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Update package checksum verification failed.");

        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SohailOS", "updates", manifest.Version);
        Directory.CreateDirectory(root);
        var package = Path.Combine(root, "SohailOS-update.zip");
        await File.WriteAllBytesAsync(package, bytes, cancellationToken);

        var script = Path.Combine(AppContext.BaseDirectory, "scripts", "apply-update.ps1");
        if (!File.Exists(script))
            return false;

        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" -Package \"{package}\" -Target \"{AppContext.BaseDirectory}\" -Pid {Environment.ProcessId}",
            UseShellExecute = false,
            CreateNoWindow = true
        });
        return true;
    }
}
