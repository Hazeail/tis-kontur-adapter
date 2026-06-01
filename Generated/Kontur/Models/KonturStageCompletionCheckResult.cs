/*
  ФАЙЛ: KonturStageCompletionCheckResult.cs
  НАЗНАЧЕНИЕ: Модель результата проверки бизнес-завершения этапа Контур ЭТрН.
  Возвращает решение без автоматического изменения состояния этапа.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  29.05.2026 - Первичное создание результата проверки завершения T1.
*/

namespace Tis.KonturIntegration.Models
{
    /// <summary>
    /// Описывает результат проверки, можно ли считать этап Контур ЭТрН бизнес-завершенным.
    /// </summary>
    /// <remarks>Результат отделен от команды подтверждения, чтобы проверка не открывала следующий этап сама по себе.</remarks>
    public class KonturStageCompletionCheckResult
    {
        /// <summary>
        /// Получает или задает признак успешного выполнения самой проверки.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Получает или задает признак того, что evidence достаточно для подтверждения завершения этапа.
        /// </summary>
        public bool CanConfirmCompletion { get; set; }

        /// <summary>
        /// Получает или задает код этапа, который проверялся.
        /// </summary>
        public string StageCode { get; set; }

        /// <summary>
        /// Получает или задает код титула, который проверялся.
        /// </summary>
        public string TitleCode { get; set; }

        /// <summary>
        /// Получает или задает идентификатор timeline документа.
        /// </summary>
        public long TimelineId { get; set; }

        /// <summary>
        /// Получает или задает код причины решения.
        /// </summary>
        public string ReasonCode { get; set; }

        /// <summary>
        /// Получает или задает диагностическое сообщение решения.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Получает или задает evidence, на котором основано решение.
        /// </summary>
        public KonturStageCompletionEvidence Evidence { get; set; }
    }
}