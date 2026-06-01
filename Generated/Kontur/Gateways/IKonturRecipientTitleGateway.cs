/*
  ФАЙЛ: IKonturRecipientTitleGateway.cs
  НАЗНАЧЕНИЕ: Порт отправки ответных титулов Контур ЭТрН.
  Отделяет use case ответных титулов от конкретного KonturAdapter и деталей операторного API.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  29.05.2026 - Первичное создание порта отправки ответных титулов.
*/

using Tis.KonturIntegration.Models;

namespace Tis.KonturIntegration.Integration
{
    /// <summary>
    /// Описывает контракт отправки ответных титулов T2, T3 и T4 в Контур.
    /// Используется use case-слоем как единая граница для сценариев ответных этапов.
    /// </summary>
    public interface IKonturRecipientTitleGateway
    {
        /// <summary>
        /// Отправляет ответный титул T2, T3 или T4 в существующий операторный документооборот.
        /// </summary>
        /// <param name="titleCode">Код титула: T2, T3 или T4.</param>
        /// <param name="timelineId">Идентификатор timeline документа в ТИС.</param>
        /// <param name="xmlPath">Путь к XML-файлу ответного титула.</param>
        /// <param name="signaturePath">Путь к detached-подписи ответного титула.</param>
        /// <param name="senderBoxId">DiadocBoxId отправителя, выбранный для этапа.</param>
        /// <returns>Унифицированный результат отправки этапа.</returns>
        /// <remarks>Gateway не принимает решение о допустимости перехода между этапами.</remarks>
        KonturStageExecutionResult SendRecipientTitle(string titleCode, long timelineId, string xmlPath, string signaturePath, string senderBoxId);
    }
}
