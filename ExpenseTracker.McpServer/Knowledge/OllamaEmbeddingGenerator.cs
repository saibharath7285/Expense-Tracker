using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ExpenseTracker.McpServer.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExpenseTracker.McpServer.Knowledge;

public sealed class OllamaEmbeddingGenerator : IEmbeddingGenerator
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly ILogger<OllamaEmbeddingGenerator> _logger;

    public OllamaEmbeddingGenerator(
        HttpClient httpClient,
        IOptions<OllamaOptions> options,
        ILogger<OllamaEmbeddingGenerator> logger)
    {
        _httpClient = httpClient;
        _model = string.IsNullOrWhiteSpace(options.Value.EmbeddingModel)
            ? "embeddinggemma"
            : options.Value.EmbeddingModel;
        _logger = logger;
    }

    public async Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException(
                "Text is required to generate an embedding.",
                nameof(text));
        }

        var requestBody = new
        {
            model = _model,
            input = text,
            truncate = true,
            keep_alive = "30m"
        };

        var serializedRequest = JsonSerializer.Serialize(requestBody);
        using var requestContent = new StringContent(
            serializedRequest,
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.PostAsync(
            "api/embed",
            requestContent,
            cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Ollama embedding request failed with status {StatusCode} for model {Model}.",
                response.StatusCode,
                _model);

            throw new HttpRequestException(
                $"Ollama embedding request failed with status code {(int)response.StatusCode}.");
        }

        var embeddingResponse = JsonSerializer.Deserialize<OllamaEmbeddingResponse>(
            responseBody,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        var embedding = embeddingResponse?.Embeddings?.FirstOrDefault();
        if (embedding is null || embedding.Length == 0)
        {
            throw new InvalidOperationException(
                "Ollama returned no embedding values.");
        }

        return embedding;
    }

    private sealed class OllamaEmbeddingResponse
    {
        [JsonPropertyName("embeddings")]
        public float[][]? Embeddings { get; init; }
    }
}
