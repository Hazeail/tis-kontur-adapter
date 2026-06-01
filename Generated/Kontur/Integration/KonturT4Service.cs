/*
  ФАЙЛ: KonturT4Service.cs
  НАЗНАЧЕНИЕ: Служебный сценарий запуска T4 через KonturAdapter из контекста ТИС.
  Используется как отдельная точка вызова этапа T4 без смешивания с UI-логикой.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  13.05.2026 - Первичное создание файла.
  14.05.2026 - Переключена авторизация вызовов на OIDC access token.
  26.05.2026 - Передача X-Solution-Info переведена в общий клиент API.
*/

using KonturApiAdapter = Tis.KonturIntegration.KonturAdapter.KonturAdapter;
using KonturApiClient = Tis.KonturIntegration.KonturClient.KonturClient;
using Tis.KonturIntegration.Models;
using Tis.KonturIntegration.Storage;

namespace Tis.KonturIntegration.Integration
{
    /// <summary>
    /// Сервис запуска сценария T4 через Контур с подключением SQL-репозиториев.
    /// </summary>
    public class KonturT4Service
    {
        /// <summary>
        /// Инициализирует сервис строкой подключения ТИС.
        /// </summary>
        /// <param name="connectionString">Строка подключения к SQL, где доступны таблицы Perdoc.</param>
        /// <remarks>Сервис не привязан к WebForms и может вызываться из разных точек интеграции.</remarks>
        public KonturT4Service(string connectionString)
        {
            ConnectionString = connectionString;
        }

        /// <summary>
        /// Получает строку подключения, используемую для SQL-репозиториев.
        /// </summary>
        public string ConnectionString { get; private set; }

        /// <summary>
        /// Выполняет шаг отправки T4 для указанного timeline с файлом подписи.
        /// </summary>
        /// <param name="timelineId">Идентификатор документа в timeline ТИС.</param>
        /// <param name="xmlPath">Путь к XML-файлу T4.</param>
        /// <param name="signaturePath">Путь к файлу открепленной подписи T4.</param>
        /// <returns>Итог выполнения сценария с ключевыми идентификаторами.</returns>
        /// <remarks>
        /// Подпись обязательна для отправки T4.
        /// Если параметр не задан, адаптер пытается взять подпись из EPD-хранилища по timelineId.
        /// </remarks>
        public KonturT4ExecutionResult Execute(long timelineId, string xmlPath, string signaturePath)
        {
            var settingsRepository = new KonturSettingsRepository(ConnectionString);
            var roleAccessRepository = new KonturRoleAccessRepository(ConnectionString);
            var access = new KonturAccessResolver(settingsRepository, roleAccessRepository).ResolveByTitle("T4");
            if (!access.IsReady)
            {
                return new KonturT4ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    Message = "AccessResolveFailed: " + access.Message
                };
            }

            var adapter = BuildAdapter(access.ApiUrl, access.AccessToken, access.SolutionInfo, settingsRepository);
            return adapter.StartDanaflexT4(timelineId, xmlPath, signaturePath, access.SenderBoxId);
        }

        /// <summary>
        /// Собирает экземпляр адаптера Контур с подключенными репозиториями.
        /// </summary>
        /// <param name="apiUrl">Базовый URL Контур API.</param>
        /// <param name="accessToken">OIDC access token выбранной роли.</param>
        /// <param name="settingsRepository">Репозиторий операторных настроек.</param>
        /// <returns>Готовый к работе адаптер Контур.</returns>
        /// <remarks>Сборка изолирована в отдельном методе для единообразия с другими сервисами.</remarks>
        private KonturApiAdapter BuildAdapter(string apiUrl, string accessToken, string solutionInfo, KonturSettingsRepository settingsRepository)
        {
            var client = new KonturApiClient(apiUrl, accessToken, solutionInfo);
            var adapter = new KonturApiAdapter(client)
            {
                SettingsRepository = settingsRepository,
                OperatorRefRepository = new KonturOperatorRefRepository(ConnectionString),
                RawLogRepository = new KonturRawLogRepository(ConnectionString),
                TimelineRepository = new KonturTimelineRepository(ConnectionString)
            };
            return adapter;
        }
    }
}
