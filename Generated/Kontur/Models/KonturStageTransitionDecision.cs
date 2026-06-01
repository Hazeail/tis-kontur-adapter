/*
  ФАЙЛ: KonturStageTransitionDecision.cs
  НАЗНАЧЕНИЕ: Модель решения о готовности этапа Контур ЭТрН к запуску.
  Отделяет правила переходов процесса от UI, SQL-хранилища и операторного API.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  29.05.2026 - Первичное создание модели решения перехода этапа.
*/

namespace Tis.KonturIntegration.Models
{
    /// <summary>
    /// Описывает результат проверки готовности этапа к запуску и используется use case-слоем перед обращением к операторному adapter.
    /// </summary>
    public class KonturStageTransitionDecision
    {
        /// <summary>
        /// Получает или задает признак разрешения запуска этапа.
        /// </summary>
        public bool IsAllowed { get; set; }

        /// <summary>
        /// Получает или задает код проверяемого этапа.
        /// </summary>
        public string StageCode { get; set; }

        /// <summary>
        /// Получает или задает код титула, соответствующий этапу.
        /// </summary>
        public string TitleCode { get; set; }

        /// <summary>
        /// Получает или задает код причины запрета или разрешения.
        /// </summary>
        public string ReasonCode { get; set; }

        /// <summary>
        /// Получает или задает диагностическое сообщение решения.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Получает или задает код предыдущего этапа, если проверка зависит от него.
        /// </summary>
        public string RequiredPreviousStageCode { get; set; }
    }
}
