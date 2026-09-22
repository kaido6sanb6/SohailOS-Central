using System.Security.Cryptography;
using System.Text;

namespace SohailOS.Ecosystem;

public static class KnowledgeChunker
{
    public const int DefaultMaxCharacters = 4000;

    public static IReadOnlyList<ChunkRecord> Chunk(
        string text,
        string docId,
        string revisionId,
        string language,
        int maxCharacters = DefaultMaxCharacters)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(docId);
        ArgumentException.ThrowIfNullOrWhiteSpace(revisionId);

        if (maxCharacters <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCharacters));

        if (text.Length == 0)
            return Array.Empty<ChunkRecord>();

        var chunks = new List<ChunkRecord>();
        var ordinal = 0;
        var cursor = 0;

        while (cursor < text.Length)
        {
            var remaining = text.Length - cursor;
            var length = Math.Min(maxCharacters, remaining);

            if (length < remaining)
            {
                var newline = text.LastIndexOf('\n', cursor + length - 1, length);
                if (newline >= cursor + Math.Max(1, maxCharacters / 3))
                    length = newline - cursor + 1;
            }

            var chunkText = text.Substring(cursor, length);
            var byteStart = Encoding.UTF8.GetByteCount(text.AsSpan(0, cursor));
            var byteEnd = byteStart + Encoding.UTF8.GetByteCount(chunkText);
            var chunkHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(chunkText)))
                .ToLowerInvariant();
            var chunkId = $"gh:chunk:{chunkHash}:{ordinal}";

            chunks.Add(new ChunkRecord(
                chunkId,
                docId,
                revisionId,
                ordinal,
                byteStart,
                byteEnd,
                chunkHash,
                chunkText,
                language));

            cursor += length;
            ordinal++;
        }

        return chunks;
    }
}
