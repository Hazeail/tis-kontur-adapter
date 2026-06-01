/*
  SQL: 009_Perdoc_TEpdStageSignerSelection.sql
  НАЗНАЧЕНИЕ: Хранилище выбранного подписанта по этапу ЭТрН Контур.
  Нужна для восстановления выбора подписанта в KonturProbe по связке TimelineId и StageCode.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  22.05.2026 - Первичное создание таблицы выбора подписанта этапа.
*/

IF OBJECT_ID(N'Perdoc.dbo.TEpdStageSignerSelection', N'U') IS NULL
BEGIN
    CREATE TABLE Perdoc.dbo.TEpdStageSignerSelection
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TEpdStageSignerSelection PRIMARY KEY,
        TimelineId BIGINT NOT NULL,
        StageCode NVARCHAR(16) NOT NULL,
        SignerFizLicoId BIGINT NOT NULL,
        UpdatedByUserId BIGINT NOT NULL CONSTRAINT DF_TEpdStageSignerSelection_UpdatedByUserId DEFAULT (0),
        UpdatedAt DATETIME NOT NULL CONSTRAINT DF_TEpdStageSignerSelection_UpdatedAt DEFAULT (GETDATE())
    );
END

IF NOT EXISTS
(
    SELECT 1
      FROM sys.indexes
     WHERE name = N'UX_TEpdStageSignerSelection_Timeline_Stage'
       AND object_id = OBJECT_ID(N'Perdoc.dbo.TEpdStageSignerSelection')
)
BEGIN
    CREATE UNIQUE INDEX UX_TEpdStageSignerSelection_Timeline_Stage
        ON Perdoc.dbo.TEpdStageSignerSelection (TimelineId, StageCode);
END
