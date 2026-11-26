namespace ExpenseTracker.McpServer.Entities;

public interface IEmbeddingGenerator
{
    Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default);
}

public interface IKnowledgeSearchService
{
    Task<IReadOnlyList<KnowledgeMatch>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken);
}

public sealed record KnowledgeMatch(
    string Source,
    string Section,
    string Content,
    double Score);

internal sealed record KnowledgeChunk(
    string Source,
    string Section,
    string Content);
