using ExpenseTracker.API.Entities;

namespace ExpenseTracker.API.Contracts
{
    public interface IExpenseTrackerService
    {
        Task<List<Expense_Result>> GetExpensesAsync();
        Task<List<GetCategories_Result>> GetCategoriesAsync();
        Task<List<GetSpendingSummary_Result>> GetSpendingSummaryAsync();
        Task<int> AddExpenseAsync(AddExpense_Params expense);
        Task<bool> UpdateExpenseAsync(UpdateExpense_Params expense);
        Task<bool> DeleteExpenseAsync(DeleteExpense_Params expense);
    }
}
