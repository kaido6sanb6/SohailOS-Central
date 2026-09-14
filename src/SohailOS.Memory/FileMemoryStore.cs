using System.Text.Json;
using SohailOS.Core;

namespace SohailOS.Memory;

/// <summary>
/// Small local JSON memory store for the desktop MVP. The file path is supplied by the host.
/// </summary>
public sealed class FileMemoryStore : IMemoryStore
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public FileMemoryStore(string path)
    {
        _path = path;
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
    }

    public async Task SaveAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var items = await LoadAsync(cancellationToken);
            items[key] = value;
            await WriteAsync(items, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var items = await LoadAsync(cancellationToken);
            return items.TryGetValue(key, out var value) ? value : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<Dictionary<string, string>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
            return new Dictionary<string, string>();

        await using var stream = File.OpenRead(_path);
        return await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream, cancellationToken: cancellationToken)
            ?? new Dictionary<string, string>();
    }

    private async Task WriteAsync(Dictionary<string, string> items, CancellationToken cancellationToken)
    {
        var temp = $"{_path}.tmp";
        await using (var stream = File.Create(temp))
        {
            await JsonSerializer.SerializeAsync(stream, items, new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
        }
        File.Move(temp, _path, true);
    }
}
