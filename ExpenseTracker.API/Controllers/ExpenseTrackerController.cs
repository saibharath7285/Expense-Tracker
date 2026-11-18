using ExpenseTracker.API.Contracts;
using ExpenseTracker.API.Entities;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExpenseTrackerController : ControllerBase
    {
        private readonly IExpenseTrackerService _expenseTrackerService;

        public ExpenseTrackerController(IExpenseTrackerService expenseTrackerService)
        {
            _expenseTrackerService = expenseTrackerService;
        }

        [HttpGet("expenses")]
        public async Task<IActionResult> GetExpenses()
        {
            var expenses = await _expenseTrackerService.GetExpensesAsync();
            return Ok(expenses);
        }

        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await _expenseTrackerService.GetCategoriesAsync();
            return Ok(categories);
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSpendingSummary()
        {
            var summary = await _expenseTrackerService.GetSpendingSummaryAsync();
            return Ok(summary);
        }

        [HttpPost("expenses")]
        public async Task<IActionResult> AddExpense([FromBody] AddExpense_Params request)
        {
            if (request == null)
                return BadRequest("Invalid expense payload.");

            var newId = await _expenseTrackerService.AddExpenseAsync(request);
            return Ok(new { id = newId, message = "Expense added successfully." });
        }

        [HttpPut("expenses/{id}")]
        public async Task<IActionResult> UpdateExpense(int id, [FromBody] UpdateExpense_Params request)
        {
            if (request == null || id <= 0)
                return BadRequest("Invalid expense data.");

            request.Id = id;
            var success = await _expenseTrackerService.UpdateExpenseAsync(request);
            if (!success)
                return NotFound($"Expense with ID {id} not found.");

            return Ok(new { message = "Expense updated successfully." });
        }

        [HttpDelete("expenses/{id}")]
        public async Task<IActionResult> DeleteExpense(int id)
        {
            if (id <= 0)
                return BadRequest("Invalid ID.");

            var success = await _expenseTrackerService.DeleteExpenseAsync(new DeleteExpense_Params { Id = id });
            if (!success)
                return NotFound($"Expense with ID {id} not found.");

            return Ok(new { message = "Expense deleted successfully." });
        }
    }
}
