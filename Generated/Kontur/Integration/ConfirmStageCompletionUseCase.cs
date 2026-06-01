/*
  ФАЙЛ: ConfirmStageCompletionUseCase.cs
  НАЗНАЧЕНИЕ: Сценарий явного подтверждения завершения этапа Контур ЭТрН оператором.
  Отделяет операторское подтверждение процесса от UI и от прямой записи в SQL-хранилище состояния.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  29.05.2026 - Первичное создание сценария подтверждения завершения этапа и разрешения следующего шага.
*/

using System;
using Tis.KonturIntegration.Models;
using Tis.KonturIntegration.Storage;

namespace Tis.KonturIntegration.Integration
{
    /// <summary>
    /// Выполняет прикладной сценарий явного подтверждения завершения этапа Контур ЭТрН.
    /// </summary>
    /// <remarks>
    /// Сценарий нужен как временный операторский мост, пока завершение этапа еще не подтверждается
    /// автоматически отдельным backend-циклом чтения статуса оператора.
    /// </remarks>
    public class ConfirmStageCompletionUseCase
    {
        /// <summary>
        /// Инициализирует сценарий repository явного состояния этапа.
        /// </summary>
        /// <param name="stageStateRepository">Repository чтения и сохранения состояния этапа.</param>
        /// <remarks>Сценарий не создает SQL-адаптеры внутри себя и использует только переданный storage boundary.</remarks>
        public ConfirmStageCompletionUseCase(IKonturStageStateRepository stageStateRepository)
        {
            if (stageStateRepository == null)
            {
                throw new ArgumentNullException("stageStateRepository");
            }

            StageStateRepository = stageStateRepository;
        }

        /// <summary>
        /// Получает repository явного состояния этапа.
        /// </summary>
        public IKonturStageStateRepository StageStateRepository { get; private set; }

        /// <summary>
        /// Подтверждает завершение этапа и при необходимости открывает следующий шаг процесса.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа в ТИС.</param>
        /// <param name="stageCode">Код этапа UI или сценария.</param>
        /// <param name="operatorStatus">Операторский комментарий подтверждения.</param>
        /// <returns>Обновленное явное состояние этапа.</returns>
        /// <remarks>
        /// Подтверждение допускается только для этапа, который уже был отправлен.
        /// Для T4 следующий шаг не открывается, потому что цепочка на нем завершается.
        /// </remarks>
        public KonturStageState Execute(long timelineId, string stageCode, string operatorStatus)
        {
            var normalizedStageCode = NormalizeStageCode(stageCode);
            if (timelineId <= 0)
            {
                throw new ArgumentOutOfRangeException("timelineId");
            }

            if (string.IsNullOrEmpty(normalizedStageCode))
            {
                throw new ArgumentException("StageCode должен быть указан.", "stageCode");
            }

            var state = StageStateRepository.Get(timelineId, normalizedStageCode);
            if (state == null)
            {
                throw new ApplicationException("Явное состояние этапа не найдено. Сначала нужно подготовить и отправить этап.");
            }

            if (!state.Sent && !state.Completed)
            {
                throw new ApplicationException("Нельзя подтвердить этап, который еще не был отправлен.");
            }

            state.StageCode = normalizedStageCode;
            state.TitleCode = NormalizeTitleCode(state.TitleCode, normalizedStageCode);
            state.Completed = true;
            state.NextStageAllowed = AllowsNextStage(normalizedStageCode);
            state.LastOperatorStatus = string.IsNullOrEmpty(operatorStatus) ? "Этап подтвержден оператором." : operatorStatus;
            state.LastErrorCode = string.Empty;
            state.LastErrorMessage = string.Empty;

            StageStateRepository.Save(state);
            return state;
        }

        /// <summary>
        /// Определяет, нужно ли открывать следующий этап после подтверждения текущего.
        /// </summary>
        /// <param name="stageCode">Нормализованный код текущего этапа.</param>
        /// <returns>True, если после этапа есть следующий шаг; иначе false.</returns>
        /// <remarks>T4 считается финальным этапом и не открывает следующий шаг процесса.</remarks>
        private bool AllowsNextStage(string stageCode)
        {
            return !string.Equals(stageCode, "T4", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Нормализует код этапа для чтения и сохранения состояния.
        /// </summary>
        /// <param name="stageCode">Исходный код этапа.</param>
        /// <returns>Код этапа в верхнем регистре или пустую строку.</returns>
        /// <remarks>Единая нормализация нужна для совпадения ключей UI и SQL-хранилища состояния.</remarks>
        private string NormalizeStageCode(string stageCode)
        {
            return string.IsNullOrEmpty(stageCode) ? string.Empty : stageCode.Trim().ToUpperInvariant();
        }

        /// <summary>
        /// Возвращает код титула этапа для хранения состояния.
        /// </summary>
        /// <param name="titleCode">Текущий код титула из состояния.</param>
        /// <param name="stageCode">Нормализованный код этапа.</param>
        /// <returns>Код титула T1/T2/T3/T4 или UNKNOWN.</returns>
        /// <remarks>Fallback нужен для старых состояний, сохраненных до явной синхронизации title-code.</remarks>
        private string NormalizeTitleCode(string titleCode, string stageCode)
        {
            if (!string.IsNullOrEmpty(titleCode))
            {
                return titleCode.Trim().ToUpperInvariant();
            }

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
    }
}
