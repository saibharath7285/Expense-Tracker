CREATE OR ALTER PROCEDURE dbo.spGetExpenses
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        e.Id,
        e.Title,
        e.CategoryId,
        c.Name AS CategoryName,
        e.SubCategoryId,
        sc.Name AS SubCategoryName,
        e.Amount,
        e.ExpenseDate,
        e.Notes,
        e.CreateDateTime
    FROM dbo.Expenses e
    INNER JOIN dbo.ExpenseCategories c ON e.CategoryId = c.Id
    INNER JOIN dbo.ExpenseSubCategories sc ON e.SubCategoryId = sc.Id
    ORDER BY e.ExpenseDate DESC, e.Id DESC;
END
GO
