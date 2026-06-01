/*
  SQL: 003_Perdoc_TEpdOperatorSettings.sql
  НАЗНАЧЕНИЕ: Таблица настроек операторов для хранения URL, ключей и параметров режима.
*/

IF OBJECT_ID(N'Perdoc.dbo.TEpdOperatorSettings', N'U') IS NULL
BEGIN
    CREATE TABLE Perdoc.dbo.TEpdOperatorSettings
    (
        Id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        OperatorCode NVARCHAR(32) NOT NULL,
        SettingKey NVARCHAR(64) NOT NULL,
        SettingValue NVARCHAR(512) NULL,
        IsSecret BIT NOT NULL CONSTRAINT DF_TEpdOperatorSettings_IsSecret DEFAULT (0),
        UpdatedAt DATETIME NOT NULL CONSTRAINT DF_TEpdOperatorSettings_UpdatedAt DEFAULT (GETDATE())
    );
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_TEpdOperatorSettings_OperatorCode_SettingKey'
      AND object_id = OBJECT_ID(N'Perdoc.dbo.TEpdOperatorSettings')
)
BEGIN
    CREATE UNIQUE INDEX UX_TEpdOperatorSettings_OperatorCode_SettingKey
        ON Perdoc.dbo.TEpdOperatorSettings (OperatorCode, SettingKey);
END;
