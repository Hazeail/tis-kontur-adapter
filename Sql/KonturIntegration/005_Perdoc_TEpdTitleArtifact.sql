/*
  SQL: 005_Perdoc_TEpdTitleArtifact.sql
  НАЗНАЧЕНИЕ: Создает хранилище внутренних артефактов титулов ЭТрН для интеграции Контур.
  Таблица хранит XML, открепленную подпись и признаки подписанта по timelineId и коду титула.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  13.05.2026 - Первичное создание таблицы артефактов титулов ЭТрН.
*/

IF OBJECT_ID(N'Perdoc.dbo.TEpdTitleArtifact', N'U') IS NULL
BEGIN
    CREATE TABLE Perdoc.dbo.TEpdTitleArtifact
    (
        Id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_TEpdTitleArtifact PRIMARY KEY,
        TimelineId bigint NOT NULL,
        TitleCode nvarchar(10) NOT NULL,
        VersionNo int NOT NULL,
        XmlFileName nvarchar(260) NULL,
        TitleXml varbinary(max) NULL,
        SignatureFileName nvarchar(260) NULL,
        TitleSgn varbinary(max) NULL,
        Thumbprint nvarchar(100) NULL,
        SignerRole nvarchar(50) NULL,
        SignedAt datetime NULL,
        CreatedAt datetime NOT NULL CONSTRAINT DF_TEpdTitleArtifact_CreatedAt DEFAULT (GETDATE())
    );
END
GO

IF NOT EXISTS
(
    SELECT 1
      FROM Perdoc.sys.indexes
     WHERE name = N'IX_TEpdTitleArtifact_Timeline_Title_Version'
       AND object_id = OBJECT_ID(N'Perdoc.dbo.TEpdTitleArtifact')
)
BEGIN
    CREATE INDEX IX_TEpdTitleArtifact_Timeline_Title_Version
        ON Perdoc.dbo.TEpdTitleArtifact (TimelineId, TitleCode, VersionNo DESC, Id DESC);
END
GO
