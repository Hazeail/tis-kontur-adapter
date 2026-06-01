/*
  ФАЙЛ: KonturRoleAccessRecord.cs
  НАЗНАЧЕНИЕ: Модель записи ролевого доступа для отправки титулов ЭТрН через Контур.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  13.05.2026 - Первичное создание модели ролевого доступа по титулу/роли.
*/

using System;

namespace Tis.KonturIntegration.Models
{
    /// <summary>
    /// Описывает запись ролевого доступа для этапа ЭТрН.
    /// Используется резолвером доступа для выбора boxId и, при наличии, ApiKey.
    /// </summary>
    public class KonturRoleAccessRecord
    {
        /// <summary>
        /// Получает или задает идентификатор записи.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Получает или задает код оператора.
        /// </summary>
        public string OperatorCode { get; set; }

        /// <summary>
        /// Получает или задает код титула (T1/T2/T3/T4).
        /// </summary>
        public string TitleCode { get; set; }

        /// <summary>
        /// Получает или задает бизнес-роль отправителя.
        /// </summary>
        public string SenderRole { get; set; }

        /// <summary>
        /// Получает или задает ИНН организации роли.
        /// </summary>
        public string Inn { get; set; }

        /// <summary>
        /// Получает или задает КПП организации роли.
        /// </summary>
        public string Kpp { get; set; }

        /// <summary>
        /// Получает или задает DiadocBoxId отправителя.
        /// </summary>
        public string DiadocBoxId { get; set; }

        /// <summary>
        /// Получает или задает переопределенный API-ключ для роли.
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>
        /// Получает или задает приоритет выбора записи.
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Получает или задает признак активности записи.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Получает или задает момент изменения записи.
        /// </summary>
        public DateTime UpdatedAt { get; set; }
    }
}
