using ExpenseTracker.API.Contracts;
using ExpenseTracker.API.Entities;
using ExpenseTracker.API.Entities.DBConstants;

namespace ExpenseTracker.API.Services
{
    public class ExpenseTrackerService : IExpenseTrackerService
    {
        private readonly IRepositoryAsync<Expense_Result> _expenseRepository;
        private readonly IRepositoryAsync<GetCategories_Result> _categoryRepository;
        private readonly IRepositoryAsync<GetSpendingSummary_Result> _summaryRepository;
        private readonly IRepositoryAsync<AddExpense_Result> _addExpenseRepository;
        private readonly IRepositoryAsync<NoOutputResult> _noOutputRepository;

        public ExpenseTrackerService(
            IRepositoryAsync<Expense_Result> expenseRepository,
            IRepositoryAsync<GetCategories_Result> categoryRepository,
            IRepositoryAsync<GetSpendingSummary_Result> summaryRepository,
            IRepositoryAsync<AddExpense_Result> addExpenseRepository,
            IRepositoryAsync<NoOutputResult> noOutputRepository)
        {
            _expenseRepository = expenseRepository;
            _categoryRepository = categoryRepository;
            _summaryRepository = summaryRepository;
            _addExpenseRepository = addExpenseRepository;
            _noOutputRepository = noOutputRepository;
        }

        public async Task<List<Expense_Result>> GetExpensesAsync()
        {
            return await _expenseRepository.ExecuteStoredProcedureAsync(StoredProcedureConstants.GET_EXPENSES);
        }

        public async Task<List<GetCategories_Result>> GetCategoriesAsync()
        {
            return await _categoryRepository.ExecuteStoredProcedureAsync(StoredProcedureConstants.GET_CATEGORIES);
        }

        public async Task<List<GetSpendingSummary_Result>> GetSpendingSummaryAsync()
        {
            return await _summaryRepository.ExecuteStoredProcedureAsync(StoredProcedureConstants.GET_SPENDING_SUMMARY);
        }

        public async Task<int> AddExpenseAsync(AddExpense_Params model)
        {
            var result = await _addExpenseRepository.ExecuteStoredProcedureAsync(StoredProcedureConstants.ADD_EXPENSE, model);
            return Convert.ToInt32(result.FirstOrDefault()?.NewExpenseId ?? 0);
        }

        public async Task<bool> UpdateExpenseAsync(UpdateExpense_Params model)
        {
            var result = await _noOutputRepository.ExecuteStoredProcedureAsync(StoredProcedureConstants.UPDATE_EXPENSE, model);
            return result.FirstOrDefault()?.RowsAffected > 0;
        }

        public async Task<bool> DeleteExpenseAsync(DeleteExpense_Params model)
        {
            var result = await _noOutputRepository.ExecuteStoredProcedureAsync(StoredProcedureConstants.DELETE_EXPENSE, model);
            return result.FirstOrDefault()?.RowsAffected > 0;
        }
    }
}