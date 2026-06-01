/*
  ФАЙЛ: KonturTestModeState.cs
  НАЗНАЧЕНИЕ: Модель состояния тестового режима Kontur-only по конкретному TimelineId.
  Нужна для безопасного отделения тестового сценария подписи Контур от штатного боевого контура ТИС.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  28.05.2026 - Первичное создание модели состояния Kontur-only режима.
*/

using System;

namespace Tis.KonturIntegration.Models
{
    /// <summary>
    /// Описывает состояние тестового режима Kontur-only по документу timeline.
    /// </summary>
    public class KonturTestModeState
    {
        /// <summary>
        /// Получает или задает идентификатор записи состояния.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Получает или задает идентификатор timeline документа.
        /// </summary>
        public long TimelineId { get; set; }

        /// <summary>
        /// Получает или задает признак включенного тестового режима.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Получает или задает идентификатор пользователя ТИС, изменившего режим.
        /// </summary>
        public long UpdatedByUserId { get; set; }

        /// <summary>
        /// Получает или задает дату последнего изменения режима.
        /// </summary>
        public DateTime UpdatedAt { get; set; }
    }
}
