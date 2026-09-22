using System.Text;

namespace SohailOS.Ecosystem;

public sealed record KnowledgeSearchExportResult(
    IReadOnlyList<string> Files,
    long TotalBytes,
    int ChunkCount);

public static class KnowledgeSearchExportService
{
    private const int DefaultMaxFileBytes = 3_500_000;

    public static async Task<KnowledgeSearchExportResult> ExportAsync(
        DataPlaneProvider provider,
        string outputDirectory,
        int maxFileBytes = DefaultMaxFileBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        if (maxFileBytes < 16 * 1024)
            throw new ArgumentOutOfRangeException(nameof(maxFileBytes));

        Directory.CreateDirectory(outputDirectory);

        var files = new List<string>();
        long totalBytes = 0;
        var totalChunks = 0;

        foreach (var repository in await provider.ListRepositoriesAsync(cancellationToken))
        {
            if (repository.TrustTier is KnowledgeTrustTier.Tombstoned or KnowledgeTrustTier.Unverified)
                continue;

            var buffer = new StringBuilder();
            var part = 1;

            async Task FlushAsync()
            {
                if (buffer.Length == 0)
                    return;

                var key = $"{Sanitize(repository.GithubNumericId.ToString())}/part-{part:D4}.md";
                var path = Path.Combine(outputDirectory, key.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);

                var bytes = Encoding.UTF8.GetBytes(buffer.ToString());
                if (bytes.Length > maxFileBytes)
                    throw new InvalidDataException($"Export item exceeds configured limit: {key} ({bytes.Length} bytes).");

                await File.WriteAllBytesAsync(path, bytes, cancellationToken);
                files.Add(path);
                totalBytes += bytes.LongLength;
                buffer.Clear();
                part++;
            }

            foreach (var document in await provider.ListDocumentsAsync(repository.RepoId, false, cancellationToken))
            {
                var result = await provider.GetDocumentAsync(document.DocId, "current", cancellationToken);
                if (result is null)
                    continue;

                foreach (var chunk in result.Chunks.OrderBy(x => x.Ordinal))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var source = await provider.GetSourceAsync(chunk.RevisionId, cancellationToken);
                    if (source is null)
                        continue;

                    var entry = BuildEntry(source, chunk);
                    var entryBytes = Encoding.UTF8.GetByteCount(entry);

                    if (entryBytes > maxFileBytes)
                        throw new InvalidDataException(
                            $"Single knowledge chunk exceeds export limit: {source.RepoFullName}:{source.Path}:{chunk.Ordinal}.");

                    if (Encoding.UTF8.GetByteCount(buffer.ToString()) + entryBytes > maxFileBytes)
                        await FlushAsync();

                    buffer.Append(entry);
                    totalChunks++;
                }
            }

            await FlushAsync();
        }

        return new KnowledgeSearchExportResult(files, totalBytes, totalChunks);
    }

    private static string BuildEntry(KnowledgeSourcePointer source, ChunkRecord chunk)
    {
        var header = $"""
            ---
            repo_id: {source.RepoId}
            repo_full_name: {source.RepoFullName}
            path: {source.Path}
            git_oid: {source.GitOid}
            blob_sha: {source.BlobSha}
            content_hash: {chunk.ContentHash}
            chunk_id: {chunk.ChunkId}
            ordinal: {chunk.Ordinal}
            byte_start: {chunk.ByteStart}
            byte_end: {chunk.ByteEnd}
            trust_tier: {source.TrustTier}
            indexed_at: {source.IndexedAt:O}
            permalink: {source.Permalink}
            ---

            """;

        return header + chunk.Text.TrimEnd() + "\n\n";
    }

    private static string Sanitize(string value) =>
        new(value.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_').ToArray());
}
