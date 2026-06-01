/*
  ФАЙЛ: KonturStageSignerSelection.cs
  НАЗНАЧЕНИЕ: Модель сохраненного выбора подписанта по этапу ЭТрН Контур.
  Нужна для восстановления рабочего состояния KonturProbe по связке TimelineId и StageCode.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  22.05.2026 - Первичное создание модели сохраненного выбора подписанта.
*/

using System;

namespace Tis.KonturIntegration.Models
{
    /// <summary>
    /// Описывает сохраненный выбор подписанта этапа.
    /// </summary>
    public class KonturStageSignerSelection
    {
        /// <summary>
        /// Получает или задает идентификатор записи выбора.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Получает или задает идентификатор timeline.
        /// </summary>
        public long TimelineId { get; set; }

        /// <summary>
        /// Получает или задает код этапа T1/T2/T3/T4.
        /// </summary>
        public string StageCode { get; set; }

        /// <summary>
        /// Получает или задает идентификатор TFizLico выбранного подписанта.
        /// </summary>
        public long SignerFizLicoId { get; set; }

        /// <summary>
        /// Получает или задает идентификатор пользователя ТИС, который сохранил выбор.
        /// </summary>
        public long UpdatedByUserId { get; set; }

        /// <summary>
        /// Получает или задает дату последнего обновления выбора.
        /// </summary>
        public DateTime UpdatedAt { get; set; }
    }
}
