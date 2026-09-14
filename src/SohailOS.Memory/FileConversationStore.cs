using System.Text.Json;
using SohailOS.Core;

namespace SohailOS.Memory;

public sealed class FileConversationStore : IConversationStore
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public FileConversationStore(string path) => _path = path;

    public async Task<IReadOnlyList<ConversationTurn>> GetRecentAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0) return Array.Empty<ConversationTurn>();

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var turns = await LoadAsync(cancellationToken);
            return turns.TakeLast(limit).ToArray();
        }
        finally { _gate.Release(); }
    }

    public async Task AppendAsync(
        ConversationTurn turn,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var turns = await LoadAsync(cancellationToken);
            turns.Add(turn);
            await SaveAsync(turns, cancellationToken);
        }
        finally { _gate.Release(); }
    }

    private async Task<List<ConversationTurn>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path)) return new List<ConversationTurn>();
        await using var stream = File.OpenRead(_path);
        return await JsonSerializer.DeserializeAsync<List<ConversationTurn>>(stream, _json, cancellationToken)
            ?? new List<ConversationTurn>();
    }

    private async Task SaveAsync(List<ConversationTurn> turns, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

        var tempPath = _path + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, turns, _json, cancellationToken);
        }

        File.Move(tempPath, _path, true);
    }
}
