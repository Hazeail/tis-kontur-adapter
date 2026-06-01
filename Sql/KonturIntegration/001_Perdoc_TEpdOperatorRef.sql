/*
  SQL: 001_Perdoc_TEpdOperatorRef.sql
  НАЗНАЧЕНИЕ: Таблица внешних операторных идентификаторов для привязки к timeline ТИС.
*/

IF OBJECT_ID(N'Perdoc.dbo.TEpdOperatorRef', N'U') IS NULL
BEGIN
    CREATE TABLE Perdoc.dbo.TEpdOperatorRef
    (
        Id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        TimelineId BIGINT NOT NULL,
        OperatorCode NVARCHAR(32) NOT NULL,
        RefType NVARCHAR(64) NOT NULL,
        RefValue NVARCHAR(256) NOT NULL,
        SourceEventId NVARCHAR(128) NULL,
        CreatedAt DATETIME NOT NULL CONSTRAINT DF_TEpdOperatorRef_CreatedAt DEFAULT (GETDATE())
    );
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_TEpdOperatorRef_TimelineId'
      AND object_id = OBJECT_ID(N'Perdoc.dbo.TEpdOperatorRef')
)
BEGIN
    CREATE INDEX IX_TEpdOperatorRef_TimelineId
        ON Perdoc.dbo.TEpdOperatorRef (TimelineId);
END;
