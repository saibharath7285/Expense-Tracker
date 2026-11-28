using System.Text.RegularExpressions;

namespace ExpenseTracker.API.Services;

public static class CopilotResponseSanitizer
{
    public static string Sanitize(string reply)
    {
        if (string.IsNullOrWhiteSpace(reply))
        {
            return string.Empty;
        }

        var sanitized = reply;

        // Replace phrases like "use the 'get_categories' command" or "call the search_knowledge tool".
        sanitized = Regex.Replace(
            sanitized,
            @"\b(?:use|call|invoke)\s+the\s+['""`]?[a-z]+_[a-z0-9_]+['""`]?(?:\s+(?:MCP\s+)?(?:function|tool|command))?\b",
            "tell me the details directly",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // Replace phrases like "the get_categories command" or "the search_knowledge MCP tool".
        sanitized = Regex.Replace(
            sanitized,
            @"\bthe\s+['""`]?[a-z]+_[a-z0-9_]+['""`]?(?:\s+(?:MCP\s+)?(?:function|tool|command))\b",
            "the assistant",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // Replace any remaining snake_case tool names (with optional backticks and optional tool/command/function suffix).
        sanitized = Regex.Replace(
            sanitized,
            @"(?<![\p{L}\p{N}_/])`?[a-z]+(?:_[a-z0-9]+)+`?(?:\s+(?:MCP\s+)?(?:tool|command|function))?(?![\p{L}\p{N}_/])",
            "the assistant",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // Strip leaked internal database IDs (e.g., "The expense ID is 4004.", "(ID: 4004)", "with ID 4004").
        sanitized = Regex.Replace(
            sanitized,
            @"\s*(?:The\s+)?(?:expense|category|subcategory)\s+ID\s+is\s+\d+\.?",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        sanitized = Regex.Replace(
            sanitized,
            @"\s*\(\s*ID\s*:\s*\d+\s*\)",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        sanitized = Regex.Replace(
            sanitized,
            @"\s*\bwith\s+(?:expense\s+|category\s+|subcategory\s+)?ID\s+\d+\b",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return sanitized.Trim();
    }
}
