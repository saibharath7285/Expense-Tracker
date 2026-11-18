namespace ExpenseTracker.API.Entities.DBConstants
{
    public class StoredProcedureConstants
    {
        public const string GET_EXPENSES = "dbo.spGetExpenses";
        public const string ADD_EXPENSE = "dbo.spAddExpense";
        public const string UPDATE_EXPENSE = "dbo.spUpdateExpense";
        public const string DELETE_EXPENSE = "dbo.spDeleteExpense";
        public const string GET_CATEGORIES = "dbo.spGetCategories";
        public const string GET_SPENDING_SUMMARY = "dbo.spGetSpendingSummary";
        public const string SP_GET_EXPENSES = GET_EXPENSES;
    }
}
