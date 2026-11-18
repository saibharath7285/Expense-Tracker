namespace ExpenseTracker.API.Entities;

public class Expense_Result
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int SubCategoryId { get; set; }
    public string SubCategoryName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; }
    public string? Notes { get; set; }
    public DateTime? CreateDateTime { get; set; }
}
public class ExpenseModel : Expense_Result { }

public class GetCategories_Result
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int? SubCategoryId { get; set; }
    public string? SubCategoryName { get; set; }
}

public class GetSpendingSummary_Result
{
    public string CategoryName { get; set; } = string.Empty;
    public int ExpenseCount { get; set; }
    public decimal TotalAmount { get; set; }
}

public class AddExpense_Result
{
    public decimal NewExpenseId { get; set; }
}