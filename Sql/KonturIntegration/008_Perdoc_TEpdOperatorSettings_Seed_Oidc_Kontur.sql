/*
  ФАЙЛ: 008_Perdoc_TEpdOperatorSettings_Seed_Oidc_Kontur.sql
  НАЗНАЧЕНИЕ: Инициализация OIDC-настроек Контур в Perdoc.dbo.TEpdOperatorSettings.
  Скрипт подготавливает ключи для Bearer-авторизации и автообновления токена.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  14.05.2026 - Первичное создание скрипта OIDC-настроек для контура Контур.
*/

SET NOCOUNT ON;

DECLARE @OperatorCode NVARCHAR(50) = N'Kontur';

/*
  Перед запуском заполнить значения:
  - @OidcAccessToken: текущий access token
  - @OidcRefreshToken: refresh token
  - @OidcClientId: client_id из OIDC приложения
  - @OidcClientSecret: client_secret из OIDC приложения
  - @OidcTokenExpiresAtUtc: UTC-время истечения access token в ISO-формате (например 2026-05-14T12:30:00Z)
*/
DECLARE @OidcAccessToken NVARCHAR(MAX) = N'__PASTE_ACCESS_TOKEN__';
DECLARE @OidcRefreshToken NVARCHAR(MAX) = N'__PASTE_REFRESH_TOKEN__';
DECLARE @OidcClientId NVARCHAR(400) = N'__PASTE_CLIENT_ID__';
DECLARE @OidcClientSecret NVARCHAR(400) = N'__PASTE_CLIENT_SECRET__';
DECLARE @OidcTokenEndpoint NVARCHAR(400) = N'https://identity.kontur.ru/connect/token';
DECLARE @OidcTokenExpiresAtUtc NVARCHAR(100) = N'__PASTE_EXPIRES_AT_UTC_ISO__';

DECLARE @Settings TABLE
(
    SettingKey NVARCHAR(200) NOT NULL,
    SettingValue NVARCHAR(MAX) NOT NULL
);

INSERT INTO @Settings (SettingKey, SettingValue)
VALUES
    (N'OidcAccessToken', @OidcAccessToken),
    (N'OidcRefreshToken', @OidcRefreshToken),
    (N'OidcClientId', @OidcClientId),
    (N'OidcClientSecret', @OidcClientSecret),
    (N'OidcTokenEndpoint', @OidcTokenEndpoint),
    (N'OidcTokenExpiresAtUtc', @OidcTokenExpiresAtUtc);

/*
  При необходимости можно задать отдельные токены по ролям/этапам.
  Текущий код использует эти ключи приоритетно, затем fallback на общий OidcAccessToken.
*/
--INSERT INTO @Settings (SettingKey, SettingValue) VALUES (N'OidcAccessToken_T1', @OidcAccessToken);
--INSERT INTO @Settings (SettingKey, SettingValue) VALUES (N'OidcAccessToken_T2', @OidcAccessToken);
--INSERT INTO @Settings (SettingKey, SettingValue) VALUES (N'OidcAccessToken_T3', @OidcAccessToken);
--INSERT INTO @Settings (SettingKey, SettingValue) VALUES (N'OidcAccessToken_T4', @OidcAccessToken);

MERGE Perdoc.dbo.TEpdOperatorSettings AS tgt
USING
(
    SELECT
        @OperatorCode AS OperatorCode,
        s.SettingKey,
        s.SettingValue
    FROM @Settings s
) AS src
ON tgt.OperatorCode = src.OperatorCode
   AND tgt.SettingKey = src.SettingKey
WHEN MATCHED THEN
    UPDATE
    SET tgt.SettingValue = src.SettingValue
WHEN NOT MATCHED THEN
    INSERT (OperatorCode, SettingKey, SettingValue)
    VALUES (src.OperatorCode, src.SettingKey, src.SettingValue);

SELECT
    OperatorCode,
    SettingKey,
    LEFT(SettingValue, 120) AS SettingValuePreview
FROM Perdoc.dbo.TEpdOperatorSettings
WHERE OperatorCode = @OperatorCode
  AND SettingKey IN
  (
      N'OidcAccessToken',
      N'OidcRefreshToken',
      N'OidcClientId',
      N'OidcClientSecret',
      N'OidcTokenEndpoint',
      N'OidcTokenExpiresAtUtc',
      N'OidcAccessToken_T1',
      N'OidcAccessToken_T2',
      N'OidcAccessToken_T3',
      N'OidcAccessToken_T4'
  )
ORDER BY SettingKey;
