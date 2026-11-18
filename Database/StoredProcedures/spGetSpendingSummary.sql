USE [AdventureWorksLT2022];
GO

CREATE OR ALTER PROCEDURE dbo.spGetSpendingSummary
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        c.Name AS CategoryName,
        COUNT(e.Id) AS ExpenseCount,
        ISNULL(SUM(e.Amount), 0) AS TotalAmount
    FROM dbo.ExpenseCategories c
    LEFT JOIN dbo.Expenses e ON c.Id = e.CategoryId
    GROUP BY c.Name
    ORDER BY TotalAmount DESC;
END;
GO