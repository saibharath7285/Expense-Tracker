USE [AdventureWorksLT2022];
GO

CREATE OR ALTER PROCEDURE dbo.spGetCategories
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        c.Id AS CategoryId,
        c.Name AS CategoryName,
        s.Id AS SubCategoryId,
        s.Name AS SubCategoryName
    FROM dbo.ExpenseCategories c
    LEFT JOIN dbo.ExpenseSubCategories s ON c.Id = s.CategoryId
    ORDER BY c.Name, s.Name;
END;
GO