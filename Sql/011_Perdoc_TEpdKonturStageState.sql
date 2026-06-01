/*
  SQL: 011_Perdoc_TEpdKonturStageState.sql
  НАЗНАЧЕНИЕ: Хранение явного состояния этапа Контур ЭТрН по TimelineId и StageCode.
  Таблица отделяет жизненный цикл этапа от XML/SGN-артефактов, raw-log и внешних refs оператора.
*/

IF OBJECT_ID(N'Perdoc.dbo.TEpdKonturStageState', N'U') IS NULL
BEGIN
    CREATE TABLE Perdoc.dbo.TEpdKonturStageState
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TEpdKonturStageState PRIMARY KEY,
        TimelineId BIGINT NOT NULL,
        StageCode NVARCHAR(32) NOT NULL,
        TitleCode NVARCHAR(10) NOT NULL,
        XmlBuilt BIT NOT NULL CONSTRAINT DF_TEpdKonturStageState_XmlBuilt DEFAULT (0),
        SignatureImported BIT NOT NULL CONSTRAINT DF_TEpdKonturStageState_SignatureImported DEFAULT (0),
        Sent BIT NOT NULL CONSTRAINT DF_TEpdKonturStageState_Sent DEFAULT (0),
        Completed BIT NOT NULL CONSTRAINT DF_TEpdKonturStageState_Completed DEFAULT (0),
        NextStageAllowed BIT NOT NULL CONSTRAINT DF_TEpdKonturStageState_NextStageAllowed DEFAULT (0),
        TransportationId NVARCHAR(100) NULL,
        TitleId NVARCHAR(100) NULL,
        LastOperatorStatus NVARCHAR(100) NULL,
        LastErrorCode NVARCHAR(100) NULL,
        LastErrorMessage NVARCHAR(500) NULL,
        UpdatedAt DATETIME NOT NULL CONSTRAINT DF_TEpdKonturStageState_UpdatedAt DEFAULT (GETDATE())
    );
END
GO

IF COL_LENGTH('Perdoc.dbo.TEpdKonturStageState', 'TransportationId') IS NULL
BEGIN
    ALTER TABLE Perdoc.dbo.TEpdKonturStageState
    ADD TransportationId NVARCHAR(100) NULL;
END
GO

IF COL_LENGTH('Perdoc.dbo.TEpdKonturStageState', 'TitleId') IS NULL
BEGIN
    ALTER TABLE Perdoc.dbo.TEpdKonturStageState
    ADD TitleId NVARCHAR(100) NULL;
END
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_TEpdKonturStageState_Timeline_Stage'
      AND object_id = OBJECT_ID(N'Perdoc.dbo.TEpdKonturStageState')
)
BEGIN
    CREATE UNIQUE INDEX UX_TEpdKonturStageState_Timeline_Stage
        ON Perdoc.dbo.TEpdKonturStageState (TimelineId, StageCode);
END
GO
