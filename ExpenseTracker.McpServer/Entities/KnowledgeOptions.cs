namespace ExpenseTracker.McpServer.Entities;

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    public string Endpoint { get; set; } = "http://localhost:11434";
    public string EmbeddingModel { get; set; } = "embeddinggemma";
    public int EmbeddingTimeoutSeconds { get; set; } = 60;
}

public sealed class KnowledgeOptions
{
    public const string SectionName = "Knowledge";

    public string Path { get; set; } = string.Empty;
    public double MinSimilarity { get; set; } = 0.45;
}
