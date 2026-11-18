USE [AdventureWorksLT2022];
GO

CREATE OR ALTER PROCEDURE dbo.spAddExpense
    @Title NVARCHAR(150),
    @CategoryId INT,
    @SubCategoryId INT,
    @Amount DECIMAL(18,2),
    @ExpenseDate DATETIME2,
    @Notes NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.Expenses (
        Title, 
        CategoryId, 
        SubCategoryId, 
        Amount, 
        ExpenseDate, 
        Notes, 
        CreateDateTime
    )
    VALUES (
        @Title, 
        @CategoryId, 
        @SubCategoryId, 
        @Amount, 
        @ExpenseDate, 
        @Notes, 
        SYSUTCDATETIME()
    );

    SELECT SCOPE_IDENTITY() AS NewExpenseId;
END;
GO