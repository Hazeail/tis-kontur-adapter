/*
  ФАЙЛ: KonturTitleBuildResult.cs
  НАЗНАЧЕНИЕ: Модель результата сборки или чтения XML титула ЭТрН для stage-runner Контур.
  Позволяет явно вернуть успех, ошибку и полезную нагрузку без исключений в штатных бизнес-сценариях.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  13.05.2026 - Первичное создание результата сборки титула для внутреннего конвейера ЭТрН.
*/

namespace Tis.KonturIntegration.Models
{
    /// <summary>
    /// Передает результат построения XML титула между builder-слоем и оркестратором этапа Контур.
    /// Используется для явной остановки сценария до подписи и отправки, если XML не может быть собран.
    /// </summary>
    public class KonturTitleBuildResult
    {
        /// <summary>
        /// Получает или задает признак успешного построения или чтения титула.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Получает или задает код титула: T1, T2, T3 или T4.
        /// </summary>
        public string TitleCode { get; set; }

        /// <summary>
        /// Получает или задает идентификатор timeline документа.
        /// </summary>
        public long TimelineId { get; set; }

        /// <summary>
        /// Получает или задает артефакт титула, если сборка завершилась успешно.
        /// </summary>
        public KonturTitleArtifact Artifact { get; set; }

        /// <summary>
        /// Получает или задает техническое сообщение результата.
        /// </summary>
        public string Message { get; set; }
    }
}
