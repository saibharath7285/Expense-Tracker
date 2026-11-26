using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ExpenseTracker.API.Entities
{
    public sealed record ChatMessageDto(
        string Role,
        string Content);

    public sealed record ExecutedToolDto(
        string ToolName,
        string ArgumentsJson,
        string Output,
        bool Success);

    public sealed record CopilotChatRequest(
        string Message,
        List<ChatMessageDto>? History = null);

    public sealed record CopilotChatResponse(
        bool Success,
        string Reply,
        [property: JsonIgnore] List<ExecutedToolDto> ToolsExecuted,
        string? Error = null);
}
