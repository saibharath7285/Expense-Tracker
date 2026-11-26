using System.ComponentModel;
using System.Text.Json;
using ExpenseTracker.McpServer.Entities;
using ExpenseTracker.McpServer.Knowledge;
using ModelContextProtocol.Server;

namespace ExpenseTracker.McpServer.Tools;

[McpServerToolType]
public sealed class KnowledgeTools
{
    private readonly IKnowledgeSearchService _knowledgeSearchService;

    public KnowledgeTools(IKnowledgeSearchService knowledgeSearchService)
    {
        _knowledgeSearchService = knowledgeSearchService;
    }

    [McpServerTool(Name = "search_knowledge")]
    [Description(
        "Searches ExpenseTracker guidance and help documents. " +
        "Use this for explanations and application guidance, not live expense totals.")]
    public async Task<string> SearchKnowledge(
        [Description("The user's question or topic to search for.")] string query,
        CancellationToken cancellationToken)
    {
        var matches = await _knowledgeSearchService.SearchAsync(
            query,
            limit: 5,
            cancellationToken);

        return JsonSerializer.Serialize(new
        {
            query,
            matches
        }, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }
}
