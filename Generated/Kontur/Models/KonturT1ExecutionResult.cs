/*
  ФАЙЛ: KonturT1ExecutionResult.cs
  НАЗНАЧЕНИЕ: Модель итогового результата выполнения сценария T1 через KonturAdapter.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  05.05.2026 - Первичное создание модели результата выполнения T1.
*/

namespace Tis.KonturIntegration.Models
{
    /// <summary>
    /// Итог выполнения T1-сценария Danaflex в интеграционной ветке Контур.
    /// </summary>
    public class KonturT1ExecutionResult
    {
        /// <summary>
        /// Получает или задает признак успешного завершения сквозного сценария.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Получает или задает внутренний идентификатор timeline ТИС.
        /// </summary>
        public long TimelineId { get; set; }

        /// <summary>
        /// Получает или задает идентификатор перевозки, полученный от Контура.
        /// </summary>
        public string TransportationId { get; set; }

        /// <summary>
        /// Получает или задает идентификатор титула T1, полученный от Контура.
        /// </summary>
        public string TitleId { get; set; }

        /// <summary>
        /// Получает или задает нормализованный текст результата для журналирования.
        /// </summary>
        public string Message { get; set; }
    }
}
