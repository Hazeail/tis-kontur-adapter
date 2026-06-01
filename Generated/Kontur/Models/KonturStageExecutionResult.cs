/*
  ФАЙЛ: KonturStageExecutionResult.cs
  НАЗНАЧЕНИЕ: Унифицированный результат выполнения этапа ЭТрН Контур для UI ТИС.
  Используется оркестратором этапов для единообразного возврата данных.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  12.05.2026 - Первичное создание модели унифицированного результата этапа.
*/

namespace Tis.KonturIntegration.Models
{
    /// <summary>
    /// Унифицированный результат выполнения этапа ЭТрН Контур.
    /// </summary>
    public class KonturStageExecutionResult
    {
        /// <summary>
        /// Получает или задает признак успешного завершения этапа.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Получает или задает код этапа, который запускался.
        /// </summary>
        public string StageCode { get; set; }

        /// <summary>
        /// Получает или задает идентификатор timeline документа.
        /// </summary>
        public long TimelineId { get; set; }

        /// <summary>
        /// Получает или задает transportationId из Контур.
        /// </summary>
        public string TransportationId { get; set; }

        /// <summary>
        /// Получает или задает идентификатор отправленного титула.
        /// </summary>
        public string TitleId { get; set; }

        /// <summary>
        /// Получает или задает текстовое сообщение результата.
        /// </summary>
        public string Message { get; set; }
    }
}
