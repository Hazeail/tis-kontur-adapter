/*
  ФАЙЛ: KonturStageSignerContext.cs
  НАЗНАЧЕНИЕ: Контекст выбора подписанта для этапа ЭТрН Контур.
  Хранит сведения о роли этапа, организации и списке допустимых кандидатов для страницы KonturProbe.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  22.05.2026 - Первичное создание контекста выбора подписанта.
*/

using System.Collections.Generic;

namespace Tis.KonturIntegration.Models
{
    /// <summary>
    /// Описывает контекст выбора подписанта для конкретного этапа.
    /// </summary>
    public class KonturStageSignerContext
    {
        /// <summary>
        /// Инициализирует пустой контекст выбора подписанта.
        /// </summary>
        public KonturStageSignerContext()
        {
            Candidates = new List<KonturStageSignerCandidate>();
        }

        /// <summary>
        /// Получает или задает код титула этапа T1/T2/T3/T4.
        /// </summary>
        public string TitleCode { get; set; }

        /// <summary>
        /// Получает или задает код роли этапа ГО/ТК/ГП.
        /// </summary>
        public string RequiredRoleCode { get; set; }

        /// <summary>
        /// Получает или задает человекочитаемое имя роли этапа.
        /// </summary>
        public string RequiredRoleName { get; set; }

        /// <summary>
        /// Получает или задает идентификатор организации роли.
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
        /// Получает или задает текст ошибки, если контекст определить не удалось.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Получает список допустимых подписантов этапа.
        /// </summary>
        public IList<KonturStageSignerCandidate> Candidates { get; private set; }

        /// <summary>
        /// Получает признак успешного построения контекста без ошибок.
        /// </summary>
        public bool IsResolved
        {
            get { return string.IsNullOrEmpty(ErrorMessage); }
        }
    }
}
