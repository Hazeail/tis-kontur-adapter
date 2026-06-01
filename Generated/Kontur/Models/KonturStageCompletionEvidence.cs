/*
  ФАЙЛ: KonturStageCompletionEvidence.cs
  НАЗНАЧЕНИЕ: Модель доказательства внешнего завершения этапа Контур ЭТрН.
  Отделяет факт отправки титула от признаков, по которым этап можно считать бизнес-завершенным.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  29.05.2026 - Первичное создание модели evidence для проверки завершения T1 перед открытием T2.
*/

using System;

namespace Tis.KonturIntegration.Models
{
    /// <summary>
    /// Описывает внешний набор признаков, по которым можно оценивать завершение этапа Контур ЭТрН.
    /// </summary>
    /// <remarks>
    /// Модель не является самим состоянием этапа. Она фиксирует диагностическое доказательство,
    /// полученное из API, raw-log, refs или временного ручного источника проверки.
    /// </remarks>
    public class KonturStageCompletionEvidence
    {
        /// <summary>
        /// Получает или задает внутренний идентификатор записи evidence.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Получает или задает идентификатор timeline документа в ТИС.
        /// </summary>
        public long TimelineId { get; set; }

        /// <summary>
        /// Получает или задает код этапа UI или сценария, например T1_INITIAL.
        /// </summary>
        public string StageCode { get; set; }

        /// <summary>
        /// Получает или задает код титула ЭТрН, например T1.
        /// </summary>
        public string TitleCode { get; set; }

        /// <summary>
        /// Получает или задает идентификатор перевозки в Контур.
        /// </summary>
        public string TransportationId { get; set; }

        /// <summary>
        /// Получает или задает идентификатор титула в Контур.
        /// </summary>
        public string TitleId { get; set; }

        /// <summary>
        /// Получает или задает внешний статус документа или перевозки, если он прочитан из оператора.
        /// </summary>
        public string ExternalDocumentStatus { get; set; }

        /// <summary>
        /// Получает или задает внешний статус титула, если он прочитан из оператора.
        /// </summary>
        public string ExternalTitleStatus { get; set; }

        /// <summary>
        /// Получает или задает код внешнего действия, которое указывает на готовность следующего титула.
        /// </summary>
        public string ExternalActionCode { get; set; }

        /// <summary>
        /// Получает или задает признак того, что внешний объект все еще выглядит как черновик.
        /// </summary>
        public bool IsDraft { get; set; }

        /// <summary>
        /// Получает или задает признак активной ошибки по этапу.
        /// </summary>
        public bool HasActiveError { get; set; }

        /// <summary>
        /// Получает или задает последний HTTP-статус, связанный с проверяемым этапом.
        /// </summary>
        public int? HttpStatus { get; set; }

        /// <summary>
        /// Получает или задает краткое текстовое описание источника evidence.
        /// </summary>
        public string RawEvidenceSummary { get; set; }

        /// <summary>
        /// Получает или задает источник evidence: StoredDiagnostic, OperatorApiStatus или ManualOperator.
        /// </summary>
        public string CompletionSource { get; set; }

        /// <summary>
        /// Получает или задает дату проверки evidence.
        /// </summary>
        public DateTime CheckedAt { get; set; }
    }
}