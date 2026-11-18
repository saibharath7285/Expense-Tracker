namespace ExpenseTracker.API.Entities;

public class AddExpense_Params : StoredProcedureParams
{
    public string Title { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public int SubCategoryId { get; set; }
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; }
    public string? Notes { get; set; }
}

public class UpdateExpense_Params : StoredProcedureParams
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public int SubCategoryId { get; set; }
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; }
    public string? Notes { get; set; }
}

public class DeleteExpense_Params : StoredProcedureParams
{
    public int Id { get; set; }
}