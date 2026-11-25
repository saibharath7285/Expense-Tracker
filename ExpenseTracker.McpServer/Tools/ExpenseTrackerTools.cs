using ExpenseTracker.API.Contracts;
using ExpenseTracker.API.Entities;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;

namespace ExpenseTracker.McpServer.Tools
{
    [McpServerToolType]
    public class ExpenseTrackerTools
    {
        private readonly IExpenseTrackerService _expenseService;

        public ExpenseTrackerTools(IExpenseTrackerService expenseService)
        {
            _expenseService = expenseService;
        }

        [McpServerTool(Name = "get_expenses")]
        [Description("Retrieves all recorded expenses with categories, amounts, dates, and notes from the database.")]
        public async Task<string> GetExpenses()
        {
            var expenses = await _expenseService.GetExpensesAsync();
            return JsonSerializer.Serialize(expenses, new JsonSerializerOptions { WriteIndented = true });
        }

        [McpServerTool(Name = "get_categories")]
        [Description("Retrieves all available expense categories and their subcategories for organizing and recording expenses.")]
        public async Task<string> GetCategories()
        {
            var categories = await _expenseService.GetCategoriesAsync();
            return JsonSerializer.Serialize(categories, new JsonSerializerOptions { WriteIndented = true });
        }

        [McpServerTool(Name = "get_spending_summary")]
        [Description("Retrieves a spending summary broken down by category, showing expense count and total amount spent.")]
        public async Task<string> GetSpendingSummary()
        {
            var summary = await _expenseService.GetSpendingSummaryAsync();
            return JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
        }

        [McpServerTool(Name = "add_expense")]
        [Description("Records a new expense in the database.")]
        public async Task<string> AddExpense(
            [Description("Title or description of the expense (e.g. 'Team Lunch', 'Uber ride')")] string title,
            [Description("Category ID (use GetCategories tool to see available IDs)")] int categoryId,
            [Description("SubCategory ID (use GetCategories tool to see available IDs)")] int subCategoryId,
            [Description("The expense amount in dollars (e.g. 45.50)")] decimal amount,
            [Description("The date of the expense in YYYY-MM-DD or ISO 8601 format")] DateTime expenseDate,
            [Description("Optional notes or memo about the expense")] string? notes = null)
        {
            var newId = await _expenseService.AddExpenseAsync(new AddExpense_Params
            {
                Title = title,
                CategoryId = categoryId,
                SubCategoryId = subCategoryId,
                Amount = amount,
                ExpenseDate = expenseDate,
                Notes = notes
            });

            return JsonSerializer.Serialize(new
            {
                success = true,
                newExpenseId = newId,
                message = $"Expense '{title}' recorded successfully with ID {newId}."
            }, new JsonSerializerOptions { WriteIndented = true });
        }

        [McpServerTool(Name = "update_expense")]
        [Description("Updates an existing expense record by its unique ID.")]
        public async Task<string> UpdateExpense(
            [Description("The unique ID of the expense to update")] int id,
            [Description("Updated title or description")] string title,
            [Description("Updated Category ID")] int categoryId,
            [Description("Updated SubCategory ID")] int subCategoryId,
            [Description("Updated expense amount in dollars")] decimal amount,
            [Description("Updated date of the expense")] DateTime expenseDate,
            [Description("Optional updated notes")] string? notes = null)
        {
            var success = await _expenseService.UpdateExpenseAsync(new UpdateExpense_Params
            {
                Id = id,
                Title = title,
                CategoryId = categoryId,
                SubCategoryId = subCategoryId,
                Amount = amount,
                ExpenseDate = expenseDate,
                Notes = notes
            });

            return JsonSerializer.Serialize(new
            {
                success,
                message = success ? $"Expense with ID {id} updated successfully." : $"Expense with ID {id} not found."
            }, new JsonSerializerOptions { WriteIndented = true });
        }

        [McpServerTool(Name = "delete_expense")]
        [Description("Deletes an expense record from the database by its unique ID.")]
        public async Task<string> DeleteExpense(
            [Description("The unique ID of the expense to delete")] int id)
        {
            var success = await _expenseService.DeleteExpenseAsync(new DeleteExpense_Params { Id = id });

            return JsonSerializer.Serialize(new
            {
                success,
                message = success ? $"Expense with ID {id} deleted successfully." : $"Expense with ID {id} not found."
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
