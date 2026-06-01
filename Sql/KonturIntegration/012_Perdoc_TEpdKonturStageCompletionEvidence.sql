/*
  SQL: 012_Perdoc_TEpdKonturStageCompletionEvidence.sql
  НАЗНАЧЕНИЕ: Хранение evidence-снимков проверки бизнес-завершения этапа Контур ЭТрН.
  Таблица отделяет диагностические внешние признаки от явного состояния этапа.
*/

IF OBJECT_ID(N'Perdoc.dbo.TEpdKonturStageCompletionEvidence', N'U') IS NULL
BEGIN
    CREATE TABLE Perdoc.dbo.TEpdKonturStageCompletionEvidence
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TEpdKonturStageCompletionEvidence PRIMARY KEY,
        TimelineId BIGINT NOT NULL,
        StageCode NVARCHAR(32) NOT NULL,
        TitleCode NVARCHAR(10) NULL,
        TransportationId NVARCHAR(100) NULL,
        TitleId NVARCHAR(100) NULL,
        ExternalDocumentStatus NVARCHAR(100) NULL,
        ExternalTitleStatus NVARCHAR(100) NULL,
        ExternalActionCode NVARCHAR(100) NULL,
        IsDraft BIT NOT NULL CONSTRAINT DF_TEpdKonturStageCompletionEvidence_IsDraft DEFAULT (0),
        HasActiveError BIT NOT NULL CONSTRAINT DF_TEpdKonturStageCompletionEvidence_HasActiveError DEFAULT (0),
        HttpStatus INT NULL,
        RawEvidenceSummary NVARCHAR(1000) NULL,
        CompletionSource NVARCHAR(50) NULL,
        CheckedAt DATETIME NOT NULL,
        CreatedAt DATETIME NOT NULL CONSTRAINT DF_TEpdKonturStageCompletionEvidence_CreatedAt DEFAULT (GETDATE())
    );
END
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_TEpdKonturStageCompletionEvidence_Timeline_Stage'
      AND object_id = OBJECT_ID(N'Perdoc.dbo.TEpdKonturStageCompletionEvidence')
)
BEGIN
    CREATE INDEX IX_TEpdKonturStageCompletionEvidence_Timeline_Stage
        ON Perdoc.dbo.TEpdKonturStageCompletionEvidence (TimelineId, StageCode, Id DESC);
END
GO