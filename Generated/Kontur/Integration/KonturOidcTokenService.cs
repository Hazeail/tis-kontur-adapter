/*
  ФАЙЛ: KonturOidcTokenService.cs
  НАЗНАЧЕНИЕ: Сервис контроля жизненного цикла OIDC access token для вызовов Kontur Logistics API.
  Выполняет проверку срока действия и refresh токена через identity.kontur.ru.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  14.05.2026 - Первичное создание сервиса автообновления OIDC токена.
  18.05.2026 - Добавлен fail-fast режим обновления токена с диагностикой причины.
  26.05.2026 - Добавлена ролевая схема refresh-ключей и сохранение токенов в тот же контекст настроек.
*/

using System;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json.Linq;
using Tis.KonturIntegration.Storage;

namespace Tis.KonturIntegration.Integration
{
    /// <summary>
    /// Сервис автоматического обновления OIDC токена Контур перед вызовами Logistics API.
    /// </summary>
    public class KonturOidcTokenService
    {
        /// <summary>
        /// Инициализирует сервис репозиторием операторных настроек.
        /// </summary>
        /// <param name="settingsRepository">Репозиторий доступа к TEpdOperatorSettings.</param>
        /// <remarks>Сервис использует настройки оператора Kontur для refresh-потока.</remarks>
        public KonturOidcTokenService(KonturSettingsRepository settingsRepository)
        {
            SettingsRepository = settingsRepository;
        }

        /// <summary>
        /// Получает репозиторий операторных настроек.
        /// </summary>
        public KonturSettingsRepository SettingsRepository { get; private set; }

        /// <summary>
        /// Возвращает актуальный access token: исходный или обновленный через refresh_token.
        /// </summary>
        /// <param name="currentAccessToken">Текущий access token из резолвера.</param>
        /// <returns>Актуальный access token для вызова API.</returns>
        /// <remarks>
        /// Если refresh недоступен или завершается ошибкой, метод возвращает исходный токен.
        /// Такой подход не ломает рабочий поток и позволяет диагностировать проблему по 401 ответу API.
        /// </remarks>
        public string EnsureFreshAccessToken(string currentAccessToken, string titleCode, string senderRole)
        {
            if (string.IsNullOrEmpty(currentAccessToken))
            {
                return currentAccessToken;
            }

            if (!IsRefreshRequired(titleCode, senderRole))
            {
                return currentAccessToken;
            }

            try
            {
                return RefreshToken(titleCode, senderRole);
            }
            catch
            {
                return currentAccessToken;
            }
        }

        /// <summary>
        /// Возвращает актуальный access token и завершает сценарий ошибкой, если обязательный refresh не удался.
        /// </summary>
        /// <param name="currentAccessToken">Текущий access token из резолвера.</param>
        /// <returns>Актуальный access token для вызова API.</returns>
        /// <remarks>
        /// Метод используется в боевом контуре, где недопустим скрытый fallback на просроченный токен.
        /// </remarks>
        public string EnsureFreshAccessTokenStrict(string currentAccessToken, string titleCode, string senderRole)
        {
            if (string.IsNullOrEmpty(currentAccessToken))
            {
                throw new ApplicationException("OidcAccessTokenMissing");
            }

            if (!IsRefreshRequired(titleCode, senderRole))
            {
                return currentAccessToken;
            }

            return RefreshToken(titleCode, senderRole);
        }

        /// <summary>
        /// Проверяет необходимость обновления токена по времени истечения.
        /// </summary>
        /// <returns>True, если токен истекает или время не заполнено; иначе false.</returns>
        /// <remarks>Порог обновления установлен в 2 минуты для снижения риска гонки на границе срока.</remarks>
        private bool IsRefreshRequired(string titleCode, string senderRole)
        {
            var expiresAtRaw = ReadFirstNotEmpty(BuildKeyCandidates("OidcTokenExpiresAtUtc", titleCode, senderRole));
            DateTime expiresAtUtc;
            if (!DateTime.TryParse(expiresAtRaw, out expiresAtUtc))
            {
                return true;
            }

            return expiresAtUtc <= DateTime.UtcNow.AddMinutes(2);
        }

        /// <summary>
        /// Выполняет запрос refresh_token и сохраняет новый токен в настройках.
        /// </summary>
        /// <returns>Новый access token или пустая строка при неуспехе.</returns>
        /// <remarks>
        /// Обновляются ключи: OidcAccessToken, OidcRefreshToken (если пришел), OidcTokenExpiresAtUtc.
        /// Endpoint можно переопределить настройкой OidcTokenEndpoint.
        /// </remarks>
        private string RefreshToken(string titleCode, string senderRole)
        {
            var refreshTokenKeys = BuildKeyCandidates("OidcRefreshToken", titleCode, senderRole);
            var clientIdKeys = BuildKeyCandidates("OidcClientId", titleCode, senderRole);
            var clientSecretKeys = BuildKeyCandidates("OidcClientSecret", titleCode, senderRole);
            var tokenEndpointKeys = BuildKeyCandidates("OidcTokenEndpoint", titleCode, senderRole);
            var accessTokenKeys = BuildKeyCandidates("OidcAccessToken", titleCode, senderRole);
            var expiresAtKeys = BuildKeyCandidates("OidcTokenExpiresAtUtc", titleCode, senderRole);

            var refreshToken = ReadFirstNotEmpty(refreshTokenKeys);
            var clientId = ReadFirstNotEmpty(clientIdKeys);
            var clientSecret = ReadFirstNotEmpty(clientSecretKeys);
            var tokenEndpoint = ReadFirstNotEmpty(tokenEndpointKeys);
            if (string.IsNullOrEmpty(tokenEndpoint))
            {
                tokenEndpoint = "https://identity.kontur.ru/connect/token";
            }

            if (string.IsNullOrEmpty(refreshToken) || string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                throw new ApplicationException("OidcRefreshSettingsMissing");
            }

            var body = "grant_type=refresh_token"
                + "&client_id=" + Uri.EscapeDataString(clientId)
                + "&client_secret=" + Uri.EscapeDataString(clientSecret)
                + "&refresh_token=" + Uri.EscapeDataString(refreshToken);

            var request = (HttpWebRequest)WebRequest.Create(tokenEndpoint);
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.Accept = "application/json";
            request.Timeout = 30000;
            request.ReadWriteTimeout = 30000;

            var payload = Encoding.UTF8.GetBytes(body);
            request.ContentLength = payload.Length;
            using (var stream = request.GetRequestStream())
            {
                stream.Write(payload, 0, payload.Length);
            }

            string responseText;
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                responseText = reader.ReadToEnd();
            }

            var tokenJson = JObject.Parse(responseText);
            var newAccessToken = Convert.ToString(tokenJson["access_token"]);
            if (string.IsNullOrEmpty(newAccessToken))
            {
                throw new ApplicationException("OidcRefreshResponseMissingAccessToken");
            }
            newAccessToken = NormalizeOidcAccessToken(newAccessToken);

            var newRefreshToken = Convert.ToString(tokenJson["refresh_token"]);
            var expiresInRaw = Convert.ToString(tokenJson["expires_in"]);
            int expiresIn;
            if (!int.TryParse(expiresInRaw, out expiresIn) || expiresIn <= 0)
            {
                expiresIn = 3600;
            }

            var expiresAtUtc = DateTime.UtcNow.AddSeconds(expiresIn);

            // Сохраняем обновленные токены в тот же набор ключей,
            // чтобы разные этапы и роли не перетирали друг друга общим значением.
            SettingsRepository.UpsertSettingValue("Kontur", ResolveWriteKey(accessTokenKeys), newAccessToken);
            if (!string.IsNullOrEmpty(newRefreshToken))
            {
                SettingsRepository.UpsertSettingValue("Kontur", ResolveWriteKey(refreshTokenKeys), newRefreshToken);
            }

            SettingsRepository.UpsertSettingValue("Kontur", ResolveWriteKey(expiresAtKeys), expiresAtUtc.ToString("o"));
            return newAccessToken;
        }

        /// <summary>
        /// Нормализует access token OIDC к формату `bearer:...`.
        /// </summary>
        /// <param name="accessToken">Сырое значение access_token из OIDC-ответа.</param>
        /// <returns>Значение в формате, который однозначно распознается клиентом Контур API.</returns>
        /// <remarks>
        /// Шаг исключает ошибочный переход в ApiKey-схему, если провайдер вернул opaque token без JWT-структуры.
        /// </remarks>
        private string NormalizeOidcAccessToken(string accessToken)
        {
            var normalized = (accessToken ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(normalized))
            {
                return string.Empty;
            }

            if (normalized.StartsWith("bearer:", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            return "bearer:" + normalized;
        }

        /// <summary>
        /// Строит список ключей настроек для этапа и роли с переходом к общему fallback.
        /// </summary>
        /// <param name="baseKey">Базовое имя ключа, например OidcAccessToken.</param>
        /// <param name="titleCode">Код титула T1/T2/T3/T4.</param>
        /// <param name="senderRole">Роль отправителя этапа.</param>
        /// <returns>Упорядоченный список кандидатных ключей.</returns>
        /// <remarks>Порядок нужен для согласованного чтения и записи настроек в одном и том же ролевом контексте.</remarks>
        private string[] BuildKeyCandidates(string baseKey, string titleCode, string senderRole)
        {
            var normalizedTitle = (titleCode ?? string.Empty).Trim().ToUpperInvariant();

            if (normalizedTitle == "T1")
            {
                return new[] { baseKey + "_T1", baseKey + "_Consignor", baseKey };
            }

            if (normalizedTitle == "T2")
            {
                return new[] { baseKey + "_T2", baseKey + "_Carrier", baseKey };
            }

            if (normalizedTitle == "T3")
            {
                return new[] { baseKey + "_T3", baseKey + "_Consignee", baseKey };
            }

            if (normalizedTitle == "T4")
            {
                return new[] { baseKey + "_T4", baseKey + "_Carrier", baseKey };
            }

            var normalizedRole = (senderRole ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(normalizedRole))
            {
                return new[] { baseKey + "_" + normalizedRole, baseKey };
            }

            return new[] { baseKey };
        }

        /// <summary>
        /// Читает первое непустое значение настройки из списка ключей.
        /// </summary>
        /// <param name="keys">Упорядоченный список ключей настроек.</param>
        /// <returns>Первое найденное непустое значение или пустая строка.</returns>
        /// <remarks>Метод нужен для общей логики fallback без дублирования SQL-вызовов по всему сервису.</remarks>
        private string ReadFirstNotEmpty(string[] keys)
        {
            if (keys == null)
            {
                return string.Empty;
            }

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
        /// Проверяет, пригодно ли значение настройки для реального OIDC-сценария.
        /// </summary>
        /// <param name="value">Сырое значение настройки из БД.</param>
        /// <returns>True, если значение можно использовать; иначе false.</returns>
        /// <remarks>
        /// Фильтр исключает шаблонные заглушки из seed-скриптов вида REPLACE_WITH_*,
        /// чтобы refresh не падал на тестовых/пустых role-based ключах и корректно переходил к fallback.
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

            if (normalized.IndexOf("REPLACE_WITH_", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Выбирает ключ настройки, в который нужно сохранить обновленное значение.
        /// </summary>
        /// <param name="keys">Упорядоченный список кандидатных ключей.</param>
        /// <returns>Ключ для записи обновленного значения.</returns>
        /// <remarks>
        /// Если значение уже было задано в одном из ключей, запись идет туда же.
        /// Иначе используется самый приоритетный ключ списка.
        /// </remarks>
        private string ResolveWriteKey(string[] keys)
        {
            if (keys == null || keys.Length == 0)
            {
                return string.Empty;
            }

            for (var i = 0; i < keys.Length; i++)
            {
                var value = SettingsRepository.GetSettingValue("Kontur", keys[i]);
                if (IsUsableSettingValue(value))
                {
                    return keys[i];
                }
            }

            return keys[0];
        }
    }
}
