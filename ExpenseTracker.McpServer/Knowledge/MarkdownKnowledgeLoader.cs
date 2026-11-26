using System.Text.RegularExpressions;
using ExpenseTracker.McpServer.Entities;
using Microsoft.Extensions.Logging;

namespace ExpenseTracker.McpServer.Knowledge;

/// <summary>
/// Loads and chunks Markdown knowledge files for embedding and vector indexing,
/// invalidating its cache automatically when files change on disk.
/// </summary>
public sealed class MarkdownKnowledgeLoader : IDisposable
{
    private const int MaximumChunkCharacters = 1800;

    private static readonly Regex HeadingRegex = new(
        @"^(#{1,6})\s+(.+?)\s*$",
        RegexOptions.Compiled);

    private readonly string _knowledgeDirectory;
    private readonly ILogger<MarkdownKnowledgeLoader> _logger;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly FileSystemWatcher? _watcher;

    private volatile IReadOnlyList<KnowledgeChunk>? _chunks;

    public event Action? KnowledgeChanged;

    public MarkdownKnowledgeLoader(
        string knowledgeDirectory,
        ILogger<MarkdownKnowledgeLoader> logger)
    {
        if (string.IsNullOrWhiteSpace(knowledgeDirectory))
        {
            throw new ArgumentException(
                "The knowledge directory cannot be empty.",
                nameof(knowledgeDirectory));
        }

        _knowledgeDirectory = Path.GetFullPath(knowledgeDirectory);
        _logger = logger;

        if (Directory.Exists(_knowledgeDirectory))
        {
            _watcher = new FileSystemWatcher(_knowledgeDirectory, "*.md")
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            _watcher.Changed += OnKnowledgeFileChanged;
            _watcher.Created += OnKnowledgeFileChanged;
            _watcher.Deleted += OnKnowledgeFileChanged;
            _watcher.Renamed += OnKnowledgeFileChanged;
        }
    }

    internal async Task<IReadOnlyList<KnowledgeChunk>> GetChunksAsync(
        CancellationToken cancellationToken)
    {
        var cached = _chunks;
        if (cached is not null)
        {
            return cached;
        }

        await _loadLock.WaitAsync(cancellationToken);

        try
        {
            cached = _chunks;
            if (cached is not null)
            {
                return cached;
            }

            cached = await LoadChunksAsync(cancellationToken);
            _chunks = cached;
            return cached;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private void OnKnowledgeFileChanged(object sender, FileSystemEventArgs e)
    {
        _chunks = null;
        _logger.LogInformation("Knowledge file changed ({ChangeType}): {Path}. Invalidating cache.", e.ChangeType, e.FullPath);
        KnowledgeChanged?.Invoke();
    }

    public void Dispose()
    {
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnKnowledgeFileChanged;
            _watcher.Created -= OnKnowledgeFileChanged;
            _watcher.Deleted -= OnKnowledgeFileChanged;
            _watcher.Renamed -= OnKnowledgeFileChanged;
            _watcher.Dispose();
        }

        _loadLock.Dispose();
    }

    private async Task<IReadOnlyList<KnowledgeChunk>> LoadChunksAsync(
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_knowledgeDirectory))
        {
            _logger.LogWarning(
                "Knowledge directory does not exist: {KnowledgeDirectory}",
                _knowledgeDirectory);
            return Array.Empty<KnowledgeChunk>();
        }

        var files = Directory.EnumerateFiles(
                _knowledgeDirectory,
                "*.md",
                SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var chunks = new List<KnowledgeChunk>();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var content = await File.ReadAllTextAsync(file, cancellationToken);
            var source = Path.GetRelativePath(_knowledgeDirectory, file)
                .Replace(Path.DirectorySeparatorChar, '/');

            chunks.AddRange(ParseDocument(source, content));
        }

        _logger.LogInformation(
            "Loaded {ChunkCount} knowledge chunks from {FileCount} Markdown files.",
            chunks.Count,
            files.Length);

        return chunks;
    }

    private static IReadOnlyList<KnowledgeChunk> ParseDocument(
        string source,
        string content)
    {
        var lines = RemoveFrontMatter(content)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

        var chunks = new List<KnowledgeChunk>();
        var sectionTitle = "General";
        var sectionLines = new List<string>();
        var insideCodeFence = false;

        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                insideCodeFence = !insideCodeFence;
                sectionLines.Add(line);
                continue;
            }

            var heading = !insideCodeFence
                ? HeadingRegex.Match(line)
                : Match.Empty;

            if (heading.Success)
            {
                AddSectionChunks(chunks, source, sectionTitle, sectionLines);
                sectionTitle = heading.Groups[2].Value.Trim();
                sectionLines.Clear();
                continue;
            }

            sectionLines.Add(line);
        }

        AddSectionChunks(chunks, source, sectionTitle, sectionLines);
        return chunks;
    }

    private static void AddSectionChunks(
        ICollection<KnowledgeChunk> chunks,
        string source,
        string sectionTitle,
        IReadOnlyCollection<string> sectionLines)
    {
        var sectionContent = string.Join('\n', sectionLines).Trim();
        if (string.IsNullOrWhiteSpace(sectionContent))
        {
            return;
        }

        var paragraphs = sectionContent
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var current = new List<string>();
        var currentLength = 0;

        foreach (var paragraph in paragraphs)
        {
            var proposedLength = currentLength == 0
                ? paragraph.Length
                : currentLength + paragraph.Length + 2;

            if (current.Count > 0 && proposedLength > MaximumChunkCharacters)
            {
                AddChunk(chunks, source, sectionTitle, current);
                current.Clear();
                currentLength = 0;
            }

            current.Add(paragraph);
            currentLength = currentLength == 0
                ? paragraph.Length
                : currentLength + paragraph.Length + 2;
        }

        AddChunk(chunks, source, sectionTitle, current);
    }

    private static void AddChunk(
        ICollection<KnowledgeChunk> chunks,
        string source,
        string sectionTitle,
        IReadOnlyCollection<string> contentLines)
    {
        var content = string.Join("\n\n", contentLines).Trim();
        if (!string.IsNullOrWhiteSpace(content))
        {
            chunks.Add(new KnowledgeChunk(source, sectionTitle, content));
        }
    }

    private static string RemoveFrontMatter(string content)
    {
        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (!normalized.StartsWith("---\n", StringComparison.Ordinal))
        {
            return normalized;
        }

        var closingMarker = normalized.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        return closingMarker < 0
            ? normalized
            : normalized[(closingMarker + 5)..];
    }
}
