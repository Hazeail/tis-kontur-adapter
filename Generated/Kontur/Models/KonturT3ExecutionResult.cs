/*
  ФАЙЛ: KonturT3ExecutionResult.cs
  НАЗНАЧЕНИЕ: Модель итогового результата выполнения сценария T3 через KonturAdapter.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  13.05.2026 - Первичное создание файла.
*/

namespace Tis.KonturIntegration.Models
{
    /// <summary>
    /// Итог выполнения T3-сценария в интеграционной ветке Контур.
    /// </summary>
    public class KonturT3ExecutionResult
    {
        /// <summary>
        /// Получает или задает признак успешного завершения сценария.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Получает или задает внутренний идентификатор timeline ТИС.
        /// </summary>
        public long TimelineId { get; set; }

        /// <summary>
        /// Получает или задает идентификатор перевозки в Контуре.
        /// </summary>
        public string TransportationId { get; set; }

        /// <summary>
        /// Получает или задает идентификатор титула T3 в Контуре.
        /// </summary>
        public string TitleId { get; set; }

        /// <summary>
        /// Получает или задает нормализованный текст результата для логирования.
        /// </summary>
        public string Message { get; set; }
    }
}

