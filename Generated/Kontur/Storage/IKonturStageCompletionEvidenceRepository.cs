/*
  ФАЙЛ: IKonturStageCompletionEvidenceRepository.cs
  НАЗНАЧЕНИЕ: Порт хранения evidence завершения этапа Контур ЭТрН.
  Изолирует use case проверки завершения от SQL-таблицы диагностических признаков.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  29.05.2026 - Первичное создание порта хранения completion evidence.
*/

using Tis.KonturIntegration.Models;

namespace Tis.KonturIntegration.Storage
{
    /// <summary>
    /// Определяет контракт сохранения и чтения evidence завершения этапа.
    /// </summary>
    /// <remarks>Evidence хранится отдельно от состояния этапа, чтобы не смешивать процесс и диагностические признаки.</remarks>
    public interface IKonturStageCompletionEvidenceRepository
    {
        /// <summary>
        /// Возвращает последнее evidence по timeline и этапу.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа в ТИС.</param>
        /// <param name="stageCode">Код этапа, например T1_INITIAL.</param>
        /// <returns>Последнее evidence или null, если проверка еще не выполнялась.</returns>
        KonturStageCompletionEvidence GetLatest(long timelineId, string stageCode);

        /// <summary>
        /// Сохраняет новый evidence-снимок.
        /// </summary>
        /// <param name="evidence">Evidence-снимок внешних признаков завершения.</param>
        /// <remarks>Repository хранит историю проверок, а не перезаписывает единственную строку.</remarks>
        void Save(KonturStageCompletionEvidence evidence);
    }
}