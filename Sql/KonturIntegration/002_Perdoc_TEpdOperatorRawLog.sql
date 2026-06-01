/*
  SQL: 002_Perdoc_TEpdOperatorRawLog.sql
  НАЗНАЧЕНИЕ: Таблица sanitized raw-логов для диагностики операторных вызовов.
*/

IF OBJECT_ID(N'Perdoc.dbo.TEpdOperatorRawLog', N'U') IS NULL
BEGIN
    CREATE TABLE Perdoc.dbo.TEpdOperatorRawLog
    (
        Id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        TimelineId BIGINT NULL,
        OperatorCode NVARCHAR(32) NOT NULL,
        Direction NVARCHAR(16) NOT NULL,
        Endpoint NVARCHAR(256) NULL,
        HttpStatus INT NULL,
        SanitizedPayload NVARCHAR(MAX) NULL,
        CreatedAt DATETIME NOT NULL CONSTRAINT DF_TEpdOperatorRawLog_CreatedAt DEFAULT (GETDATE())
    );
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_TEpdOperatorRawLog_TimelineId'
      AND object_id = OBJECT_ID(N'Perdoc.dbo.TEpdOperatorRawLog')
)
BEGIN
    CREATE INDEX IX_TEpdOperatorRawLog_TimelineId
        ON Perdoc.dbo.TEpdOperatorRawLog (TimelineId);
END;
