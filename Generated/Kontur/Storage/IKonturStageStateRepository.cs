/*
  ФАЙЛ: IKonturStageStateRepository.cs
  НАЗНАЧЕНИЕ: Порт хранения явного состояния этапа Контур ЭТрН.
  Изолирует use case и экранные сервисы от SQL-структуры реконструкционного слоя.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  28.05.2026 - Первичное создание порта хранения состояния этапа.
*/

using Tis.KonturIntegration.Models;

namespace Tis.KonturIntegration.Storage
{
    /// <summary>
    /// Определяет контракт чтения и сохранения состояния этапа Контур ЭТрН для слоя use case и экранного read-model.
    /// </summary>
    public interface IKonturStageStateRepository
    {
        /// <summary>
        /// Возвращает сохраненное состояние этапа по timeline и коду этапа.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа в ТИС.</param>
        /// <param name="stageCode">Код этапа, например T1_INITIAL, T2, T3 или T4.</param>
        /// <returns>Сохраненное состояние этапа или null, если состояние еще не зафиксировано.</returns>
        /// <remarks>Метод не вычисляет состояние по артефактам и читает только явную SQL-границу реконструкции.</remarks>
        KonturStageState Get(long timelineId, string stageCode);

        /// <summary>
        /// Сохраняет текущий снимок состояния этапа.
        /// </summary>
        /// <param name="state">Состояние этапа для вставки или обновления.</param>
        /// <remarks>Для одной пары TimelineId и StageCode хранится одна актуальная запись состояния.</remarks>
        void Save(KonturStageState state);
    }
}
