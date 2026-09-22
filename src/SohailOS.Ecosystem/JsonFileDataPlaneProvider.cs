using System.Text.Json;

namespace SohailOS.Ecosystem;

public sealed class JsonFileDataPlaneProvider : InMemoryDataPlaneProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _path;
    private readonly bool _autoSave;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    public JsonFileDataPlaneProvider(string path, bool autoSave = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
        _autoSave = autoSave;

        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        Load();
    }

    public override async Task<KnowledgeWriteResult> UpsertRepositoriesAsync(IEnumerable<RepositoryIdentity> repositories, CancellationToken cancellationToken = default)
    {
        var result = await base.UpsertRepositoriesAsync(repositories, cancellationToken);
        if (_autoSave)
            if (_autoSave)
            await SaveAsync(cancellationToken);
        return result;
    }

    public override async Task<KnowledgeWriteResult> UpsertRevisionsAsync(IEnumerable<SourceRevision> revisions, CancellationToken cancellationToken = default)
    {
        var result = await base.UpsertRevisionsAsync(revisions, cancellationToken);
        if (_autoSave)
            await SaveAsync(cancellationToken);
        return result;
    }

    public override async Task<KnowledgeWriteResult> UpsertDocumentsAsync(IEnumerable<DocumentRecord> documents, CancellationToken cancellationToken = default)
    {
        var result = await base.UpsertDocumentsAsync(documents, cancellationToken);
        if (_autoSave)
            await SaveAsync(cancellationToken);
        return result;
    }

    public override async Task<KnowledgeWriteResult> UpsertChunksAsync(IEnumerable<ChunkRecord> chunks, CancellationToken cancellationToken = default)
    {
        var result = await base.UpsertChunksAsync(chunks, cancellationToken);
        if (_autoSave)
            await SaveAsync(cancellationToken);
        return result;
    }

    public override async Task<KnowledgeWriteResult> UpsertEmbeddingsAsync(string generationId, IEnumerable<(string ChunkId, float[] Vector)> items, CancellationToken cancellationToken = default)
    {
        var result = await base.UpsertEmbeddingsAsync(generationId, items, cancellationToken);
        if (_autoSave)
            await SaveAsync(cancellationToken);
        return result;
    }

    public override async Task<KnowledgeWriteResult> TombstoneAsync(IEnumerable<string> ids, string reason, CancellationToken cancellationToken = default)
    {
        var result = await base.TombstoneAsync(ids, reason, cancellationToken);
        if (_autoSave)
            await SaveAsync(cancellationToken);
        return result;
    }

    public override async Task<KnowledgeWriteResult> RecordRunAsync(KnowledgeRunRecord run, CancellationToken cancellationToken = default)
    {
        var result = await base.RecordRunAsync(run, cancellationToken);
        if (_autoSave)
            await SaveAsync(cancellationToken);
        return result;
    }

    public override async Task<KnowledgeWriteResult> SetRevisionStateAsync(string revisionId, KnowledgeIndexState state, string? reason = null, CancellationToken cancellationToken = default)
    {
        var result = await base.SetRevisionStateAsync(revisionId, state, reason, cancellationToken);
        if (result.Success && _autoSave)
            if (_autoSave)
            await SaveAsync(cancellationToken);
        return result;
    }

    private void Load()
    {
        if (!File.Exists(_path))
            return;

        var json = File.ReadAllText(_path);
        if (string.IsNullOrWhiteSpace(json))
            return;

        var snapshot = JsonSerializer.Deserialize<KnowledgeIndexSnapshot>(json, JsonOptions);
        if (snapshot is not null)
            ImportSnapshot(snapshot);
    }

    public Task FlushAsync(CancellationToken cancellationToken = default)
        => SaveAsync(cancellationToken);

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        await _saveGate.WaitAsync(cancellationToken);
        try
        {
            var snapshot = ExportSnapshot();
            var temporary = _path + ".tmp";

            await using (var stream = File.Create(temporary))
                await JsonSerializer.SerializeAsync(stream, snapshot, JsonOptions, cancellationToken);

            File.Move(temporary, _path, true);
        }
        finally
        {
            _saveGate.Release();
        }
    }
}
