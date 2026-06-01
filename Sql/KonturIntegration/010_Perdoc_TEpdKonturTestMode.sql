/*
  SQL: 010_Perdoc_TEpdKonturTestMode.sql
  НАЗНАЧЕНИЕ: Хранение флага тестового режима Kontur-only по TimelineId.
  Нужна для отделения тестового сценария подписи Контур от обычного рабочего контура ТИС.
*/

IF OBJECT_ID(N'Perdoc.dbo.TEpdKonturTestMode', N'U') IS NULL
BEGIN
    CREATE TABLE Perdoc.dbo.TEpdKonturTestMode
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TEpdKonturTestMode PRIMARY KEY,
        TimelineId BIGINT NOT NULL,
        IsEnabled BIT NOT NULL CONSTRAINT DF_TEpdKonturTestMode_IsEnabled DEFAULT (0),
        UpdatedByUserId BIGINT NOT NULL CONSTRAINT DF_TEpdKonturTestMode_UpdatedByUserId DEFAULT (0),
        UpdatedAt DATETIME NOT NULL CONSTRAINT DF_TEpdKonturTestMode_UpdatedAt DEFAULT (GETDATE())
    );
END
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_TEpdKonturTestMode_TimelineId'
      AND object_id = OBJECT_ID(N'Perdoc.dbo.TEpdKonturTestMode')
)
BEGIN
    CREATE UNIQUE INDEX UX_TEpdKonturTestMode_TimelineId
        ON Perdoc.dbo.TEpdKonturTestMode (TimelineId);
END
GO
