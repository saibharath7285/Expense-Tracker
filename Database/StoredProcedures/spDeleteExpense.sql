USE [AdventureWorksLT2022];
GO

CREATE OR ALTER PROCEDURE dbo.spDeleteExpense
    @Id INT
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM dbo.Expenses
    WHERE Id = @Id;

    SELECT CAST(@@ROWCOUNT AS INT) AS RowsAffected;
END;
GO