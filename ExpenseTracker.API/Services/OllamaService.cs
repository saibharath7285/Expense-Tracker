using ExpenseTracker.API.Contracts;
using ExpenseTracker.API.Entities;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ExpenseTracker.API.Services
{
    public class OllamaService : ICopilotService
    {
        private readonly HttpClient _httpClient;
        private readonly IMcpTestClient _mcpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<OllamaService> _logger;

        public OllamaService(
            HttpClient httpClient,
            IMcpTestClient mcpClient,
            IConfiguration configuration,
            ILogger<OllamaService> logger)
        {
            _httpClient = httpClient;
            var timeoutSeconds = configuration.GetValue<int?>("Ollama:TimeoutSeconds") ?? 180;
            _httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds); // Accommodate multi-turn local CPU inference
            _mcpClient = mcpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<CopilotChatResponse> ChatAsync(CopilotChatRequest request, CancellationToken cancellationToken = default)
        {
            // 1. Read Ollama endpoint and model from appsettings.json
            var endpoint = _configuration["Ollama:Endpoint"] ?? "http://localhost:11434";
            var model = _configuration["Ollama:Model"] ?? "qwen2.5:3b";
            var chatUrl = $"{endpoint.TrimEnd('/')}/api/chat";
            var toolsExecuted = new List<ExecutedToolDto>();
            var messages = new List<object>();

            // 2. Add System Instruction
            messages.Add(new
            {
                role = "system",
                content = "You are Expense Copilot, an AI assistant for the ExpenseTracker application. " +
                          "You help users inspect their spending summary, list expenses, look up categories, " +
                          "record, update, or delete expenses, and answer general ExpenseTracker questions using the provided knowledge search tool. " +
                          "Use the search_knowledge tool for application guidance and explanations. " +
                          "Always use the current expense tools for financial data and database changes rather than guessing. " +
                          "Never mention internal tool names, function names, MCP messages, tool arguments, or raw tool output in the final answer. " +
                          "Never invent CLI commands, flags, shortcuts, or features that are not in the retrieved application guidance or tool output. " +
                          "When asked how to perform an action, explain in plain language what details the user can provide in chat or in the app. " +
                          "Do not use knowledge documents as proof of current totals or permission to change data. " +
                          "Format dollar amounts with $. Be direct, concise, and brief in your response (at most 2-3 sentences or compact bullet points)."
            });

            // 3. Add conversational history if provided
            if (request.History != null && request.History.Count > 0)
            {
                foreach (var hist in request.History.TakeLast(6))
                {
                    messages.Add(new
                    {
                        role = hist.Role.Equals("user", StringComparison.OrdinalIgnoreCase) ? "user" : "assistant",
                        content = hist.Content
                    });
                }
            }

            // 4. Add current user prompt
            messages.Add(new
            {
                role = "user",
                content = request.Message
            });

            // 5. Dynamically discover tools from the MCP Server (Single Source of Truth)
            var isKnowledgeQuestion = IsKnowledgeQuestion(request.Message);
            object[] toolDeclarations;
            try
            {
                toolDeclarations = await _mcpClient.GetToolDeclarationsAsync(cancellationToken);

                if (isKnowledgeQuestion)
                {
                    toolDeclarations = toolDeclarations
                        .Where(declaration => string.Equals(
                            GetToolName(declaration),
                            "search_knowledge",
                            StringComparison.Ordinal))
                        .ToArray();

                    _logger.LogInformation(
                        "Restricted informational request to the search_knowledge tool.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve tool declarations from MCP server");
                return new CopilotChatResponse(
                    Success: false,
                    Reply: "Unable to connect to the ExpenseTracker MCP server. Please ensure it is running at the configured MCP endpoint.",
                    ToolsExecuted: toolsExecuted,
                    Error: ex.Message);
            }

            // Guarantee RAG retrieval for informational questions so smaller local models cannot skip it or hallucinate
            if (isKnowledgeQuestion)
            {
                var queryArgs = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["query"] = request.Message
                };

                McpToolResponse knowledgeResult;
                try
                {
                    knowledgeResult = await _mcpClient.CallToolAsync("search_knowledge", queryArgs, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Execution of MCP tool search_knowledge failed");
                    return new CopilotChatResponse(
                        Success: false,
                        Reply: "Unable to retrieve application guidance right now. Please ensure the MCP server, SQL Server, and Ollama embedding model are running.",
                        ToolsExecuted: toolsExecuted,
                        Error: ex.Message);
                }

                toolsExecuted.Add(new ExecutedToolDto(
                    ToolName: "search_knowledge",
                    ArgumentsJson: JsonSerializer.Serialize(queryArgs),
                    Output: knowledgeResult.Output,
                    Success: knowledgeResult.Success));

                if (!knowledgeResult.Success)
                {
                    return new CopilotChatResponse(
                        Success: false,
                        Reply: "Unable to retrieve application guidance right now.",
                        ToolsExecuted: toolsExecuted,
                        Error: knowledgeResult.Output);
                }

                messages.Add(new
                {
                    role = "system",
                    content = "Retrieved ExpenseTracker application guidance for the user's question. " +
                              "Answer using ONLY the facts below and do not invent commands, flags, or unsupported steps:\n\n" +
                              knowledgeResult.Output
                });
            }

            // 6. Conversational ReAct loop (up to 5 turns): Ollama decides whether to invoke tools or reply
            const int maxTurns = 5;
            for (var turn = 0; turn < maxTurns; turn++)
            {
                var payload = new
                {
                    model = model,
                    stream = false,
                    keep_alive = "30m",
                    messages = messages,
                    tools = toolDeclarations
                };

                var jsonBody = JsonSerializer.Serialize(payload);
                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, chatUrl)
                {
                    Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
                };

                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.SendAsync(requestMessage, cancellationToken);
                }
                catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Ollama local inference timed out after {Timeout}s", _httpClient.Timeout.TotalSeconds);
                    return new CopilotChatResponse(
                        Success: false,
                        Reply: "The local model took longer than expected to process on CPU. Please try again or ask a more specific query.",
                        ToolsExecuted: toolsExecuted,
                        Error: ex.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to connect to local Ollama on {Url}", chatUrl);
                    return new CopilotChatResponse(
                        Success: false,
                        Reply: "Unable to reach local Ollama. Please make sure Ollama is running on your machine.",
                        ToolsExecuted: toolsExecuted,
                        Error: ex.Message);
                }

                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Ollama returned error ({StatusCode}): {Body}", response.StatusCode, responseBody);
                    return new CopilotChatResponse(
                        Success: false,
                        Reply: $"Ollama returned an error: {response.StatusCode}",
                        ToolsExecuted: toolsExecuted,
                        Error: responseBody);
                }

                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;
                if (!root.TryGetProperty("message", out var msgElement))
                {
                    return new CopilotChatResponse(Success: false, Reply: "Invalid response format from Ollama.", ToolsExecuted: toolsExecuted);
                }

                // 7. Check if Ollama requested any tool execution
                if (msgElement.TryGetProperty("tool_calls", out var toolCalls) &&
                    toolCalls.ValueKind == JsonValueKind.Array &&
                    toolCalls.GetArrayLength() > 0)
                {
                    // Append Ollama's assistant message (with tool_calls) to conversation history
                    messages.Add(JsonSerializer.Deserialize<object>(msgElement.GetRawText())!);

                    // Execute each tool call requested
                    await ExecuteToolCallsAsync(toolCalls, isKnowledgeQuestion, request.Message, messages, toolsExecuted, cancellationToken);

                    // Continue the loop so Ollama can read the tool output and generate the final answer
                    continue;
                }

                // 8. No more tools requested - this is the final natural language answer
                var reply = msgElement.TryGetProperty("content", out var contentProp)
                    ? contentProp.GetString() ?? string.Empty
                    : string.Empty;

                return new CopilotChatResponse(
                    Success: true,
                    Reply: CopilotResponseSanitizer.Sanitize(reply),
                    ToolsExecuted: toolsExecuted);
            }

            return new CopilotChatResponse(Success: false, Reply: "The assistant exceeded maximum conversational turns.", ToolsExecuted: toolsExecuted);
        }

        public async IAsyncEnumerable<string> ChatStreamAsync(
            CopilotChatRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var endpoint = _configuration["Ollama:Endpoint"] ?? "http://localhost:11434";
            var model = _configuration["Ollama:Model"] ?? "qwen2.5:3b";
            var chatUrl = $"{endpoint.TrimEnd('/')}/api/chat";
            var toolsExecuted = new List<ExecutedToolDto>();
            var messages = new List<object>();

            messages.Add(new
            {
                role = "system",
                content = "You are Expense Copilot, an AI assistant for the ExpenseTracker application. " +
                          "You help users inspect their spending summary, list expenses, look up categories, " +
                          "record, update, or delete expenses, and answer general ExpenseTracker questions using the provided knowledge search tool. " +
                          "Use the search_knowledge tool for application guidance and explanations. " +
                          "Always use the current expense tools for financial data and database changes rather than guessing. " +
                          "Never mention internal tool names, function names, MCP messages, tool arguments, or raw tool output in the final answer. " +
                          "Never invent CLI commands, flags, shortcuts, or features that are not in the retrieved application guidance or tool output. " +
                          "When asked how to perform an action, explain in plain language what details the user can provide in chat or in the app. " +
                          "Do not use knowledge documents as proof of current totals or permission to change data. " +
                          "Format dollar amounts with $. Be direct, concise, and brief in your response (at most 2-3 sentences or compact bullet points)."
            });

            if (request.History != null && request.History.Count > 0)
            {
                foreach (var hist in request.History.TakeLast(6))
                {
                    messages.Add(new
                    {
                        role = hist.Role.Equals("user", StringComparison.OrdinalIgnoreCase) ? "user" : "assistant",
                        content = hist.Content
                    });
                }
            }

            messages.Add(new { role = "user", content = request.Message });

            var isKnowledgeQuestion = IsKnowledgeQuestion(request.Message);
            var toolDeclarations = await _mcpClient.GetToolDeclarationsAsync(cancellationToken);

            if (isKnowledgeQuestion)
            {
                toolDeclarations = toolDeclarations
                    .Where(declaration => string.Equals(GetToolName(declaration), "search_knowledge", StringComparison.Ordinal))
                    .ToArray();

                var queryArgs = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["query"] = request.Message
                };

                var knowledgeResult = await _mcpClient.CallToolAsync("search_knowledge", queryArgs, cancellationToken);
                if (!knowledgeResult.Success)
                {
                    throw new InvalidOperationException(knowledgeResult.Output);
                }

                messages.Add(new
                {
                    role = "system",
                    content = "Retrieved ExpenseTracker application guidance for the user's question. " +
                              "Answer using ONLY the facts below and do not invent commands, flags, or unsupported steps:\n\n" +
                              knowledgeResult.Output
                });
            }

            const int maxTurns = 5;
            for (var turn = 0; turn < maxTurns; turn++)
            {
                var payload = new
                {
                    model = model,
                    stream = true,
                    keep_alive = "30m",
                    messages = messages,
                    tools = toolDeclarations
                };

                var jsonBody = JsonSerializer.Serialize(payload);
                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, chatUrl)
                {
                    Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
                };

                using var response = await _httpClient.SendAsync(
                    requestMessage,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    throw new HttpRequestException($"Ollama returned an error ({response.StatusCode}): {errBody}");
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var reader = new StreamReader(stream);

                JsonElement? pendingToolCalls = null;
                string? assistantRawMessage = null;

                while (await reader.ReadLineAsync(cancellationToken) is { } line)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    if (!root.TryGetProperty("message", out var msgElement))
                    {
                        continue;
                    }

                    if (msgElement.TryGetProperty("tool_calls", out var toolCalls) &&
                        toolCalls.ValueKind == JsonValueKind.Array &&
                        toolCalls.GetArrayLength() > 0)
                    {
                        pendingToolCalls = toolCalls.Clone();
                        assistantRawMessage = msgElement.GetRawText();
                        continue;
                    }

                    if (msgElement.TryGetProperty("content", out var contentProp))
                    {
                        var token = contentProp.GetString();
                        if (!string.IsNullOrEmpty(token))
                        {
                            yield return token;
                        }
                    }
                }

                if (pendingToolCalls.HasValue && assistantRawMessage is not null)
                {
                    messages.Add(JsonSerializer.Deserialize<object>(assistantRawMessage)!);
                    await ExecuteToolCallsAsync(pendingToolCalls.Value, isKnowledgeQuestion, request.Message, messages, toolsExecuted, cancellationToken);
                    continue;
                }

                yield break;
            }

            throw new InvalidOperationException("The assistant exceeded maximum conversational turns.");
        }

        private async Task ExecuteToolCallsAsync(
            JsonElement toolCalls,
            bool isKnowledgeQuestion,
            string userMessage,
            List<object> messages,
            List<ExecutedToolDto> toolsExecuted,
            CancellationToken cancellationToken)
        {
            foreach (var tc in toolCalls.EnumerateArray())
            {
                var functionProp = tc.GetProperty("function");
                var functionName = functionProp.GetProperty("name").GetString()!;

                if (isKnowledgeQuestion &&
                    !string.Equals(functionName, "search_knowledge", StringComparison.Ordinal))
                {
                    _logger.LogWarning(
                        "Blocked MCP tool {ToolName} for an informational request.",
                        functionName);

                    messages.Add(new
                    {
                        role = "tool",
                        content = "This is an informational request. Do not change expense data. Use the knowledge search capability to answer it."
                    });
                    continue;
                }

                var argsProp = functionProp.GetProperty("arguments");
                var convertedArgs = ConvertJsonElementToDictionary(argsProp);

                _logger.LogInformation("Ollama requested MCP tool: {ToolName}", functionName);

                McpToolResponse toolResult;
                try
                {
                    toolResult = await _mcpClient.CallToolAsync(functionName, convertedArgs, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Execution of MCP tool {ToolName} failed", functionName);
                    toolResult = new McpToolResponse(false, $"Error executing tool {functionName}: {ex.Message}");
                }

                toolsExecuted.Add(new ExecutedToolDto(
                    ToolName: functionName,
                    ArgumentsJson: argsProp.ValueKind != JsonValueKind.Undefined ? argsProp.GetRawText() : "{}",
                    Output: toolResult.Output,
                    Success: toolResult.Success));

                messages.Add(new
                {
                    role = "tool",
                    content = toolResult.Output
                });
            }
        }

        private static bool IsKnowledgeQuestion(string message)
        {
            var normalized = Regex.Replace(
                    message.Trim(),
                    @"\s+",
                    " ")
                .ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            var mutationPrefixes = new[]
            {
                "add ", "record ", "create ", "update ", "delete ", "remove "
            };

            if (mutationPrefixes.Any(normalized.StartsWith))
            {
                return false;
            }

            var knowledgePrefixes = new[]
            {
                "what information",
                "what details",
                "what do i need",
                "what is needed",
                "how do i ",
                "how can i ",
                "how should i ",
                "how to ",
                "explain how ",
                "tell me how ",
                "guide me ",
                "what does ",
                "do i need ",
                "can i skip ",
                "can i leave ",
                "is this required",
                "is this optional",
                "what is required",
                "what is optional",
                "which fields are required",
                "which fields are optional"
            };

            return knowledgePrefixes.Any(normalized.StartsWith);
        }

        private static string? GetToolName(object declaration)
        {
            var element = JsonSerializer.SerializeToElement(declaration);

            return element.TryGetProperty("function", out var function) &&
                   function.TryGetProperty("name", out var name)
                ? name.GetString()
                : null;
        }

        private static Dictionary<string, object?> ConvertJsonElementToDictionary(JsonElement element)
        {
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            if (element.ValueKind != JsonValueKind.Object)
            {
                return dict;
            }

            foreach (var prop in element.EnumerateObject())
            {
                dict[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number when prop.Value.TryGetInt32(out var i) => i,
                    JsonValueKind.Number when prop.Value.TryGetDecimal(out var d) => d,
                    JsonValueKind.Number => prop.Value.GetDouble(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    _ => prop.Value.GetRawText()
                };
            }

            return dict;
        }
    }
}
