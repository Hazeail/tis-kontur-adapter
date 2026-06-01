/*
  ФАЙЛ: KonturStageSignerCandidate.cs
  НАЗНАЧЕНИЕ: Модель допустимого подписанта этапа ЭТрН Контур.
  Используется страницей KonturProbe для ручного выбора подписанта по роли и организации этапа.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  22.05.2026 - Первичное создание модели кандидата подписанта этапа.
*/

using System;

namespace Tis.KonturIntegration.Models
{
    /// <summary>
    /// Описывает допустимого подписанта для конкретного этапа ЭТрН.
    /// </summary>
    public class KonturStageSignerCandidate
    {
        /// <summary>
        /// Получает или задает код титула этапа T1/T2/T3/T4.
        /// </summary>
        public string TitleCode { get; set; }

        /// <summary>
        /// Получает или задает код роли этапа в сокращенном виде ГО/ТК/ГП.
        /// </summary>
        public string RequiredRoleCode { get; set; }

        /// <summary>
        /// Получает или задает человекочитаемое имя роли этапа.
        /// </summary>
        public string RequiredRoleName { get; set; }

        /// <summary>
        /// Получает или задает идентификатор организации, за которую подписывается этап.
        /// </summary>
        public long RequiredKontragentId { get; set; }

        /// <summary>
        /// Получает или задает ИНН организации роли.
        /// </summary>
        public string RequiredKontragentInn { get; set; }

        /// <summary>
        /// Получает или задает наименование организации роли.
        /// </summary>
        public string RequiredKontragentName { get; set; }

        /// <summary>
        /// Получает или задает идентификатор TFizLico допустимого подписанта.
        /// </summary>
        public long SignerFizLicoId { get; set; }

        /// <summary>
        /// Получает или задает ФИО подписанта.
        /// </summary>
        public string SignerFio { get; set; }

        /// <summary>
        /// Получает или задает должность подписанта.
        /// </summary>
        public string Position { get; set; }

        /// <summary>
        /// Получает или задает ИНН физического лица подписанта.
        /// </summary>
        public string SignerInnFl { get; set; }

        /// <summary>
        /// Получает или задает источник полномочий: TRukAndUL или TMchdK.
        /// </summary>
        public string AuthoritySource { get; set; }

        /// <summary>
        /// Получает или задает краткое описание основания полномочий.
        /// </summary>
        public string AuthorityDescription { get; set; }

        /// <summary>
        /// Получает или задает тип документа-основания полномочий.
        /// </summary>
        public string AuthorityDocType { get; set; }

        /// <summary>
        /// Получает или задает дату документа-основания полномочий.
        /// </summary>
        public DateTime? AuthorityDocDate { get; set; }

        /// <summary>
        /// Получает или задает номер МЧД, если кандидат найден через TMchdK.
        /// </summary>
        public string MchdNumber { get; set; }
    }
}
