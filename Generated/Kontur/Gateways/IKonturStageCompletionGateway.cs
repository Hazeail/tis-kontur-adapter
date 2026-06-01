/*
  ФАЙЛ: IKonturStageCompletionGateway.cs
  НАЗНАЧЕНИЕ: Порт получения внешних признаков завершения этапа Контур ЭТрН.
  Отделяет use case проверки завершения от конкретного API, raw-log и SQL-источников.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  29.05.2026 - Первичное создание порта evidence для проверки завершения T1.
*/

using Tis.KonturIntegration.Models;

namespace Tis.KonturIntegration.Integration
{
    /// <summary>
    /// Определяет контракт чтения внешних признаков, по которым можно оценивать бизнес-завершение этапа.
    /// </summary>
    /// <remarks>
    /// Порт нужен, чтобы сначала подключить безопасный диагностический источник, а позже заменить его прямым API-чтением статуса Контур.
    /// </remarks>
    public interface IKonturStageCompletionGateway
    {
        /// <summary>
        /// Возвращает внешний evidence-снимок по timeline и этапу.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа в ТИС.</param>
        /// <param name="stageCode">Код этапа, например T1_INITIAL.</param>
        /// <returns>Evidence-снимок или null, если признаки не удалось получить.</returns>
        /// <remarks>Метод не изменяет состояние этапа и не открывает следующий шаг процесса.</remarks>
        KonturStageCompletionEvidence ReadEvidence(long timelineId, string stageCode);
    }
}