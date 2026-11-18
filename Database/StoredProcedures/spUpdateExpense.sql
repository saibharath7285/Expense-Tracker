USE [AdventureWorksLT2022];
GO

CREATE OR ALTER PROCEDURE dbo.spUpdateExpense
    @Id INT,
    @Title NVARCHAR(150),
    @CategoryId INT,
    @SubCategoryId INT,
    @Amount DECIMAL(18,2),
    @ExpenseDate DATETIME2,
    @Notes NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Expenses
    SET 
        Title = @Title,
        CategoryId = @CategoryId,
        SubCategoryId = @SubCategoryId,
        Amount = @Amount,
        ExpenseDate = @ExpenseDate,
        Notes = @Notes
    WHERE Id = @Id;

    SELECT CAST(@@ROWCOUNT AS INT) AS RowsAffected;
END;
GO