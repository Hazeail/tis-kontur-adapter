/*
  ФАЙЛ: CheckT1CompletionUseCase.cs
  НАЗНАЧЕНИЕ: Сценарий проверки внешних признаков бизнес-завершения T1 Контур ЭТрН.
  Отделяет проверку завершения первого титула от ручного подтверждения и открытия T2.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  29.05.2026 - Первичное создание сценария проверки completion evidence для T1.
  29.05.2026 - Перед evidence-проверкой добавлена синхронизация TransportationId и TitleId из legacy refs.
*/

using System;
using Tis.KonturIntegration.Models;
using Tis.KonturIntegration.Storage;

namespace Tis.KonturIntegration.Integration
{
    /// <summary>
    /// Проверяет, достаточно ли внешних признаков для подтверждения бизнес-завершения T1.
    /// </summary>
    /// <remarks>
    /// Сценарий намеренно не выставляет Completed и NextStageAllowed сам. Он только собирает evidence,
    /// сохраняет его для диагностики и возвращает решение, можно ли выполнять подтверждение отдельной командой.
    /// </remarks>
    public class CheckT1CompletionUseCase
    {
        /// <summary>
        /// Инициализирует сценарий зависимостями состояния, evidence-gateway и evidence-storage.
        /// </summary>
        /// <param name="stageStateRepository">Repository явного состояния этапа.</param>
        /// <param name="completionGateway">Gateway получения внешних признаков завершения.</param>
        /// <param name="evidenceRepository">Repository сохранения evidence-снимков.</param>
        /// <remarks>Use case не создает SQL и API-адаптеры самостоятельно.</remarks>
        public CheckT1CompletionUseCase(
            IKonturStageStateRepository stageStateRepository,
            IKonturStageCompletionGateway completionGateway,
            IKonturStageCompletionEvidenceRepository evidenceRepository,
            SyncStageStateRefsUseCase syncStageStateRefsUseCase)
        {
            if (stageStateRepository == null)
            {
                throw new ArgumentNullException("stageStateRepository");
            }

            if (completionGateway == null)
            {
                throw new ArgumentNullException("completionGateway");
            }

            if (evidenceRepository == null)
            {
                throw new ArgumentNullException("evidenceRepository");
            }

            if (syncStageStateRefsUseCase == null)
            {
                throw new ArgumentNullException("syncStageStateRefsUseCase");
            }

            StageStateRepository = stageStateRepository;
            CompletionGateway = completionGateway;
            EvidenceRepository = evidenceRepository;
            SyncStageStateRefsUseCase = syncStageStateRefsUseCase;
        }

        /// <summary>
        /// Получает repository явного состояния этапа.
        /// </summary>
        public IKonturStageStateRepository StageStateRepository { get; private set; }

        /// <summary>
        /// Получает gateway внешних признаков завершения этапа.
        /// </summary>
        public IKonturStageCompletionGateway CompletionGateway { get; private set; }

        /// <summary>
        /// Получает repository хранения evidence-снимков.
        /// </summary>
        public IKonturStageCompletionEvidenceRepository EvidenceRepository { get; private set; }

        /// <summary>
        /// Получает сценарий синхронизации refs в явное состояние этапа.
        /// </summary>
        public SyncStageStateRefsUseCase SyncStageStateRefsUseCase { get; private set; }

        /// <summary>
        /// Проверяет T1 на наличие достаточного evidence для бизнес-завершения.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа в ТИС.</param>
        /// <param name="stageCode">Код этапа T1_INITIAL или T1_DRAFT.</param>
        /// <returns>Результат проверки с evidence и диагностическим решением.</returns>
        /// <remarks>
        /// Положительный результат означает только возможность подтверждения отдельным use case.
        /// Само состояние этапа этот метод не переводит в Completed.
        /// </remarks>
        public KonturStageCompletionCheckResult Execute(long timelineId, string stageCode)
        {
            var normalizedStageCode = NormalizeStageCode(stageCode);
            var titleCode = StageToTitle(normalizedStageCode);

            if (timelineId <= 0)
            {
                return BuildResult(false, false, timelineId, normalizedStageCode, titleCode, "InvalidTimelineId", "TimelineId должен быть положительным.", null);
            }

            if (!string.Equals(titleCode, "T1", StringComparison.OrdinalIgnoreCase))
            {
                return BuildResult(false, false, timelineId, normalizedStageCode, titleCode, "StageIsNotT1", "Проверка CheckT1CompletionUseCase допускает только этапы T1_INITIAL и T1_DRAFT.", null);
            }

            var syncedState = SyncStageStateRefsUseCase.Execute(timelineId, normalizedStageCode);
            var state = syncedState ?? StageStateRepository.Get(timelineId, normalizedStageCode);
            if (state == null)
            {
                return BuildResult(false, false, timelineId, normalizedStageCode, titleCode, "StageStateMissing", "Явное состояние T1 не найдено. Сначала нужно собрать, подписать и отправить T1.", null);
            }

            if (!state.Sent)
            {
                return BuildResult(false, false, timelineId, normalizedStageCode, titleCode, "T1NotSent", "T1 еще не отправлен оператору. Проверка завершения возможна только после Sent=true.", null);
            }

            if (state.Completed && state.NextStageAllowed)
            {
                return BuildResult(true, true, timelineId, normalizedStageCode, titleCode, "AlreadyCompleted", "T1 уже подтвержден как завершенный и разрешает следующий этап.", null);
            }

            var evidence = CompletionGateway.ReadEvidence(timelineId, normalizedStageCode);
            if (evidence == null)
            {
                return BuildResult(false, false, timelineId, normalizedStageCode, titleCode, "EvidenceMissing", "Внешние признаки завершения T1 не получены.", null);
            }

            evidence.TimelineId = timelineId;
            evidence.StageCode = normalizedStageCode;
            evidence.TitleCode = titleCode;
            if (evidence.CheckedAt == DateTime.MinValue)
            {
                evidence.CheckedAt = DateTime.Now;
            }

            EvidenceRepository.Save(evidence);
            return EvaluateEvidence(timelineId, normalizedStageCode, titleCode, evidence);
        }

        /// <summary>
        /// Оценивает evidence по консервативному критерию завершения T1.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код этапа.</param>
        /// <param name="titleCode">Код титула.</param>
        /// <param name="evidence">Evidence-снимок.</param>
        /// <returns>Результат проверки завершения T1.</returns>
        /// <remarks>
        /// На первом этапе автоматическое подтверждение допускается только при наличии явного внешнего действия,
        /// которое указывает на готовность следующего участника или следующего титула. Одного TransportationId недостаточно.
        /// </remarks>
        private KonturStageCompletionCheckResult EvaluateEvidence(long timelineId, string stageCode, string titleCode, KonturStageCompletionEvidence evidence)
        {
            if (string.IsNullOrEmpty(evidence.TransportationId))
            {
                return BuildResult(true, false, timelineId, stageCode, titleCode, "TransportationIdMissing", "TransportationId отсутствует. T1 нельзя считать завершенным.", evidence);
            }

            if (string.IsNullOrEmpty(evidence.TitleId))
            {
                return BuildResult(true, false, timelineId, stageCode, titleCode, "TitleIdMissing", "TitleId отсутствует. T1 нельзя считать завершенным.", evidence);
            }

            if (evidence.HasActiveError)
            {
                return BuildResult(true, false, timelineId, stageCode, titleCode, "ActiveOperatorError", "В evidence есть признак активной ошибки оператора.", evidence);
            }

            if (evidence.IsDraft)
            {
                return BuildResult(true, false, timelineId, stageCode, titleCode, "StillDraft", "Внешний объект выглядит как черновик. T1 нельзя считать завершенным.", evidence);
            }

            if (!IsSuccessHttpStatus(evidence.HttpStatus))
            {
                return BuildResult(true, false, timelineId, stageCode, titleCode, "HttpStatusNotSuccessful", "Последний HTTP-статус не доказывает успешную обработку T1.", evidence);
            }

            if (!HasNextTitleActionEvidence(evidence))
            {
                return BuildResult(true, false, timelineId, stageCode, titleCode, "NextTitleActionMissing", "Нет явного внешнего признака, что Контур разрешил следующий ответный титул T2.", evidence);
            }

            return BuildResult(true, true, timelineId, stageCode, titleCode, "T1CompletionConfirmed", "Evidence достаточно для подтверждения завершения T1 и открытия T2.", evidence);
        }

        /// <summary>
        /// Проверяет, является ли HTTP-статус успешным.
        /// </summary>
        /// <param name="httpStatus">HTTP-статус evidence.</param>
        /// <returns>True, если статус находится в диапазоне 2xx.</returns>
        /// <remarks>Успешный HTTP-статус является обязательным, но недостаточным признаком завершения T1.</remarks>
        private bool IsSuccessHttpStatus(int? httpStatus)
        {
            return httpStatus.HasValue && httpStatus.Value >= 200 && httpStatus.Value < 300;
        }

        /// <summary>
        /// Проверяет наличие признака готовности следующего титула.
        /// </summary>
        /// <param name="evidence">Evidence-снимок.</param>
        /// <returns>True, если evidence содержит явный код следующего действия или статус готовности T2.</returns>
        /// <remarks>
        /// Список кодов намеренно вынесен в отдельный метод. После реального прогона его нужно заменить точными кодами Контур.
        /// </remarks>
        private bool HasNextTitleActionEvidence(KonturStageCompletionEvidence evidence)
        {
            return IsKnownNextAction(evidence.ExternalActionCode) ||
                   IsKnownNextAction(evidence.ExternalTitleStatus) ||
                   IsKnownNextAction(evidence.ExternalDocumentStatus);
        }

        /// <summary>
        /// Проверяет, похож ли внешний код или статус на разрешение T2.
        /// </summary>
        /// <param name="value">Внешний код действия или статус.</param>
        /// <returns>True, если значение входит в предварительный allow-list признаков T2.</returns>
        /// <remarks>Allow-list консервативный и должен быть уточнен по raw response после контрольного прогона.</remarks>
        private bool IsKnownNextAction(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            var normalized = value.Trim().ToUpperInvariant();
            return normalized == "T2_ALLOWED" ||
                   normalized == "T2_AVAILABLE" ||
                   normalized == "RECIPIENT_TITLE_ALLOWED" ||
                   normalized == "NEXT_TITLE_ALLOWED" ||
                   normalized == "WAITING_RECIPIENT_TITLE" ||
                   normalized == "WAITING_T2";
        }

        /// <summary>
        /// Формирует результат проверки завершения.
        /// </summary>
        /// <param name="isSuccess">Признак успешного выполнения проверки.</param>
        /// <param name="canConfirmCompletion">Признак достаточности evidence.</param>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код этапа.</param>
        /// <param name="titleCode">Код титула.</param>
        /// <param name="reasonCode">Код причины решения.</param>
        /// <param name="message">Диагностическое сообщение.</param>
        /// <param name="evidence">Evidence-снимок.</param>
        /// <returns>Результат проверки.</returns>
        /// <remarks>Единый конструктор результата сохраняет одинаковый формат диагностики.</remarks>
        private KonturStageCompletionCheckResult BuildResult(
            bool isSuccess,
            bool canConfirmCompletion,
            long timelineId,
            string stageCode,
            string titleCode,
            string reasonCode,
            string message,
            KonturStageCompletionEvidence evidence)
        {
            return new KonturStageCompletionCheckResult
            {
                IsSuccess = isSuccess,
                CanConfirmCompletion = canConfirmCompletion,
                TimelineId = timelineId,
                StageCode = stageCode,
                TitleCode = titleCode,
                ReasonCode = reasonCode,
                Message = message,
                Evidence = evidence
            };
        }

        /// <summary>
        /// Нормализует код этапа.
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
        /// <remarks>Для проверки T1 поддерживаются T1_INITIAL и T1_DRAFT.</remarks>
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
    }
}
