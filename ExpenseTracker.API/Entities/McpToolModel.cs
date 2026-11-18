namespace ExpenseTracker.API.Entities
{
    public sealed record McpToolRequest(
        string ToolName,
        Dictionary<string, object?>? Arguments);

    public sealed record McpToolResponse(
        bool Success,
        string Output);
}
