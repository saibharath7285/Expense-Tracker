USE [AdventureWorksLT2022];
GO

CREATE OR ALTER PROCEDURE dbo.spGetKnowledgeChunkHashes
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Id, ContentHash
    FROM dbo.KnowledgeChunks;
END;
GO
