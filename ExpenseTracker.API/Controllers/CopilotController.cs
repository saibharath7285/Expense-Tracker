using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ExpenseTracker.API.Contracts;
using ExpenseTracker.API.Entities;
using ExpenseTracker.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ExpenseTracker.API.Controllers
{
    [ApiController]
    [Route("api/copilot")]
    public class CopilotController : ControllerBase
    {
        private readonly ICopilotService _copilotService;
        private readonly ILogger<CopilotController> _logger;

        public CopilotController(
            ICopilotService copilotService,
            ILogger<CopilotController> logger)
        {
            _copilotService = copilotService;
            _logger = logger;
        }

        [HttpPost("chat")]
        public async Task<ActionResult<CopilotChatResponse>> Chat(
            [FromBody] CopilotChatRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest("The chat message cannot be empty.");
            }

            try
            {
                var response = await _copilotService.ChatAsync(request, cancellationToken);
                return Ok(response with
                {
                    Reply = CopilotResponseSanitizer.Sanitize(response.Reply)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Copilot chat failed.");
                return Problem(
                    title: "Copilot chat failed.",
                    detail: ex.Message,
                    statusCode: 500);
            }
        }

        [HttpPost("chat/stream")]
        public async Task ChatStream(
            [FromBody] CopilotChatRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                await Response.WriteAsync("The chat message cannot be empty.", cancellationToken);
                return;
            }

            Response.Headers.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";

            try
            {
                var fullReply = new System.Text.StringBuilder();
                await foreach (var token in _copilotService.ChatStreamAsync(request, cancellationToken))
                {
                    fullReply.Append(token);
                    var payload = JsonSerializer.Serialize(new { token });
                    await Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                }

                var sanitizedReply = CopilotResponseSanitizer.Sanitize(fullReply.ToString());
                var donePayload = JsonSerializer.Serialize(new { done = true, sanitizedReply });
                await Response.WriteAsync($"data: {donePayload}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Client disconnected
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Copilot streaming chat failed.");
                var errorPayload = JsonSerializer.Serialize(new { error = ex.Message });
                await Response.WriteAsync($"data: {errorPayload}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
    }
}
