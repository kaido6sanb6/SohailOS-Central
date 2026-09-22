namespace SohailOS.Ecosystem;

public static class HybridRanker
{
    public const string FusionVersion = "rrf-v1-k60";

    public static IReadOnlyList<KnowledgeHit> Fuse(
        IReadOnlyList<KnowledgeHit> lexical,
        IReadOnlyList<KnowledgeHit> vector,
        int limit,
        double lexicalWeight = 1.0,
        double vectorWeight = 1.0,
        int k = 60)
    {
        if (limit <= 0)
            return Array.Empty<KnowledgeHit>();

        var scores = new Dictionary<string, (KnowledgeHit Hit, double Score, bool Lexical, bool Vector)>(
            StringComparer.Ordinal);

        AddRanked(lexical, lexicalWeight, k, scores, isLexical: true);
        AddRanked(vector, vectorWeight, k, scores, isLexical: false);

        return scores.Values
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Hit.Provenance.ChunkId, StringComparer.Ordinal)
            .Take(limit)
            .Select(x => x.Hit with
            {
                Score = x.Score,
                RetrievalSource = x.Lexical && x.Vector ? "hybrid" :
                    x.Lexical ? "lexical" : "vector"
            })
            .ToArray();
    }

    private static void AddRanked(
        IReadOnlyList<KnowledgeHit> hits,
        double weight,
        int k,
        Dictionary<string, (KnowledgeHit Hit, double Score, bool Lexical, bool Vector)> scores,
        bool isLexical)
    {
        for (var i = 0; i < hits.Count; i++)
        {
            var hit = hits[i];
            var rank = i + 1;
            var contribution = weight / (k + rank);

            if (scores.TryGetValue(hit.Provenance.ChunkId, out var existing))
            {
                scores[hit.Provenance.ChunkId] =
                    (existing.Hit, existing.Score + contribution,
                        existing.Lexical || isLexical,
                        existing.Vector || !isLexical);
            }
            else
            {
                scores[hit.Provenance.ChunkId] =
                    (hit, contribution, isLexical, !isLexical);
            }
        }
    }
}
