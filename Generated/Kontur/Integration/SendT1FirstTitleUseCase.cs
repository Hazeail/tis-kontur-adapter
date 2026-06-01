/*
  ФАЙЛ: SendT1FirstTitleUseCase.cs
  НАЗНАЧЕНИЕ: Сценарий отправки первого титула T1 через явный gateway-порт Контур.
  Отделяет бизнес-шаг отправки T1 от UI, сборки XML, импорта подписи и крупного KonturAdapter.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  29.05.2026 - Первичное создание сценария отправки первого титула T1 в реконструкционном слое.
  29.05.2026 - После успешной отправки в состоянии этапа сохраняются TransportationId и TitleId.
*/

using System;
using Tis.KonturIntegration.Models;
using Tis.KonturIntegration.Storage;

namespace Tis.KonturIntegration.Integration
{
    /// <summary>
    /// Выполняет прикладной сценарий отправки первого титула T1 и фиксирует явное состояние этапа.
    /// </summary>
    /// <remarks>Сценарий не считает T1 завершенным сразу после операторного вызова и не разрешает следующий этап автоматически.</remarks>
    public class SendT1FirstTitleUseCase
    {
        /// <summary>
        /// Инициализирует сценарий зависимостями gateway, policy переходов, хранения артефактов, рабочего файлового слоя и состояния этапа.
        /// </summary>
        /// <param name="firstTitleGateway">Gateway-порт отправки первого титула.</param>
        /// <param name="transitionPolicy">Policy проверки готовности этапа.</param>
        /// <param name="artifactRepository">Repository XML-артефактов титулов.</param>
        /// <param name="workspaceService">Сервис рабочего файлового слоя XML-артефактов.</param>
        /// <param name="stageStateRepository">Repository явного состояния этапа.</param>
        /// <remarks>Все зависимости передаются снаружи, чтобы use case оставался прикладной границей без создания adapter/storage внутри себя.</remarks>
        public SendT1FirstTitleUseCase(
            IKonturFirstTitleGateway firstTitleGateway,
            KonturStageTransitionPolicy transitionPolicy,
            KonturTitleArtifactRepository artifactRepository,
            KonturStageArtifactWorkspaceService workspaceService,
            IKonturStageStateRepository stageStateRepository)
        {
            if (firstTitleGateway == null)
            {
                throw new ArgumentNullException("firstTitleGateway");
            }

            if (transitionPolicy == null)
            {
                throw new ArgumentNullException("transitionPolicy");
            }

            if (artifactRepository == null)
            {
                throw new ArgumentNullException("artifactRepository");
            }

            if (workspaceService == null)
            {
                throw new ArgumentNullException("workspaceService");
            }

            if (stageStateRepository == null)
            {
                throw new ArgumentNullException("stageStateRepository");
            }

            FirstTitleGateway = firstTitleGateway;
            TransitionPolicy = transitionPolicy;
            ArtifactRepository = artifactRepository;
            WorkspaceService = workspaceService;
            StageStateRepository = stageStateRepository;
        }

        /// <summary>
        /// Получает gateway-порт отправки первого титула.
        /// </summary>
        public IKonturFirstTitleGateway FirstTitleGateway { get; private set; }

        /// <summary>
        /// Получает policy проверки готовности этапа.
        /// </summary>
        public KonturStageTransitionPolicy TransitionPolicy { get; private set; }

        /// <summary>
        /// Получает repository хранения XML-артефактов.
        /// </summary>
        public KonturTitleArtifactRepository ArtifactRepository { get; private set; }

        /// <summary>
        /// Получает сервис рабочего файлового слоя XML-артефактов.
        /// </summary>
        public KonturStageArtifactWorkspaceService WorkspaceService { get; private set; }

        /// <summary>
        /// Получает repository явного состояния этапа.
        /// </summary>
        public IKonturStageStateRepository StageStateRepository { get; private set; }

        /// <summary>
        /// Отправляет первый титул T1 в выбранном режиме и фиксирует результат в явном состоянии этапа.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа в ТИС.</param>
        /// <param name="stageCode">Код этапа T1_INITIAL или T1_DRAFT.</param>
        /// <param name="senderBoxId">DiadocBoxId отправителя; может быть пустым, если gateway использует fallback-настройку.</param>
        /// <returns>Результат отправки первого титула.</returns>
        /// <remarks>Успешная отправка фиксирует только `Sent`, но не переводит T1 в `Completed` и не разрешает следующий этап автоматически.</remarks>
        public KonturT1ExecutionResult Execute(long timelineId, string stageCode, string senderBoxId)
        {
            var normalizedStageCode = NormalizeStageCode(stageCode);
            var titleCode = StageToTitle(normalizedStageCode);
            if (!string.Equals(titleCode, "T1", StringComparison.OrdinalIgnoreCase))
            {
                SaveFailedState(timelineId, normalizedStageCode, titleCode, "StageIsNotT1", "Сценарий SendT1FirstTitleUseCase допускает только этапы T1_INITIAL и T1_DRAFT.");
                return Fail(timelineId, "StageIsNotT1");
            }

            var transitionDecision = TransitionPolicy.CanStart(timelineId, normalizedStageCode);
            if (transitionDecision == null || !transitionDecision.IsAllowed)
            {
                SaveFailedState(
                    timelineId,
                    normalizedStageCode,
                    titleCode,
                    transitionDecision == null ? "TransitionDecisionMissing" : transitionDecision.ReasonCode,
                    transitionDecision == null ? "Policy перехода не вернула решение для T1." : transitionDecision.Message);
                return Fail(timelineId, transitionDecision == null ? "TransitionDecisionMissing" : transitionDecision.ReasonCode);
            }

            var artifact = ArtifactRepository.GetLatest(timelineId, titleCode);
            if (artifact == null || !artifact.HasXml)
            {
                SaveFailedState(timelineId, normalizedStageCode, titleCode, "TitleXmlMissing", "XML титула T1 не найден. Сначала нужно выполнить сборку XML этапа.");
                return Fail(timelineId, "TitleXmlMissing");
            }

            var xmlPath = WorkspaceService.SaveCurrentXml(timelineId, titleCode, artifact.TitleXml);
            var result = IsDraftStage(normalizedStageCode)
                ? FirstTitleGateway.SendDraft(timelineId, xmlPath, senderBoxId)
                : FirstTitleGateway.SendInitial(timelineId, xmlPath, senderBoxId);

            if (result == null)
            {
                SaveFailedState(timelineId, normalizedStageCode, titleCode, "SendReturnedNull", "Gateway отправки T1 вернул пустой результат.");
                return Fail(timelineId, "SendReturnedNull");
            }

            if (!result.IsSuccess)
            {
                SaveFailedState(timelineId, normalizedStageCode, titleCode, "SendT1Failed", result.Message);
                return result;
            }

            SaveSentState(timelineId, normalizedStageCode, titleCode, result);
            return result;
        }

        /// <summary>
        /// Сохраняет состояние успешной отправки первого титула.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Нормализованный код этапа.</param>
        /// <param name="titleCode">Код титула.</param>
        /// <param name="result">Результат gateway-вызова с внешними идентификаторами этапа.</param>
        /// <remarks>Метод намеренно не выставляет Completed, потому что отправка T1 не равна бизнес-завершению первого титула.</remarks>
        private void SaveSentState(long timelineId, string stageCode, string titleCode, KonturT1ExecutionResult result)
        {
            var current = StageStateRepository.Get(timelineId, stageCode);
            var state = current ?? new KonturStageState
            {
                TimelineId = timelineId,
                StageCode = stageCode,
                TitleCode = titleCode
            };

            state.TitleCode = titleCode;
            state.XmlBuilt = true;
            state.SignatureImported = true;
            state.Sent = true;
            state.Completed = false;
            state.NextStageAllowed = false;
            state.TransportationId = result == null ? string.Empty : result.TransportationId;
            state.TitleId = result == null ? string.Empty : result.TitleId;
            state.LastOperatorStatus = result == null || string.IsNullOrEmpty(result.Message) ? "T1Sent" : result.Message;
            state.LastErrorCode = string.Empty;
            state.LastErrorMessage = string.Empty;

            StageStateRepository.Save(state);
        }

        /// <summary>
        /// Сохраняет состояние неуспешной отправки первого титула.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Нормализованный код этапа.</param>
        /// <param name="titleCode">Код титула.</param>
        /// <param name="errorCode">Код ошибки сценария.</param>
        /// <param name="errorMessage">Диагностическое сообщение ошибки.</param>
        /// <remarks>Ошибка отправки не сбрасывает XML, но снимает признак отправки и не разрешает следующий этап.</remarks>
        private void SaveFailedState(long timelineId, string stageCode, string titleCode, string errorCode, string errorMessage)
        {
            var current = StageStateRepository.Get(timelineId, stageCode);
            var state = current ?? new KonturStageState
            {
                TimelineId = timelineId,
                StageCode = stageCode,
                TitleCode = titleCode
            };

            state.TitleCode = titleCode;
            state.Sent = false;
            state.Completed = false;
            state.NextStageAllowed = false;
            state.LastErrorCode = errorCode;
            state.LastErrorMessage = string.IsNullOrEmpty(errorMessage) ? errorCode : errorMessage;

            StageStateRepository.Save(state);
        }

        /// <summary>
        /// Формирует неуспешный результат отправки первого титула.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="message">Техническое сообщение ошибки.</param>
        /// <returns>Неуспешный результат T1-сценария.</returns>
        /// <remarks>Метод используется для ошибок, возникших до gateway-вызова или при пустом ответе gateway.</remarks>
        private KonturT1ExecutionResult Fail(long timelineId, string message)
        {
            return new KonturT1ExecutionResult
            {
                IsSuccess = false,
                TimelineId = timelineId,
                Message = message
            };
        }

        /// <summary>
        /// Нормализует код этапа для ключей состояния.
        /// </summary>
        /// <param name="stageCode">Исходный код этапа.</param>
        /// <returns>Код этапа в верхнем регистре или пустая строка.</returns>
        /// <remarks>Нормализация синхронизирована с repository состояния этапа.</remarks>
        private string NormalizeStageCode(string stageCode)
        {
            return string.IsNullOrEmpty(stageCode) ? string.Empty : stageCode.Trim().ToUpperInvariant();
        }

        /// <summary>
        /// Определяет, нужно ли отправлять T1 через draft-сценарий.
        /// </summary>
        /// <param name="stageCode">Нормализованный код этапа.</param>
        /// <returns>Истина, если этап является T1_DRAFT.</returns>
        /// <remarks>Выбор режима остается явным на уровне stage-code, а не прячется внутри adapter.</remarks>
        private bool IsDraftStage(string stageCode)
        {
            return string.Equals(stageCode, "T1_DRAFT", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Преобразует код этапа в код титула ЭТрН.
        /// </summary>
        /// <param name="stageCode">Нормализованный код этапа.</param>
        /// <returns>Код титула T1 или UNKNOWN.</returns>
        /// <remarks>Сценарий первого титула не должен принимать T2/T3/T4 как варианты той же отправки.</remarks>
        private string StageToTitle(string stageCode)
        {
            if (string.Equals(stageCode, "T1_INITIAL", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stageCode, "T1_DRAFT", StringComparison.OrdinalIgnoreCase))
            {
                return "T1";
            }

            return "UNKNOWN";
        }
    }
}
