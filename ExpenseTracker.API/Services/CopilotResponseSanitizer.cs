using System.Text.RegularExpressions;

namespace ExpenseTracker.API.Services;

public static class CopilotResponseSanitizer
{
    private static readonly (string ToolName, string UserFacingText)[] InternalTools =
    [
        ("get_expenses", "current expenses"),
        ("get_categories", "available categories"),
        ("get_spending_summary", "current spending summary"),
        ("add_expense", "expense entry"),
        ("update_expense", "expense update"),
        ("delete_expense", "expense deletion"),
        ("search_knowledge", "application guidance")
    ];

    public static string Sanitize(string reply)
    {
        if (string.IsNullOrWhiteSpace(reply))
        {
            return string.Empty;
        }

        var sanitized = reply;

        foreach (var (toolName, userFacingText) in InternalTools)
        {
            var pattern =
                $@"(?<![\p{{L}}\p{{N}}_`])(?:`{Regex.Escape(toolName)}`|{Regex.Escape(toolName)})(?:\s+(?:MCP\s+)?(?:tool|command|function))?(?![\p{{L}}\p{{N}}_])";

            sanitized = Regex.Replace(
                sanitized,
                pattern,
                userFacingText,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        // Catch hallucinated or generic snake_case function/tool references (e.g., "use the 'record_expense' function").
        sanitized = Regex.Replace(
            sanitized,
            @"\b(?:use|call|invoke)\s+the\s+['""`]?[a-z]+_[a-z0-9_]+['""`]?\s+(?:MCP\s+)?(?:function|tool)\b",
            "tell me the details directly",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        sanitized = Regex.Replace(
            sanitized,
            @"\bthe\s+['""`]?[a-z]+_[a-z0-9_]+['""`]?\s+(?:MCP\s+)?(?:function|tool)\b",
            "the assistant",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return sanitized.Trim();
    }
}
