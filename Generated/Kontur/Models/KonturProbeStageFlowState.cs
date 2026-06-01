/*
  ФАЙЛ: KonturProbeStageFlowState.cs
  НАЗНАЧЕНИЕ: Модели состояния шагов экрана KonturProbe для отображения рабочего потока этапа ЭТрН.
  Файл отделяет UI-состояние от code-behind страницы, чтобы статусный расчет не жил внутри WebForms-разметки.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  23.05.2026 - Первичное создание моделей состояния шагов KonturProbe.
*/

namespace Tis.KonturIntegration.Models
{
    /// <summary>
    /// Описывает состояние одного шага в рабочем потоке этапа KonturProbe.
    /// </summary>
    public class KonturProbeFlowStepState
    {
        /// <summary>
        /// Получает или задает CSS-класс визуального состояния шага.
        /// </summary>
        public string CssClass { get; set; }

        /// <summary>
        /// Получает или задает короткий текст состояния шага.
        /// </summary>
        public string Text { get; set; }
    }

    /// <summary>
    /// Описывает агрегированное состояние шагов текущего этапа на странице KonturProbe.
    /// </summary>
    public class KonturProbeStageFlowState
    {
        /// <summary>
        /// Получает или задает состояние шага формирования XML.
        /// </summary>
        public KonturProbeFlowStepState XmlStep { get; set; }

        /// <summary>
        /// Получает или задает состояние шага подписи.
        /// </summary>
        public KonturProbeFlowStepState SignatureStep { get; set; }

        /// <summary>
        /// Получает или задает состояние шага отправки.
        /// </summary>
        public KonturProbeFlowStepState SendStep { get; set; }

        /// <summary>
        /// Получает или задает состояние шага результата.
        /// </summary>
        public KonturProbeFlowStepState ResultStep { get; set; }

        /// <summary>
        /// Получает или задает короткий итоговый текст результата этапа.
        /// </summary>
        public string ResultSummary { get; set; }
    }
}
