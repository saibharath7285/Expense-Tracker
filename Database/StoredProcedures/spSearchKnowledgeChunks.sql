USE [AdventureWorksLT2022];
GO

CREATE OR ALTER PROCEDURE dbo.spSearchKnowledgeChunks
    @QueryEmbeddingJson NVARCHAR(MAX),
    @Limit INT = 5,
    @MinSimilarity FLOAT = 0.45
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @QueryVector VECTOR(768) = CAST(@QueryEmbeddingJson AS VECTOR(768));

    SELECT TOP (@Limit)
        Source,
        Section,
        Content,
        CAST(1.0 - VECTOR_DISTANCE('cosine', Embedding, @QueryVector) AS FLOAT) AS Similarity
    FROM dbo.KnowledgeChunks
    WHERE (1.0 - VECTOR_DISTANCE('cosine', Embedding, @QueryVector)) >= @MinSimilarity
    ORDER BY VECTOR_DISTANCE('cosine', Embedding, @QueryVector) ASC;
END;
GO
