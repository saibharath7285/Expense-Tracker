using ExpenseTracker.API.Contracts;
using ExpenseTracker.API.Entities;
using System.Text;
using System.Text.Json;

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
                          "and record, update, or delete expenses using the provided tools. " +
                          "Always call the appropriate tool when the user asks about financial data rather than guessing. " +
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
            object[] toolDeclarations;
            try
            {
                toolDeclarations = await _mcpClient.GetToolDeclarationsAsync(cancellationToken);
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
                    foreach (var tc in toolCalls.EnumerateArray())
                    {
                        var functionProp = tc.GetProperty("function");
                        var functionName = functionProp.GetProperty("name").GetString()!;
                        var argsProp = functionProp.GetProperty("arguments");
                        var convertedArgs = ConvertJsonElementToDictionary(argsProp);

                        _logger.LogInformation("Ollama requested MCP tool: {ToolName}", functionName);

                        McpToolResponse toolResult;
                        try
                        {
                            // Call the MCP Server over HTTP via McpTestClient
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

                        // Feed the tool output back into Ollama with role "tool"
                        messages.Add(new
                        {
                            role = "tool",
                            content = toolResult.Output
                        });
                    }

                    // Continue the loop so Ollama can read the tool output and generate the final answer
                    continue;
                }

                // 8. No more tools requested - this is the final natural language answer
                var reply = msgElement.TryGetProperty("content", out var contentProp)
                    ? contentProp.GetString() ?? string.Empty
                    : string.Empty;

                return new CopilotChatResponse(Success: true, Reply: reply.Trim(), ToolsExecuted: toolsExecuted);
            }

            return new CopilotChatResponse(Success: false, Reply: "The assistant exceeded maximum conversational turns.", ToolsExecuted: toolsExecuted);
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
