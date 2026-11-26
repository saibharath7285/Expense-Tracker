using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ExpenseTracker.API.Entities.DBConstants;
using ExpenseTracker.McpServer.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExpenseTracker.McpServer.Knowledge;

/// <summary>
/// Stores and searches knowledge chunks in SQL Server 2025 using stored procedures
/// and native VECTOR(768) embeddings.
/// </summary>
public sealed class SqlKnowledgeSearchService : IKnowledgeSearchService
{
    private const int DefaultLimit = 5;
    private const int MaximumLimit = 10;
    private const int ExpectedVectorDimensions = 768;

    private readonly string _connectionString;
    private readonly IEmbeddingGenerator _embeddingGenerator;
    private readonly MarkdownKnowledgeLoader _chunkLoader;
    private readonly ILogger<SqlKnowledgeSearchService> _logger;
    private readonly double _minSimilarity;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    private volatile bool _synced;

    public SqlKnowledgeSearchService(
        string connectionString,
        IEmbeddingGenerator embeddingGenerator,
        MarkdownKnowledgeLoader chunkLoader,
        IOptions<KnowledgeOptions> options,
        ILogger<SqlKnowledgeSearchService> logger)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));
        }

        _connectionString = connectionString;
        _embeddingGenerator = embeddingGenerator;
        _chunkLoader = chunkLoader;
        _logger = logger;
        _minSimilarity = options.Value.MinSimilarity;

        _chunkLoader.KnowledgeChanged += () => _synced = false;
    }

    public async Task<IReadOnlyList<KnowledgeMatch>> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<KnowledgeMatch>();
        }

        var resultLimit = Math.Clamp(
            limit <= 0 ? DefaultLimit : limit,
            1,
            MaximumLimit);

        await EnsureSyncedAsync(cancellationToken);

        var queryEmbedding = await _embeddingGenerator.GenerateEmbeddingAsync(query, cancellationToken);
        if (queryEmbedding.Length != ExpectedVectorDimensions)
        {
            throw new InvalidOperationException(
                $"Query embedding dimension ({queryEmbedding.Length}) does not match stored vector dimension ({ExpectedVectorDimensions}).");
        }

        return await QueryVectorMatchesAsync(queryEmbedding, resultLimit, cancellationToken);
    }

    private async Task EnsureSyncedAsync(CancellationToken cancellationToken)
    {
        if (_synced)
        {
            return;
        }

        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            if (_synced)
            {
                return;
            }

            var chunks = await _chunkLoader.GetChunksAsync(cancellationToken);
            if (chunks.Count == 0)
            {
                _synced = true;
                return;
            }

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var existingHashes = await GetExistingHashesAsync(connection, cancellationToken);
            var desired = chunks
                .Select(chunk => new
                {
                    Chunk = chunk,
                    Hash = ComputeContentHash(chunk)
                })
                .ToList();

            var desiredHashSet = desired.Select(d => d.Hash).ToHashSet(StringComparer.Ordinal);
            var staleHashes = existingHashes.Keys.Where(h => !desiredHashSet.Contains(h)).ToList();

            if (staleHashes.Count > 0)
            {
                await DeleteStaleChunksAsync(connection, staleHashes, cancellationToken);
            }

            var insertedCount = 0;
            foreach (var item in desired)
            {
                if (existingHashes.ContainsKey(item.Hash))
                {
                    continue;
                }

                var embedding = await _embeddingGenerator.GenerateEmbeddingAsync(
                    FormatChunkForEmbedding(item.Chunk),
                    cancellationToken);

                if (embedding.Length != ExpectedVectorDimensions)
                {
                    throw new InvalidOperationException(
                        $"Embedding dimension mismatch for '{item.Chunk.Source}': expected {ExpectedVectorDimensions}, got {embedding.Length}.");
                }

                await InsertChunkAsync(connection, item.Chunk, item.Hash, embedding, cancellationToken);
                insertedCount++;
            }

            _synced = true;

            _logger.LogInformation(
                "SQL Server vector store synced: {TotalChunks} chunks ({Inserted} inserted, {Deleted} deleted).",
                desired.Count,
                insertedCount,
                staleHashes.Count);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task<IReadOnlyList<KnowledgeMatch>> QueryVectorMatchesAsync(
        float[] queryEmbedding,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(StoredProcedureConstants.SEARCH_KNOWLEDGE_CHUNKS, connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("@QueryEmbeddingJson", FormatVectorJson(queryEmbedding));
        command.Parameters.AddWithValue("@Limit", limit);
        command.Parameters.AddWithValue("@MinSimilarity", _minSimilarity);

        var matches = new List<KnowledgeMatch>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var source = reader.GetString(0);
            var section = reader.GetString(1);
            var content = reader.GetString(2);
            var similarity = reader.IsDBNull(3) ? 0d : reader.GetDouble(3);

            matches.Add(new KnowledgeMatch(
                source,
                section,
                content,
                Math.Round(similarity, 4)));
        }

        return matches;
    }

    private static async Task<Dictionary<string, int>> GetExistingHashesAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        var hashes = new Dictionary<string, int>(StringComparer.Ordinal);

        await using var command = new SqlCommand(StoredProcedureConstants.GET_KNOWLEDGE_CHUNK_HASHES, connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            hashes[reader.GetString(1).Trim()] = reader.GetInt32(0);
        }

        return hashes;
    }

    private static async Task DeleteStaleChunksAsync(
        SqlConnection connection,
        IReadOnlyList<string> staleHashes,
        CancellationToken cancellationToken)
    {
        foreach (var hash in staleHashes)
        {
            await using var command = new SqlCommand(StoredProcedureConstants.DELETE_KNOWLEDGE_CHUNK_BY_HASH, connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ContentHash", hash);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task InsertChunkAsync(
        SqlConnection connection,
        KnowledgeChunk chunk,
        string contentHash,
        float[] embedding,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(StoredProcedureConstants.INSERT_KNOWLEDGE_CHUNK, connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.AddWithValue("@Source", chunk.Source);
        command.Parameters.AddWithValue("@Section", chunk.Section);
        command.Parameters.AddWithValue("@Content", chunk.Content);
        command.Parameters.AddWithValue("@ContentHash", contentHash);
        command.Parameters.AddWithValue("@EmbeddingJson", FormatVectorJson(embedding));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string FormatChunkForEmbedding(KnowledgeChunk chunk)
    {
        return $"{chunk.Source} | {chunk.Section}\n\n{chunk.Content}";
    }

    private static string ComputeContentHash(KnowledgeChunk chunk)
    {
        var payload = $"{chunk.Source}\n{chunk.Section}\n{chunk.Content}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes);
    }

    private static string FormatVectorJson(float[] vector)
    {
        var sb = new StringBuilder(vector.Length * 10 + 2);
        sb.Append('[');
        for (var i = 0; i < vector.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append(vector[i].ToString("G9", CultureInfo.InvariantCulture));
        }

        sb.Append(']');
        return sb.ToString();
    }
}
