/*
  ФАЙЛ: KonturT2Service.cs
  НАЗНАЧЕНИЕ: Служебный сценарий запуска T2 через KonturAdapter из контекста ТИС.
  Используется как отдельная точка вызова этапа T2 без смешивания с UI-логикой.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  08.05.2026 - Первичное создание файла.
  12.05.2026 - Добавлен сценарий fallback подписи из EpdRepo при пустом пути к файлу подписи.
  12.05.2026 - Подключено ролевое разрешение доступа по титулу для выбора ApiKey и sender boxId.
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
    /// Сервис запуска сценария T2 через Контур с подключением SQL-репозиториев.
    /// </summary>
    public class KonturT2Service
    {
        /// <summary>
        /// Инициализирует сервис строкой подключения ТИС.
        /// </summary>
        /// <param name="connectionString">Строка подключения к SQL, где доступны таблицы Perdoc.</param>
        /// <remarks>Сервис не привязан к WebForms и может вызываться из разных точек интеграции.</remarks>
        public KonturT2Service(string connectionString)
        {
            ConnectionString = connectionString;
        }

        /// <summary>
        /// Получает строку подключения, используемую для SQL-репозиториев.
        /// </summary>
        public string ConnectionString { get; private set; }

        /// <summary>
        /// Выполняет шаг отправки T2 для указанного timeline.
        /// </summary>
        /// <param name="timelineId">Идентификатор документа в timeline ТИС.</param>
        /// <param name="xmlPath">Путь к XML-файлу T2.</param>
        /// <returns>Итог выполнения сценария с ключевыми идентификаторами.</returns>
        /// <remarks>
        /// Сервис использует refs предыдущих шагов и общий контур логирования адаптера,
        /// чтобы обеспечить трассируемость выполнения T2.
        /// </remarks>
        public KonturT2ExecutionResult Execute(long timelineId, string xmlPath)
        {
            return Execute(timelineId, xmlPath, string.Empty);
        }

        /// <summary>
        /// Выполняет шаг отправки T2 для указанного timeline с файлом подписи.
        /// </summary>
        /// <param name="timelineId">Идентификатор документа в timeline ТИС.</param>
        /// <param name="xmlPath">Путь к XML-файлу T2.</param>
        /// <param name="signaturePath">Путь к файлу открепленной подписи T2.</param>
        /// <returns>Итог выполнения сценария с ключевыми идентификаторами.</returns>
        /// <remarks>
        /// Подпись обязательна для отправки T2 в Контур.
        /// Если параметр не задан, адаптер пытается взять подпись из EPD-хранилища по timelineId.
        /// </remarks>
        public KonturT2ExecutionResult Execute(long timelineId, string xmlPath, string signaturePath)
        {
            var settingsRepository = new KonturSettingsRepository(ConnectionString);
            var roleAccessRepository = new KonturRoleAccessRepository(ConnectionString);
            var access = new KonturAccessResolver(settingsRepository, roleAccessRepository).ResolveByTitle("T2");
            if (!access.IsReady)
            {
                return new KonturT2ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    Message = "AccessResolveFailed: " + access.Message
                };
            }

            var adapter = BuildAdapter(access.ApiUrl, access.AccessToken, access.SolutionInfo, settingsRepository);
            return adapter.StartDanaflexT2(timelineId, xmlPath, signaturePath, access.SenderBoxId);
        }

        /// <summary>
        /// Собирает экземпляр адаптера Контур с подключенными репозиториями.
        /// </summary>
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
