/*
  ФАЙЛ: KonturStageState.cs
  НАЗНАЧЕНИЕ: Модель явного состояния этапа Контур ЭТрН.
  Отделяет жизненный цикл этапа от XML/SGN-артефактов, raw-log и legacy timeline.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  28.05.2026 - Первичное создание модели состояния этапа для реконструкционного слоя.
  29.05.2026 - Добавлены TransportationId и TitleId, чтобы состояние этапа хранило ключевые operator refs без ручной SQL-синхронизации.
*/

using System;

namespace Tis.KonturIntegration.Models
{
    /// <summary>
    /// Описывает сохраненное состояние этапа Контур ЭТрН и используется как внутренняя модель процесса между UI, use case и storage-слоем.
    /// </summary>
    public class KonturStageState
    {
        /// <summary>
        /// Получает или задает внутренний идентификатор записи состояния.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Получает или задает идентификатор timeline документа в ТИС.
        /// </summary>
        public long TimelineId { get; set; }

        /// <summary>
        /// Получает или задает код этапа UI или сценария, например T1_INITIAL, T2, T3 или T4.
        /// </summary>
        public string StageCode { get; set; }

        /// <summary>
        /// Получает или задает код титула ЭТрН, например T1, T2, T3 или T4.
        /// </summary>
        public string TitleCode { get; set; }

        /// <summary>
        /// Получает или задает признак того, что XML этапа сформирован и зафиксирован.
        /// </summary>
        public bool XmlBuilt { get; set; }

        /// <summary>
        /// Получает или задает признак того, что подпись этапа импортирована или подготовлена.
        /// </summary>
        public bool SignatureImported { get; set; }

        /// <summary>
        /// Получает или задает признак того, что этап был отправлен оператору.
        /// </summary>
        public bool Sent { get; set; }

        /// <summary>
        /// Получает или задает признак бизнес-завершения этапа.
        /// </summary>
        public bool Completed { get; set; }

        /// <summary>
        /// Получает или задает признак разрешения следующего этапа процесса.
        /// </summary>
        public bool NextStageAllowed { get; set; }

        /// <summary>
        /// Получает или задает последний известный TransportationId по этапу.
        /// </summary>
        public string TransportationId { get; set; }

        /// <summary>
        /// Получает или задает последний известный TitleId по этапу.
        /// </summary>
        public string TitleId { get; set; }

        /// <summary>
        /// Получает или задает последний известный статус оператора по этапу.
        /// </summary>
        public string LastOperatorStatus { get; set; }

        /// <summary>
        /// Получает или задает последний технический код ошибки оператора или адаптера.
        /// </summary>
        public string LastErrorCode { get; set; }

        /// <summary>
        /// Получает или задает последнее диагностическое сообщение ошибки.
        /// </summary>
        public string LastErrorMessage { get; set; }

        /// <summary>
        /// Получает или задает дату последнего обновления состояния.
        /// </summary>
        public DateTime UpdatedAt { get; set; }
    }
}
