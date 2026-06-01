/*
  ФАЙЛ: KonturTestModeService.cs
  НАЗНАЧЕНИЕ: Сервис работы с тестовым режимом Kontur-only по TimelineId.
  Выступает единой точкой входа для страниц и интеграционных сервисов, которым нужно знать, активен ли специальный тестовый сценарий.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  28.05.2026 - Первичное создание сервиса состояния Kontur-only режима.
*/

using Tis.KonturIntegration.Models;
using Tis.KonturIntegration.Storage;

namespace Tis.KonturIntegration.Integration
{
    /// <summary>
    /// Сервис чтения и изменения состояния тестового режима Kontur-only.
    /// </summary>
    public class KonturTestModeService
    {
        /// <summary>
        /// Инициализирует сервис строкой подключения к ТИС и Perdoc.
        /// </summary>
        /// <param name="connectionString">Строка подключения к SQL Server.</param>
        public KonturTestModeService(string connectionString)
        {
            TestModeRepository = new KonturTestModeRepository(connectionString);
        }

        /// <summary>
        /// Получает репозиторий состояния тестового режима.
        /// </summary>
        public KonturTestModeRepository TestModeRepository { get; private set; }

        /// <summary>
        /// Возвращает состояние тестового режима по timeline.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <returns>Состояние режима или null, если оно не задано.</returns>
        public KonturTestModeState GetState(long timelineId)
        {
            return TestModeRepository.GetState(timelineId);
        }

        /// <summary>
        /// Проверяет, включен ли тестовый режим для timeline.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <returns>True, если режим включен; иначе false.</returns>
        public bool IsEnabled(long timelineId)
        {
            var state = GetState(timelineId);
            return state != null && state.IsEnabled;
        }

        /// <summary>
        /// Сохраняет новое состояние тестового режима для timeline.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="isEnabled">Новое значение признака режима.</param>
        /// <param name="updatedByUserId">Идентификатор пользователя ТИС, изменившего режим.</param>
        public void SaveState(long timelineId, bool isEnabled, long updatedByUserId)
        {
            TestModeRepository.SaveState(new KonturTestModeState
            {
                TimelineId = timelineId,
                IsEnabled = isEnabled,
                UpdatedByUserId = updatedByUserId
            });
        }

        /// <summary>
        /// Удаляет сохраненное состояние тестового режима для timeline.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        public void DeleteState(long timelineId)
        {
            TestModeRepository.DeleteState(timelineId);
        }
    }
}
