using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ExpenseTracker.API.Contracts;
using ExpenseTracker.API.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ExpenseTracker.API.Services
{
    public class GeminiCopilotService : ICopilotService
    {
        private readonly HttpClient _httpClient;
        private readonly IMcpTestClient _mcpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GeminiCopilotService> _logger;

        public GeminiCopilotService(
            HttpClient httpClient,
            IMcpTestClient mcpClient,
            IConfiguration configuration,
            ILogger<GeminiCopilotService> logger)
        {
            _httpClient = httpClient;
            _mcpClient = mcpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<CopilotChatResponse> ChatAsync(
            CopilotChatRequest request,
            CancellationToken cancellationToken = default)
        {
            var apiKey = _configuration["Gemini:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return new CopilotChatResponse(
                    Success: false,
                    Reply: "Gemini API key is not configured. Please set your Gemini API key in ExpenseTracker.API's appsettings.json under 'Gemini:ApiKey' or set the GEMINI_API_KEY environment variable.",
                    ToolsExecuted: new List<ExecutedToolDto>(),
                    Error: "MISSING_API_KEY");
            }

            var model = _configuration["Gemini:Model"];
            if (string.IsNullOrWhiteSpace(model))
            {
                model = "gemini-2.0-flash";
            }

            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

            var toolsExecuted = new List<ExecutedToolDto>();
            var contents = new List<object>();

            // Append conversational history if provided
            if (request.History != null && request.History.Count > 0)
            {
                foreach (var hist in request.History.TakeLast(8))
                {
                    var geminiRole = hist.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ||
                                     hist.Role.Equals("model", StringComparison.OrdinalIgnoreCase)
                                        ? "model"
                                        : "user";

                    contents.Add(new
                    {
                        role = geminiRole,
                        parts = new object[] { new { text = hist.Content } }
                    });
                }
            }

            // Append current user message
            contents.Add(new
            {
                role = "user",
                parts = new object[] { new { text = request.Message } }
            });

            var payload = new
            {
                system_instruction = new
                {
                    parts = new object[]
                    {
                        new
                        {
                            text = "You are Expense Copilot, an AI assistant for the ExpenseTracker application. " +
                                   "You help users inspect their spending summary, list expenses, look up categories, " +
                                   "and record or delete expenses using the provided tools. " +
                                   "Always call the appropriate tool when the user asks about their financial data rather than guessing. " +
                                   "Format dollar amounts with $. Be concise, clear, and helpful in your explanations."
                        }
                    }
                },
                contents,
                tools = GetToolDeclarations()
            };

            const int maxTurns = 5;
            for (var turn = 0; turn < maxTurns; turn++)
            {
                var jsonBody = JsonSerializer.Serialize(payload);
                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
                };

                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.SendAsync(requestMessage, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to connect to Gemini API.");
                    return new CopilotChatResponse(
                        Success: false,
                        Reply: "Unable to reach the Gemini API service. Please verify your internet connection.",
                        ToolsExecuted: toolsExecuted,
                        Error: ex.Message);
                }

                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Gemini API error ({StatusCode}): {ResponseBody}", response.StatusCode, responseBody);
                    return new CopilotChatResponse(
                        Success: false,
                        Reply: $"Gemini API returned an error ({response.StatusCode}): {ExtractErrorMessage(responseBody)}",
                        ToolsExecuted: toolsExecuted,
                        Error: responseBody);
                }

                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;

                if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
                {
                    return new CopilotChatResponse(
                        Success: false,
                        Reply: "Gemini did not return any response content.",
                        ToolsExecuted: toolsExecuted);
                }

                var candidate = candidates[0];
                var contentProp = candidate.GetProperty("content");
                var parts = contentProp.GetProperty("parts");

                // Check for function calls
                var functionCallFound = false;
                string? functionName = null;
                JsonElement functionArgs = default;

                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("functionCall", out var fc))
                    {
                        functionCallFound = true;
                        functionName = fc.GetProperty("name").GetString();
                        if (fc.TryGetProperty("args", out var args))
                        {
                            functionArgs = args;
                        }
                        break;
                    }
                }

                if (!functionCallFound || string.IsNullOrWhiteSpace(functionName))
                {
                    // Model returned final text answer
                    var replyBuilder = new StringBuilder();
                    foreach (var part in parts.EnumerateArray())
                    {
                        if (part.TryGetProperty("text", out var textProp))
                        {
                            replyBuilder.Append(textProp.GetString());
                        }
                    }

                    return new CopilotChatResponse(
                        Success: true,
                        Reply: replyBuilder.ToString().Trim(),
                        ToolsExecuted: toolsExecuted);
                }

                // Execute the requested MCP tool
                var convertedArgs = ConvertJsonElementToDictionary(functionArgs);
                _logger.LogInformation("Gemini requested MCP tool: {ToolName}", functionName);

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
                    ArgumentsJson: functionArgs.ValueKind != JsonValueKind.Undefined ? functionArgs.GetRawText() : "{}",
                    Output: toolResult.Output,
                    Success: toolResult.Success));

                // Append the model's functionCall turn
                contents.Add(new
                {
                    role = "model",
                    parts = new object[]
                    {
                        new
                        {
                            functionCall = new
                            {
                                name = functionName,
                                args = convertedArgs
                            }
                        }
                    }
                });

                // Append the functionResponse turn
                contents.Add(new
                {
                    role = "function",
                    parts = new object[]
                    {
                        new
                        {
                            functionResponse = new
                            {
                                name = functionName,
                                response = new
                                {
                                    output = toolResult.Output,
                                    success = toolResult.Success
                                }
                            }
                        }
                    }
                });

                // Update payload contents for next iteration
                payload = new
                {
                    payload.system_instruction,
                    contents,
                    payload.tools
                };
            }

            return new CopilotChatResponse(
                Success: false,
                Reply: "The assistant reached the maximum number of tool calls for this request.",
                ToolsExecuted: toolsExecuted);
        }

        private static object[] GetToolDeclarations()
        {
            return new object[]
            {
                new
                {
                    function_declarations = new object[]
                    {
                        new
                        {
                            name = "get_spending_summary",
                            description = "Retrieves spending totals grouped by category, showing expense count and total amount spent.",
                            parameters = new { type = "OBJECT", properties = new { } }
                        },
                        new
                        {
                            name = "get_expenses",
                            description = "Retrieves all recorded expenses with categories, amounts, dates, and notes.",
                            parameters = new { type = "OBJECT", properties = new { } }
                        },
                        new
                        {
                            name = "get_categories",
                            description = "Retrieves all available expense categories and their subcategories with IDs.",
                            parameters = new { type = "OBJECT", properties = new { } }
                        },
                        new
                        {
                            name = "add_expense",
                            description = "Records a new expense in the database.",
                            parameters = new
                            {
                                type = "OBJECT",
                                properties = new Dictionary<string, object>
                                {
                                    ["title"] = new { type = "STRING", description = "Title or description of the expense (e.g. 'Lunch with Client')" },
                                    ["categoryId"] = new { type = "INTEGER", description = "Category ID (e.g. 1 for Food & Dining)" },
                                    ["subCategoryId"] = new { type = "INTEGER", description = "SubCategory ID (e.g. 1 for Groceries, 2 for Restaurants)" },
                                    ["amount"] = new { type = "NUMBER", description = "The expense amount in dollars (e.g. 45.50)" },
                                    ["expenseDate"] = new { type = "STRING", description = "Date of expense in YYYY-MM-DD or ISO 8601 format" },
                                    ["notes"] = new { type = "STRING", description = "Optional notes or comments" }
                                },
                                required = new[] { "title", "categoryId", "subCategoryId", "amount", "expenseDate" }
                            }
                        },
                        new
                        {
                            name = "update_expense",
                            description = "Updates an existing expense record by ID.",
                            parameters = new
                            {
                                type = "OBJECT",
                                properties = new Dictionary<string, object>
                                {
                                    ["id"] = new { type = "INTEGER", description = "Unique ID of the expense" },
                                    ["title"] = new { type = "STRING", description = "Updated title" },
                                    ["categoryId"] = new { type = "INTEGER", description = "Updated category ID" },
                                    ["subCategoryId"] = new { type = "INTEGER", description = "Updated subcategory ID" },
                                    ["amount"] = new { type = "NUMBER", description = "Updated amount in dollars" },
                                    ["expenseDate"] = new { type = "STRING", description = "Updated date in YYYY-MM-DD format" },
                                    ["notes"] = new { type = "STRING", description = "Updated notes" }
                                },
                                required = new[] { "id", "title", "categoryId", "subCategoryId", "amount", "expenseDate" }
                            }
                        },
                        new
                        {
                            name = "delete_expense",
                            description = "Deletes an expense record by its unique ID.",
                            parameters = new
                            {
                                type = "OBJECT",
                                properties = new Dictionary<string, object>
                                {
                                    ["id"] = new { type = "INTEGER", description = "The unique ID of the expense to delete" }
                                },
                                required = new[] { "id" }
                            }
                        }
                    }
                }
            };
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
                dict[prop.Name] = ConvertJsonValue(prop.Value);
            }

            return dict;
        }

        private static object? ConvertJsonValue(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    if (element.TryGetInt32(out var intVal))
                    {
                        return intVal;
                    }
                    if (element.TryGetInt64(out var longVal))
                    {
                        return longVal;
                    }
                    if (element.TryGetDecimal(out var decVal))
                    {
                        return decVal;
                    }
                    return element.GetDouble();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Null:
                    return null;
                default:
                    return element.GetRawText();
            }
        }

        private static string ExtractErrorMessage(string responseBody)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                if (doc.RootElement.TryGetProperty("error", out var errorProp) &&
                    errorProp.TryGetProperty("message", out var msgProp))
                {
                    return msgProp.GetString() ?? responseBody;
                }
            }
            catch
            {
                // fallback
            }

            return responseBody;
        }
    }
}
