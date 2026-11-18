using System;
using System.Threading;
using System.Threading.Tasks;
using ExpenseTracker.API.Contracts;
using ExpenseTracker.API.Entities;
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
                return Ok(response);
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
    }
}
