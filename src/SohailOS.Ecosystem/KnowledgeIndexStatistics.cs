namespace SohailOS.Ecosystem;

public sealed record KnowledgeIndexStatistics(
    int Repositories,
    int Revisions,
    int Documents,
    int Chunks,
    int Embeddings);
