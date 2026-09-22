using System.Security.Cryptography;
using System.Text;

namespace SohailOS.Ecosystem;

public static class KnowledgeIdentity
{
    public static string RepositoryId(long githubNumericId) =>
        $"gh:repo:{githubNumericId}";

    public static string DocumentId(long githubNumericId, string normalizedPath) =>
        $"gh:doc:{githubNumericId}:{NormalizePath(normalizedPath)}";

    public static string RevisionId(string gitOid, string normalizedPath) =>
        $"gh:rev:{gitOid}:{NormalizePath(normalizedPath)}";

    public static string ChunkId(string text, int ordinal)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return $"gh:chunk:{hash}:{ordinal}";
    }

    public static string EmbeddingGenerationId(
        string provider,
        string model,
        int dimensions,
        string normalizationVersion) =>
        $"emb:{provider}:{model}:{dimensions}:{normalizationVersion}";

    public static string RunId(DateTimeOffset now) =>
        $"run:{now.UtcDateTime:yyyyMMddHHmmssfff}:{Guid.NewGuid():N}";

    public static string NormalizePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return path.Normalize(NormalizationForm.FormC).Replace('\\', '/');
    }
}
