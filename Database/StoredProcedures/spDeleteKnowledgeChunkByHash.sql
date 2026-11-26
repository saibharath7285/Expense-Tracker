USE [AdventureWorksLT2022];
GO

CREATE OR ALTER PROCEDURE dbo.spDeleteKnowledgeChunkByHash
    @ContentHash CHAR(64)
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM dbo.KnowledgeChunks
    WHERE ContentHash = @ContentHash;
END;
GO
