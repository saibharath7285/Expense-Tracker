USE [AdventureWorksLT2022];
GO

CREATE OR ALTER PROCEDURE dbo.spInsertKnowledgeChunk
    @Source NVARCHAR(260),
    @Section NVARCHAR(260),
    @Content NVARCHAR(MAX),
    @ContentHash CHAR(64),
    @EmbeddingJson NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.KnowledgeChunks (Source, Section, Content, ContentHash, Embedding)
    VALUES (@Source, @Section, @Content, @ContentHash, CAST(@EmbeddingJson AS VECTOR(768)));
END;
GO
