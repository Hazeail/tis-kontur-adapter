/*
  ФАЙЛ: KonturAccessResolver.cs
  НАЗНАЧЕНИЕ: Ролевое разрешение настроек доступа к Контур API по титулу ЭТрН.
  Инкапсулирует выбор AccessToken/BoxId и убирает ручные переключения из UI ТИС.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  12.05.2026 - Первичное создание резолвера доступа по титулу T1/T2/T3/T4.
  13.05.2026 - Добавлен приоритетный резолвинг доступа через ролевой реестр TEpdOperatorRoleAccess.
  14.05.2026 - Переведено разрешение авторизации на OIDC access token с fallback для плавной миграции.
  18.05.2026 - Добавлен fail-fast контроль OIDC refresh с явной диагностикой в AccessContext.
  26.05.2026 - Добавлено разрешение X-Solution-Info и ограничение refresh только для OIDC-токенов.
*/

using Tis.KonturIntegration.Models;
using Tis.KonturIntegration.Storage;

namespace Tis.KonturIntegration.Integration
{
    /// <summary>
    /// Резолвер ролевого доступа к Контур API для этапов ЭТрН.
    /// </summary>
    public class KonturAccessResolver
    {
        /// <summary>
        /// Инициализирует резолвер репозиторием операторных настроек.
        /// </summary>
        /// <param name="settingsRepository">Репозиторий чтения настроек оператора Контур.</param>
        /// <remarks>Резолвер не хранит состояние сессии и выполняет только вычисление конфигурации отправки.</remarks>
        public KonturAccessResolver(KonturSettingsRepository settingsRepository)
            : this(settingsRepository, null)
        {
        }

        /// <summary>
        /// Инициализирует резолвер репозиториями настроек и ролевого доступа.
        /// </summary>
        /// <param name="settingsRepository">Репозиторий чтения настроек оператора Контур.</param>
        /// <param name="roleAccessRepository">Репозиторий ролевого реестра доступа.</param>
        /// <remarks>При наличии записи в ролевом реестре она имеет приоритет для выбора boxId и role-specific токена.</remarks>
        public KonturAccessResolver(KonturSettingsRepository settingsRepository, KonturRoleAccessRepository roleAccessRepository)
        {
            SettingsRepository = settingsRepository;
            RoleAccessRepository = roleAccessRepository;
            OidcTokenService = new KonturOidcTokenService(settingsRepository);
        }

        /// <summary>
        /// Получает репозиторий операторных настроек.
        /// </summary>
        public KonturSettingsRepository SettingsRepository { get; private set; }

        /// <summary>
        /// Получает репозиторий ролевого реестра доступа.
        /// </summary>
        public KonturRoleAccessRepository RoleAccessRepository { get; private set; }

        /// <summary>
        /// Получает сервис поддержки жизненного цикла OIDC токена.
        /// </summary>
        public KonturOidcTokenService OidcTokenService { get; private set; }

        /// <summary>
        /// Получает код титула текущего прохода резолвера.
        /// </summary>
        private string CurrentTitleCode { get; set; }

        /// <summary>
        /// Получает роль отправителя текущего прохода резолвера.
        /// </summary>
        private string CurrentSenderRole { get; set; }

        /// <summary>
        /// Разрешает контекст доступа для указанного титула ЭТрН.
        /// </summary>
        /// <param name="titleCode">Код титула (T1/T2/T3/T4).</param>
        /// <returns>Контекст доступа с ключом, ящиком и признаком готовности.</returns>
        /// <remarks>
        /// Приоритет настроек: специализированные ключи титула, затем ролевые ключи, затем общий fallback.
        /// Это позволяет безопасно внедрять многоролевую схему без поломки существующей конфигурации.
        /// </remarks>
        public KonturAccessContext ResolveByTitle(string titleCode)
        {
            var normalizedTitle = (titleCode ?? string.Empty).Trim().ToUpperInvariant();
            var apiUrl = ReadFirstNotEmpty("LogisticsApiUrl");

            string senderRole;
            string accessToken;
            string senderBoxId;

            senderRole = ResolveSenderRole(normalizedTitle);
            if (senderRole == "Unknown")
            {
                return new KonturAccessContext
                {
                    IsReady = false,
                    TitleCode = normalizedTitle,
                    SenderRole = "Unknown",
                    ApiUrl = apiUrl,
                    Message = "UnsupportedTitleCode"
                };
            }

            CurrentTitleCode = normalizedTitle;
            CurrentSenderRole = senderRole;

            var roleAccess = ResolveRoleAccess(normalizedTitle, senderRole);
            accessToken = ResolveAccessToken(normalizedTitle, senderRole, roleAccess);
            string refreshError;
            accessToken = RefreshTokenIfNeeded(accessToken, out refreshError);
            senderBoxId = ResolveSenderBoxId(normalizedTitle, senderRole, roleAccess);
            var solutionInfo = ResolveSolutionInfo();

            if (!string.IsNullOrEmpty(refreshError))
            {
                return BuildNotReady(normalizedTitle, senderRole, apiUrl, accessToken, senderBoxId, solutionInfo, refreshError);
            }

            if (string.IsNullOrEmpty(apiUrl))
            {
                return BuildNotReady(normalizedTitle, senderRole, apiUrl, accessToken, senderBoxId, solutionInfo, "MissingLogisticsApiUrl");
            }

            if (string.IsNullOrEmpty(accessToken))
            {
                return BuildNotReady(normalizedTitle, senderRole, apiUrl, accessToken, senderBoxId, solutionInfo, "MissingAccessTokenForRole");
            }

            if (string.IsNullOrEmpty(senderBoxId))
            {
                return BuildNotReady(normalizedTitle, senderRole, apiUrl, accessToken, senderBoxId, solutionInfo, "MissingSenderBoxIdForRole");
            }

            return new KonturAccessContext
            {
                IsReady = true,
                TitleCode = normalizedTitle,
                SenderRole = senderRole,
                ApiUrl = apiUrl,
                AccessToken = accessToken,
                SenderBoxId = senderBoxId,
                SolutionInfo = solutionInfo,
                Message = roleAccess == null ? "AccessResolvedFromSettings" : "AccessResolvedFromRoleRegistry"
            };
        }

        /// <summary>
        /// Возвращает бизнес-роль отправителя по коду титула.
        /// </summary>
        /// <param name="titleCode">Код титула T1/T2/T3/T4.</param>
        /// <returns>Роль отправителя или Unknown.</returns>
        /// <remarks>Роль используется для поиска записи в ролевом реестре и fallback-настроек.</remarks>
        private string ResolveSenderRole(string titleCode)
        {
            if (titleCode == "T1")
            {
                return "Consignor";
            }

            if (titleCode == "T2")
            {
                return "Carrier";
            }

            if (titleCode == "T3")
            {
                return "Consignee";
            }

            if (titleCode == "T4")
            {
                return "Carrier";
            }

            return "Unknown";
        }

        /// <summary>
        /// Читает запись ролевого реестра для этапа.
        /// </summary>
        /// <param name="titleCode">Код титула.</param>
        /// <param name="senderRole">Роль отправителя.</param>
        /// <returns>Запись ролевого реестра или null.</returns>
        /// <remarks>Отсутствие записи не считается ошибкой и обрабатывается fallback-логикой.</remarks>
        private KonturRoleAccessRecord ResolveRoleAccess(string titleCode, string senderRole)
        {
            if (RoleAccessRepository == null)
            {
                return null;
            }

            return RoleAccessRepository.FindActive("Kontur", titleCode, senderRole);
        }

        /// <summary>
        /// Разрешает OIDC access token с учетом ролевого реестра и fallback-настроек.
        /// </summary>
        /// <param name="titleCode">Код титула.</param>
        /// <param name="senderRole">Роль отправителя.</param>
        /// <param name="roleAccess">Найденная запись ролевого реестра.</param>
        /// <returns>Итоговый access token.</returns>
        /// <remarks>Если в ролевом реестре токен не задан, используется иерархия ключей настроек OIDC с fallback.</remarks>
        private string ResolveAccessToken(string titleCode, string senderRole, KonturRoleAccessRecord roleAccess)
        {
            if (roleAccess != null && !string.IsNullOrEmpty(roleAccess.ApiKey))
            {
                return roleAccess.ApiKey.Trim();
            }

            if (titleCode == "T1")
            {
                return NormalizeResolvedCredential(ReadFirstNotEmpty("OidcAccessToken_T1", "OidcAccessToken_Consignor", "OidcAccessToken", "ApiKey_T1", "ApiKey_Consignor", "ApiKey"));
            }

            if (titleCode == "T2")
            {
                return NormalizeResolvedCredential(ReadFirstNotEmpty("OidcAccessToken_T2", "OidcAccessToken_Carrier", "OidcAccessToken", "ApiKey_T2", "ApiKey_Carrier", "ApiKey"));
            }

            if (titleCode == "T3")
            {
                return NormalizeResolvedCredential(ReadFirstNotEmpty("OidcAccessToken_T3", "OidcAccessToken_Consignee", "OidcAccessToken", "ApiKey_T3", "ApiKey_Consignee", "ApiKey"));
            }

            if (titleCode == "T4")
            {
                return NormalizeResolvedCredential(ReadFirstNotEmpty("OidcAccessToken_T4", "OidcAccessToken_Carrier", "OidcAccessToken", "ApiKey_T4", "ApiKey_Carrier", "ApiKey"));
            }

            return string.Empty;
        }

        /// <summary>
        /// Нормализует выбранные учетные данные доступа перед передачей в HTTP-клиент.
        /// </summary>
        /// <param name="credential">Сырое значение из настроек доступа.</param>
        /// <returns>Нормализованное значение: OIDC-токен как `bearer:...`, apiKey — без изменений.</returns>
        /// <remarks>
        /// Метод устраняет неоднозначность для opaque OIDC-токенов без JWT-структуры,
        /// которые иначе могли трактоваться как apiKey в клиенте Контур.
        /// </remarks>
        private string NormalizeResolvedCredential(string credential)
        {
            var normalized = (credential ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(normalized))
            {
                return string.Empty;
            }

            if (normalized.StartsWith("apiKey:", System.StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            if (normalized.StartsWith("bearer:", System.StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            if (normalized.Split('.').Length == 3)
            {
                return "bearer:" + normalized;
            }

            // Если credential пришел из OIDC-ветки, а токен opaque (не JWT),
            // все равно принудительно маркируем его как bearer.
            if (normalized.Length >= 32)
            {
                return "bearer:" + normalized;
            }

            return normalized;
        }

        /// <summary>
        /// Разрешает boxId отправителя с учетом ролевого реестра и fallback-настроек.
        /// </summary>
        /// <param name="titleCode">Код титула.</param>
        /// <param name="senderRole">Роль отправителя.</param>
        /// <param name="roleAccess">Найденная запись ролевого реестра.</param>
        /// <returns>Итоговый DiadocBoxId отправителя.</returns>
        /// <remarks>Ролевой реестр является приоритетным источником boxId для стабильной маршрутизации по ролям.</remarks>
        private string ResolveSenderBoxId(string titleCode, string senderRole, KonturRoleAccessRecord roleAccess)
        {
            if (roleAccess != null && !string.IsNullOrEmpty(roleAccess.DiadocBoxId))
            {
                return roleAccess.DiadocBoxId.Trim();
            }

            if (titleCode == "T1")
            {
                return ReadFirstNotEmpty("SenderBoxId_T1", "DanaflexSenderBoxId_T1", "DanaflexSenderBoxId_Consignor", "DanaflexSenderBoxId");
            }

            if (titleCode == "T2")
            {
                return ReadFirstNotEmpty("SenderBoxId_T2", "DanaflexSenderBoxId_T2", "DanaflexSenderBoxId_Carrier", "DanaflexSenderBoxId");
            }

            if (titleCode == "T3")
            {
                return ReadFirstNotEmpty("SenderBoxId_T3", "DanaflexSenderBoxId_T3", "DanaflexSenderBoxId_Consignee", "DanaflexSenderBoxId");
            }

            if (titleCode == "T4")
            {
                return ReadFirstNotEmpty("SenderBoxId_T4", "DanaflexSenderBoxId_T4", "DanaflexSenderBoxId_Carrier", "DanaflexSenderBoxId");
            }

            return string.Empty;
        }

        /// <summary>
        /// Читает первое непустое значение настройки из заданного списка ключей.
        /// </summary>
        /// <param name="keys">Упорядоченный список ключей настроек.</param>
        /// <returns>Первое найденное непустое значение или пустая строка.</returns>
        /// <remarks>Метод нужен для мягкой миграции схемы настроек без единовременного обновления всех окружений.</remarks>
        private string ReadFirstNotEmpty(params string[] keys)
        {
            for (var i = 0; i < keys.Length; i++)
            {
                var value = SettingsRepository.GetSettingValue("Kontur", keys[i]);
                if (IsUsableSettingValue(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Проверяет, пригодно ли значение настройки доступа для отправки в Контур.
        /// </summary>
        /// <param name="value">Сырое значение настройки из БД.</param>
        /// <returns>True, если значение можно использовать в runtime; иначе false.</returns>
        /// <remarks>
        /// Метод отбрасывает seed-заглушки вида REPLACE_WITH_*,
        /// чтобы резолвер не выбирал нерабочий ключ при наличии корректного fallback.
        /// </remarks>
        private bool IsUsableSettingValue(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            var normalized = value.Trim();
            if (normalized.Length == 0)
            {
                return false;
            }

            if (normalized.IndexOf("REPLACE_WITH_", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Обновляет access token при необходимости через refresh_token.
        /// </summary>
        /// <param name="accessToken">Текущий токен из настроек.</param>
        /// <returns>Актуальный токен после проверки срока действия.</returns>
        /// <remarks>При ошибке refresh возвращается исходный токен для сохранения обратной совместимости.</remarks>
        private string RefreshTokenIfNeeded(string accessToken, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (OidcTokenService == null)
            {
                return accessToken;
            }

            // API key и непрозрачные legacy-значения не должны попадать в OIDC refresh,
            // иначе рабочая не-OIDC авторизация начнет падать на поиске refresh-настроек.
            if (!RequiresOidcRefresh(accessToken))
            {
                return accessToken;
            }

            try
            {
                return OidcTokenService.EnsureFreshAccessTokenStrict(accessToken, CurrentTitleCode, CurrentSenderRole);
            }
            catch (System.Exception exception)
            {
                errorMessage = "OidcRefreshFailed: " + exception.Message;
                return string.Empty;
            }
        }

        /// <summary>
        /// Определяет, относится ли текущее значение доступа к OIDC-сценарию.
        /// </summary>
        /// <param name="accessToken">Текущее значение access token из настроек.</param>
        /// <returns>True, если токен нужно проверять через OIDC refresh; иначе false.</returns>
        /// <remarks>Шаг защищает API key-ветку от ошибочного перехода в OIDC refresh-поток.</remarks>
        private bool RequiresOidcRefresh(string accessToken)
        {
            if (string.IsNullOrEmpty(accessToken))
            {
                return false;
            }

            var normalized = accessToken.Trim();
            if (normalized.StartsWith("apiKey:", System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (normalized.StartsWith("bearer:", System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return normalized.Split('.').Length == 3;
        }

        /// <summary>
        /// Разрешает значение X-Solution-Info из настроек оператора.
        /// </summary>
        /// <returns>Строка для заголовка X-Solution-Info.</returns>
        /// <remarks>
        /// Сначала читаются явные ключи настройки, затем выполняется best-effort fallback по ИНН.
        /// Это позволяет включить заголовок без одномоментной миграции всех окружений.
        /// </remarks>
        private string ResolveSolutionInfo()
        {
            var configuredValue = ReadFirstNotEmpty("XSolutionInfo", "X-Solution-Info", "SolutionInfo");
            if (!string.IsNullOrEmpty(configuredValue))
            {
                return configuredValue;
            }

            var inn = ReadFirstNotEmpty("SolutionInfoInn", "OperatorInn", "PartnerInn", "Inn");
            if (!string.IsNullOrEmpty(inn))
            {
                return "TIS_" + inn;
            }

            return "TIS_unknown";
        }

        /// <summary>
        /// Формирует неготовый контекст доступа с диагностикой.
        /// </summary>
        /// <param name="titleCode">Код титула.</param>
        /// <param name="senderRole">Роль отправителя.</param>
        /// <param name="apiUrl">Текущий API URL.</param>
        /// <param name="accessToken">Текущий access token.</param>
        /// <param name="senderBoxId">Текущий boxId отправителя.</param>
        /// <param name="message">Причина неготовности.</param>
        /// <returns>Контекст с признаком IsReady=false.</returns>
        /// <remarks>Отдельный метод упрощает единообразную диагностику в UI и журналах.</remarks>
        private KonturAccessContext BuildNotReady(string titleCode, string senderRole, string apiUrl, string accessToken, string senderBoxId, string solutionInfo, string message)
        {
            return new KonturAccessContext
            {
                IsReady = false,
                TitleCode = titleCode,
                SenderRole = senderRole,
                ApiUrl = apiUrl,
                AccessToken = accessToken,
                SenderBoxId = senderBoxId,
                SolutionInfo = solutionInfo,
                Message = message
            };
        }
    }
}
