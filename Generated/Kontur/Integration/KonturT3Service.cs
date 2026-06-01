/*
  ФАЙЛ: KonturT3Service.cs
  НАЗНАЧЕНИЕ: Служебный сценарий запуска T3 через KonturAdapter из контекста ТИС.
  Используется как отдельная точка вызова этапа T3 без смешивания с UI-логикой.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  13.05.2026 - Первичное создание файла.
  13.05.2026 - Добавлен запуск T3 по внутреннему артефакту XML и подписи.
  13.05.2026 - Подключен ролевой реестр доступа TEpdOperatorRoleAccess.
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
    /// Сервис запуска сценария T3 через Контур с подключением SQL-репозиториев.
    /// </summary>
    public class KonturT3Service
    {
        /// <summary>
        /// Инициализирует сервис строкой подключения ТИС.
        /// </summary>
        /// <param name="connectionString">Строка подключения к SQL, где доступны таблицы Perdoc.</param>
        /// <remarks>Сервис не привязан к WebForms и может вызываться из разных точек интеграции.</remarks>
        public KonturT3Service(string connectionString)
        {
            ConnectionString = connectionString;
        }

        /// <summary>
        /// Получает строку подключения, используемую для SQL-репозиториев.
        /// </summary>
        public string ConnectionString { get; private set; }

        /// <summary>
        /// Выполняет шаг отправки T3 для указанного timeline с файлом подписи.
        /// </summary>
        /// <param name="timelineId">Идентификатор документа в timeline ТИС.</param>
        /// <param name="xmlPath">Путь к XML-файлу T3.</param>
        /// <param name="signaturePath">Путь к файлу открепленной подписи T3.</param>
        /// <returns>Итог выполнения сценария с ключевыми идентификаторами.</returns>
        /// <remarks>
        /// Подпись обязательна для отправки T3.
        /// Если параметр не задан, адаптер пытается взять подпись из EPD-хранилища по timelineId.
        /// </remarks>
        public KonturT3ExecutionResult Execute(long timelineId, string xmlPath, string signaturePath)
        {
            var settingsRepository = new KonturSettingsRepository(ConnectionString);
            var roleAccessRepository = new KonturRoleAccessRepository(ConnectionString);
            var access = new KonturAccessResolver(settingsRepository, roleAccessRepository).ResolveByTitle("T3");
            if (!access.IsReady)
            {
                return new KonturT3ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    Message = "AccessResolveFailed: " + access.Message
                };
            }

            var adapter = BuildAdapter(access.ApiUrl, access.AccessToken, access.SolutionInfo, settingsRepository);
            return adapter.StartDanaflexT3(timelineId, xmlPath, signaturePath, access.SenderBoxId);
        }

        /// <summary>
        /// Выполняет шаг отправки T3 по внутреннему артефакту XML и подписи.
        /// </summary>
        /// <param name="timelineId">Идентификатор документа в timeline ТИС.</param>
        /// <param name="artifact">Сохраненный XML и открепленная подпись титула T3.</param>
        /// <returns>Итог выполнения сценария с ключевыми идентификаторами.</returns>
        /// <remarks>Метод используется stage-runner, когда пользователь не передает пути к XML/SGN-файлам.</remarks>
        public KonturT3ExecutionResult ExecuteArtifact(long timelineId, KonturTitleArtifact artifact)
        {
            var settingsRepository = new KonturSettingsRepository(ConnectionString);
            var roleAccessRepository = new KonturRoleAccessRepository(ConnectionString);
            var access = new KonturAccessResolver(settingsRepository, roleAccessRepository).ResolveByTitle("T3");
            if (!access.IsReady)
            {
                return new KonturT3ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    Message = "AccessResolveFailed: " + access.Message
                };
            }

            var adapter = BuildAdapter(access.ApiUrl, access.AccessToken, access.SolutionInfo, settingsRepository);
            return adapter.StartDanaflexT3Artifact(timelineId, artifact, access.SenderBoxId);
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
