using ExpenseTracker.API.Contracts;
using ExpenseTracker.API.Entities;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.API.Controllers
{
    [ApiController]
    [Route("api/mcp")]
    public class McpController : ControllerBase
    {
        private static readonly HashSet<string> AllowedTools = new(StringComparer.OrdinalIgnoreCase)
        {
            "get_expenses",
            "get_categories",
            "get_spending_summary",
            "add_expense",
            "update_expense",
            "delete_expense",
        };

        private readonly IMcpTestClient _mcpClient;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<McpController> _logger;

        public McpController(
            IMcpTestClient mcpClient,
            IWebHostEnvironment environment,
            ILogger<McpController> logger)
        {
            _mcpClient = mcpClient;
            _environment = environment;
            _logger = logger;
        }

        [HttpPost("tools")]
        public async Task<ActionResult<McpToolResponse>> CallTool(
            [FromBody] McpToolRequest request,
            CancellationToken cancellationToken)
        {
            if (!_environment.IsDevelopment())
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(request.ToolName) ||
                !AllowedTools.Contains(request.ToolName))
            {
                return BadRequest("The requested MCP tool is not allowed.");
            }

            try
            {
                var response = await _mcpClient.CallToolAsync(
                    request.ToolName,
                    request.Arguments,
                    cancellationToken);

                return Ok(response);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "MCP tool {ToolName} failed.",
                    request.ToolName);

                return Problem(
                    title: "MCP tool execution failed.",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status502BadGateway);
            }
        }
    }
}
