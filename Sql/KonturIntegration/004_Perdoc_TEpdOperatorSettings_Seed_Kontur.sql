/*
  SQL: 004_Perdoc_TEpdOperatorSettings_Seed_Kontur.sql
  НАЗНАЧЕНИЕ: Базовая инициализация настроек Kontur для первой поставки Danaflex.
*/

IF NOT EXISTS (
    SELECT 1
    FROM Perdoc.dbo.TEpdOperatorSettings
    WHERE OperatorCode = N'Kontur' AND SettingKey = N'LogisticsApiUrl'
)
BEGIN
    INSERT INTO Perdoc.dbo.TEpdOperatorSettings
        (OperatorCode, SettingKey, SettingValue, IsSecret)
    VALUES
        (N'Kontur', N'LogisticsApiUrl', N'https://logist-api.kontur.ru', 0);
END;

IF NOT EXISTS (
    SELECT 1
    FROM Perdoc.dbo.TEpdOperatorSettings
    WHERE OperatorCode = N'Kontur' AND SettingKey = N'ApiKey'
)
BEGIN
    INSERT INTO Perdoc.dbo.TEpdOperatorSettings
        (OperatorCode, SettingKey, SettingValue, IsSecret)
    VALUES
        (N'Kontur', N'ApiKey', N'47b6691b-87ee-5f1a-3b5a-893f9ac949d0', 1);
END;

IF NOT EXISTS (
    SELECT 1
    FROM Perdoc.dbo.TEpdOperatorSettings
    WHERE OperatorCode = N'Kontur' AND SettingKey = N'DanaflexSenderBoxId'
)
BEGIN
    INSERT INTO Perdoc.dbo.TEpdOperatorSettings
        (OperatorCode, SettingKey, SettingValue, IsSecret)
    VALUES
        (N'Kontur', N'DanaflexSenderBoxId', N'3d63a359-42a5-4a19-97af-c63f139b9423', 0);
END;
