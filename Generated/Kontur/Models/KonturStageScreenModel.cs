/*
  ФАЙЛ: KonturStageScreenModel.cs
  НАЗНАЧЕНИЕ: Экранная модель состояния этапа Контур ЭТрН для будущего тонкого UI-слоя.
  Объединяет явное состояние процесса и текущий fallback read-model без изменения runtime-пути WebForms.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  28.05.2026 - Первичное создание экранной модели состояния этапа.
*/

namespace Tis.KonturIntegration.Models
{
    /// <summary>
    /// Описывает данные, которые экран Контур ЭТрН должен получать из прикладного слоя вместо прямой диагностики code-behind.
    /// Используется как read-model между будущим ScreenService и WebForms UI.
    /// </summary>
    public class KonturStageScreenModel
    {
        /// <summary>
        /// Получает или задает идентификатор timeline документа в ТИС.
        /// </summary>
        public long TimelineId { get; set; }

        /// <summary>
        /// Получает или задает код этапа UI или сценария.
        /// </summary>
        public string StageCode { get; set; }

        /// <summary>
        /// Получает или задает код титула ЭТрН.
        /// </summary>
        public string TitleCode { get; set; }

        /// <summary>
        /// Получает или задает признак наличия явного сохраненного состояния этапа.
        /// </summary>
        public bool HasExplicitState { get; set; }

        /// <summary>
        /// Получает или задает источник состояния, выбранный для отображения.
        /// </summary>
        public string StateSource { get; set; }

        /// <summary>
        /// Получает или задает явное сохраненное состояние этапа.
        /// </summary>
        public KonturStageState StageState { get; set; }

        /// <summary>
        /// Получает или задает вычисляемое состояние текущего fallback read-model.
        /// </summary>
        public KonturProbeStageFlowState FallbackFlowState { get; set; }

        /// <summary>
        /// Получает или задает итоговый текст состояния для отображения оператору.
        /// </summary>
        public string ResultSummary { get; set; }
    }
}
