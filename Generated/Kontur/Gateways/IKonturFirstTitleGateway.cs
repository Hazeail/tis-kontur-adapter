/*
  ФАЙЛ: IKonturFirstTitleGateway.cs
  НАЗНАЧЕНИЕ: Порт отправки первого титула Контур ЭТрН.
  Отделяет use case первого титула от конкретного KonturAdapter и деталей операторного API.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  29.05.2026 - Первичное создание порта отправки первого титула.
*/

using Tis.KonturIntegration.Models;

namespace Tis.KonturIntegration.Integration
{
    /// <summary>
    /// Описывает контракт отправки первого титула T1 в Контур.
    /// Используется use case-слоем как граница между процессом и adapter-реализацией.
    /// </summary>
    public interface IKonturFirstTitleGateway
    {
        /// <summary>
        /// Отправляет первичный титул T1 как старт нового документооборота.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа в ТИС.</param>
        /// <param name="xmlPath">Путь к XML-файлу титула T1.</param>
        /// <param name="senderBoxId">DiadocBoxId отправителя, выбранный для этапа.</param>
        /// <returns>Результат отправки первого титула.</returns>
        /// <remarks>Метод не определяет готовность этапа и не управляет состоянием процесса.</remarks>
        KonturT1ExecutionResult SendInitial(long timelineId, string xmlPath, string senderBoxId);

        /// <summary>
        /// Отправляет титул T1 через draft-сценарий Контур.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа в ТИС.</param>
        /// <param name="xmlPath">Путь к XML-файлу титула T1.</param>
        /// <param name="senderBoxId">DiadocBoxId отправителя, выбранный для этапа.</param>
        /// <returns>Результат отправки первого титула через draft-сценарий.</returns>
        /// <remarks>Метод оставлен отдельным, чтобы use case явно выбирал режим первого титула.</remarks>
        KonturT1ExecutionResult SendDraft(long timelineId, string xmlPath, string senderBoxId);
    }
}
