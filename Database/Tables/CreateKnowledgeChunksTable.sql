USE [AdventureWorksLT2022];
GO

IF OBJECT_ID(N'dbo.KnowledgeChunks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.KnowledgeChunks
    (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Source NVARCHAR(260) NOT NULL,
        Section NVARCHAR(260) NOT NULL,
        Content NVARCHAR(MAX) NOT NULL,
        ContentHash CHAR(64) NOT NULL,
        Embedding VECTOR(768) NOT NULL,
        UpdatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_KnowledgeChunks_UpdatedAtUtc DEFAULT SYSUTCDATETIME()
    );

    CREATE UNIQUE INDEX IX_KnowledgeChunks_ContentHash
        ON dbo.KnowledgeChunks (ContentHash);
END;
GO
