/*
  ФАЙЛ: SyncStageStateRefsUseCase.cs
  НАЗНАЧЕНИЕ: Сценарий синхронизации явного состояния этапа с legacy refs оператора Контур.
  Подтягивает TransportationId и TitleId в KonturStageState без ad-hoc SQL в UI.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  29.05.2026 - Первичное создание сценария синхронизации operator refs в явное состояние этапа.
*/

using System;
using Tis.KonturIntegration.Models;
using Tis.KonturIntegration.Storage;

namespace Tis.KonturIntegration.Integration
{
    /// <summary>
    /// Синхронизирует ключевые внешние идентификаторы этапа из TEpdOperatorRef в KonturStageState.
    /// </summary>
    /// <remarks>
    /// Сценарий нужен как compatibility-мост после legacy runtime и перед evidence-проверкой T1,
    /// чтобы состояние процесса не зависело от ручной SQL-синхронизации refs.
    /// </remarks>
    public class SyncStageStateRefsUseCase
    {
        /// <summary>
        /// Инициализирует сценарий зависимостями состояния этапа и legacy refs оператора.
        /// </summary>
        /// <param name="stageStateRepository">Repository явного состояния этапа.</param>
        /// <param name="operatorRefRepository">Repository legacy refs оператора.</param>
        /// <remarks>Use case не создает SQL-реализации самостоятельно и не должен собираться внутри UI-логики.</remarks>
        public SyncStageStateRefsUseCase(
            IKonturStageStateRepository stageStateRepository,
            IKonturOperatorRefRepository operatorRefRepository)
        {
            if (stageStateRepository == null)
            {
                throw new ArgumentNullException("stageStateRepository");
            }

            if (operatorRefRepository == null)
            {
                throw new ArgumentNullException("operatorRefRepository");
            }

            StageStateRepository = stageStateRepository;
            OperatorRefRepository = operatorRefRepository;
        }

        /// <summary>
        /// Получает repository явного состояния этапа.
        /// </summary>
        public IKonturStageStateRepository StageStateRepository { get; private set; }

        /// <summary>
        /// Получает repository legacy refs оператора.
        /// </summary>
        public IKonturOperatorRefRepository OperatorRefRepository { get; private set; }

        /// <summary>
        /// Синхронизирует TransportationId и TitleId в явном состоянии этапа.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа в ТИС.</param>
        /// <param name="stageCode">Код этапа, например T1_INITIAL, T2, T3 или T4.</param>
        /// <returns>Актуальное состояние этапа после синхронизации или null, если синхронизировать нечего.</returns>
        /// <remarks>
        /// Сценарий не меняет Completed и NextStageAllowed.
        /// Если refs отсутствуют, текущее состояние возвращается без принудительной записи.
        /// </remarks>
        public KonturStageState Execute(long timelineId, string stageCode)
        {
            if (timelineId <= 0)
            {
                throw new ArgumentOutOfRangeException("timelineId", "TimelineId должен быть положительным.");
            }

            var normalizedStageCode = NormalizeStageCode(stageCode);
            var titleCode = StageToTitle(normalizedStageCode);
            var state = StageStateRepository.Get(timelineId, normalizedStageCode);
            var transportationId = ReadTransportationId(timelineId);
            var titleId = ReadTitleId(timelineId, titleCode);

            if (state == null && string.IsNullOrEmpty(transportationId) && string.IsNullOrEmpty(titleId))
            {
                return null;
            }

            state = state ?? new KonturStageState
            {
                TimelineId = timelineId,
                StageCode = normalizedStageCode,
                TitleCode = titleCode
            };

            var changed = false;
            if (!string.IsNullOrEmpty(transportationId) && !string.Equals(state.TransportationId, transportationId, StringComparison.Ordinal))
            {
                state.TransportationId = transportationId;
                changed = true;
            }

            if (!string.IsNullOrEmpty(titleId) && !string.Equals(state.TitleId, titleId, StringComparison.Ordinal))
            {
                state.TitleId = titleId;
                changed = true;
            }

            if (changed)
            {
                state.TitleCode = titleCode;

                StageStateRepository.Save(state);
                return StageStateRepository.Get(timelineId, normalizedStageCode);
            }

            return state;
        }

        /// <summary>
        /// Читает TransportationId из operator refs.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <returns>TransportationId или пустую строку.</returns>
        /// <remarks>TransportationId остается technical context и не считается разрешением следующего этапа сам по себе.</remarks>
        private string ReadTransportationId(long timelineId)
        {
            return Safe(OperatorRefRepository.GetLatestRefValue(timelineId, "Kontur", "TransportationId"));
        }

        /// <summary>
        /// Читает TitleId этапа из operator refs.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="titleCode">Код титула этапа.</param>
        /// <returns>TitleId или пустую строку.</returns>
        /// <remarks>Для T1 сохраняется legacy refType TitleId, для T2/T3/T4 используется stage-specific refType.</remarks>
        private string ReadTitleId(long timelineId, string titleCode)
        {
            if (string.Equals(titleCode, "T1", StringComparison.OrdinalIgnoreCase))
            {
                return Safe(OperatorRefRepository.GetLatestRefValue(timelineId, "Kontur", "TitleId"));
            }

            if (string.IsNullOrEmpty(titleCode) || string.Equals(titleCode, "UNKNOWN", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return Safe(OperatorRefRepository.GetLatestRefValue(timelineId, "Kontur", titleCode + "TitleId"));
        }

        /// <summary>
        /// Нормализует код этапа для ключей состояния.
        /// </summary>
        /// <param name="stageCode">Исходный код этапа.</param>
        /// <returns>Код этапа в верхнем регистре или пустую строку.</returns>
        /// <remarks>Нормализация синхронизирована с repository состояния этапа.</remarks>
        private string NormalizeStageCode(string stageCode)
        {
            return string.IsNullOrEmpty(stageCode) ? string.Empty : stageCode.Trim().ToUpperInvariant();
        }

        /// <summary>
        /// Преобразует код этапа в код титула.
        /// </summary>
        /// <param name="stageCode">Код этапа.</param>
        /// <returns>Код титула T1/T2/T3/T4 или UNKNOWN.</returns>
        /// <remarks>Маппинг нужен, чтобы не размазывать правила refType по UI и внешним gateway.</remarks>
        private string StageToTitle(string stageCode)
        {
            if (string.Equals(stageCode, "T1_INITIAL", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stageCode, "T1_DRAFT", StringComparison.OrdinalIgnoreCase))
            {
                return "T1";
            }

            if (string.Equals(stageCode, "T2", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stageCode, "T3", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stageCode, "T4", StringComparison.OrdinalIgnoreCase))
            {
                return stageCode;
            }

            return "UNKNOWN";
        }

        /// <summary>
        /// Нормализует строковое значение refs для безопасного сравнения.
        /// </summary>
        /// <param name="value">Исходное строковое значение.</param>
        /// <returns>Обрезанное значение или пустую строку.</returns>
        /// <remarks>Пустые и whitespace-значения не должны создавать ложные изменения состояния.</remarks>
        private string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Trim();
        }
    }
}
