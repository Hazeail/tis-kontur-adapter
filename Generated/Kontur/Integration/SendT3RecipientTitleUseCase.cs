/*
  ФАЙЛ: SendT3RecipientTitleUseCase.cs
  НАЗНАЧЕНИЕ: Сценарий отправки ответного титула T3 через явный gateway-порт Контур.
  Отделяет бизнес-шаг T3 от UI, сборки XML, импорта подписи и крупного KonturAdapter.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  29.05.2026 - Первичное создание сценария отправки ответного титула T3 в реконструкционном слое.
  29.05.2026 - После успешной отправки в состоянии этапа сохраняются TransportationId и TitleId.
*/

using System;
using Tis.KonturIntegration.Models;
using Tis.KonturIntegration.Storage;

namespace Tis.KonturIntegration.Integration
{
    /// <summary>
    /// Выполняет прикладной сценарий отправки ответного титула T3 и фиксирует явное состояние этапа.
    /// </summary>
    /// <remarks>Сценарий требует подтвержденный предыдущий этап и не переводит T3 в завершенное состояние автоматически.</remarks>
    public class SendT3RecipientTitleUseCase
    {
        /// <summary>
        /// Код этапа T3 в явном состоянии процесса.
        /// </summary>
        private const string StageCode = "T3";

        /// <summary>
        /// Код титула T3 в ЭТрН.
        /// </summary>
        private const string TitleCode = "T3";

        /// <summary>
        /// Инициализирует сценарий зависимостями gateway, policy переходов, транспортного контекста, хранения артефактов, рабочего файлового слоя и состояния этапа.
        /// </summary>
        /// <param name="recipientTitleGateway">Gateway-порт отправки ответных титулов.</param>
        /// <param name="transitionPolicy">Policy проверки готовности этапа.</param>
        /// <param name="transportContextGateway">Gateway-порт чтения транспортного контекста.</param>
        /// <param name="artifactRepository">Repository XML/SGN-артефактов титулов.</param>
        /// <param name="workspaceService">Сервис рабочего файлового слоя XML/SGN-артефактов.</param>
        /// <param name="stageStateRepository">Repository явного состояния этапа.</param>
        /// <remarks>Все зависимости передаются снаружи, чтобы use case оставался прикладной границей без создания adapter/storage внутри себя.</remarks>
        public SendT3RecipientTitleUseCase(
            IKonturRecipientTitleGateway recipientTitleGateway,
            KonturStageTransitionPolicy transitionPolicy,
            IKonturTransportContextGateway transportContextGateway,
            KonturTitleArtifactRepository artifactRepository,
            KonturStageArtifactWorkspaceService workspaceService,
            IKonturStageStateRepository stageStateRepository)
        {
            if (recipientTitleGateway == null)
            {
                throw new ArgumentNullException("recipientTitleGateway");
            }

            if (transitionPolicy == null)
            {
                throw new ArgumentNullException("transitionPolicy");
            }

            if (transportContextGateway == null)
            {
                throw new ArgumentNullException("transportContextGateway");
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

            RecipientTitleGateway = recipientTitleGateway;
            TransitionPolicy = transitionPolicy;
            TransportContextGateway = transportContextGateway;
            ArtifactRepository = artifactRepository;
            WorkspaceService = workspaceService;
            StageStateRepository = stageStateRepository;
        }

        /// <summary>
        /// Получает gateway-порт отправки ответных титулов.
        /// </summary>
        public IKonturRecipientTitleGateway RecipientTitleGateway { get; private set; }

        /// <summary>
        /// Получает policy проверки готовности этапа.
        /// </summary>
        public KonturStageTransitionPolicy TransitionPolicy { get; private set; }

        /// <summary>
        /// Получает gateway-порт чтения транспортного контекста.
        /// </summary>
        public IKonturTransportContextGateway TransportContextGateway { get; private set; }

        /// <summary>
        /// Получает repository хранения XML/SGN-артефактов.
        /// </summary>
        public KonturTitleArtifactRepository ArtifactRepository { get; private set; }

        /// <summary>
        /// Получает сервис рабочего файлового слоя XML/SGN-артефактов.
        /// </summary>
        public KonturStageArtifactWorkspaceService WorkspaceService { get; private set; }

        /// <summary>
        /// Получает repository явного состояния этапа.
        /// </summary>
        public IKonturStageStateRepository StageStateRepository { get; private set; }

        /// <summary>
        /// Отправляет ответный титул T3 в существующий операторный документооборот и фиксирует результат в явном состоянии этапа.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа в ТИС.</param>
        /// <param name="senderBoxId">DiadocBoxId отправителя; может быть пустым, если gateway использует fallback-настройку.</param>
        /// <returns>Результат отправки ответного титула T3.</returns>
        /// <remarks>Успешная отправка фиксирует только `Sent`, но не переводит этап в `Completed` без отдельного подтверждения оператора.</remarks>
        public KonturStageExecutionResult Execute(long timelineId, string senderBoxId)
        {
            var transitionDecision = TransitionPolicy.CanStart(timelineId, StageCode);
            if (transitionDecision == null || !transitionDecision.IsAllowed)
            {
                SaveFailedState(
                    timelineId,
                    transitionDecision == null ? "TransitionDecisionMissing" : transitionDecision.ReasonCode,
                    transitionDecision == null ? "Policy перехода не вернула решение для T3." : transitionDecision.Message);
                return Fail(timelineId, transitionDecision == null ? "TransitionDecisionMissing" : transitionDecision.ReasonCode);
            }

            var artifact = ArtifactRepository.GetLatest(timelineId, TitleCode);
            if (artifact == null || !artifact.HasXml)
            {
                SaveFailedState(timelineId, "TitleXmlMissing", "XML титула T3 не найден. Сначала нужно выполнить сборку XML этапа.");
                return Fail(timelineId, "TitleXmlMissing");
            }

            if (!artifact.HasSignature)
            {
                SaveFailedState(timelineId, "SignatureMissing", "Подпись титула T3 не импортирована. Сначала нужно выполнить шаг импорта подписи.");
                return Fail(timelineId, "SignatureMissing");
            }

            var transportationId = TransportContextGateway.GetTransportationId(timelineId);
            if (string.IsNullOrEmpty(transportationId))
            {
                SaveFailedState(timelineId, "TransportationIdMissing", "TransportationId не найден. T3 нельзя отправлять без связи с существующей перевозкой.");
                return Fail(timelineId, "TransportationIdMissing");
            }

            var xmlPath = WorkspaceService.SaveCurrentXml(timelineId, TitleCode, artifact.TitleXml);
            var signaturePath = WorkspaceService.SaveCurrentSignature(timelineId, TitleCode, artifact.TitleSgn);
            var result = RecipientTitleGateway.SendRecipientTitle(TitleCode, timelineId, xmlPath, signaturePath, senderBoxId);

            if (result == null)
            {
                SaveFailedState(timelineId, "SendReturnedNull", "Gateway отправки T3 вернул пустой результат.");
                return Fail(timelineId, "SendReturnedNull");
            }

            if (!result.IsSuccess)
            {
                SaveFailedState(timelineId, "SendT3Failed", result.Message);
                return result;
            }

            SaveSentState(timelineId, result);
            return result;
        }

        /// <summary>
        /// Сохраняет состояние успешной отправки ответного титула T3.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="result">Результат gateway-вызова с внешними идентификаторами этапа.</param>
        /// <remarks>Метод намеренно не выставляет Completed, потому что отправка T3 не равна подтвержденному завершению этапа оператором.</remarks>
        private void SaveSentState(long timelineId, KonturStageExecutionResult result)
        {
            var current = StageStateRepository.Get(timelineId, StageCode);
            var state = current ?? new KonturStageState
            {
                TimelineId = timelineId,
                StageCode = StageCode,
                TitleCode = TitleCode
            };

            state.TitleCode = TitleCode;
            state.XmlBuilt = true;
            state.SignatureImported = true;
            state.Sent = true;
            state.Completed = false;
            state.NextStageAllowed = false;
            state.TransportationId = result == null ? string.Empty : result.TransportationId;
            state.TitleId = result == null ? string.Empty : result.TitleId;
            state.LastOperatorStatus = result == null || string.IsNullOrEmpty(result.Message) ? "T3Sent" : result.Message;
            state.LastErrorCode = string.Empty;
            state.LastErrorMessage = string.Empty;

            StageStateRepository.Save(state);
        }

        /// <summary>
        /// Сохраняет состояние неуспешной отправки ответного титула T3.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="errorCode">Код ошибки сценария.</param>
        /// <param name="errorMessage">Диагностическое сообщение ошибки.</param>
        /// <remarks>Ошибка отправки не сбрасывает XML и подпись, но снимает признак отправки и не разрешает следующий этап.</remarks>
        private void SaveFailedState(long timelineId, string errorCode, string errorMessage)
        {
            var current = StageStateRepository.Get(timelineId, StageCode);
            var state = current ?? new KonturStageState
            {
                TimelineId = timelineId,
                StageCode = StageCode,
                TitleCode = TitleCode
            };

            state.TitleCode = TitleCode;
            state.Sent = false;
            state.Completed = false;
            state.NextStageAllowed = false;
            state.LastErrorCode = errorCode;
            state.LastErrorMessage = string.IsNullOrEmpty(errorMessage) ? errorCode : errorMessage;

            StageStateRepository.Save(state);
        }

        /// <summary>
        /// Формирует неуспешный результат отправки ответного титула T3.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="message">Техническое сообщение ошибки.</param>
        /// <returns>Неуспешный результат T3-сценария.</returns>
        /// <remarks>Метод используется для ошибок, возникших до фактического gateway-вызова.</remarks>
        private KonturStageExecutionResult Fail(long timelineId, string message)
        {
            return new KonturStageExecutionResult
            {
                IsSuccess = false,
                StageCode = StageCode,
                TimelineId = timelineId,
                Message = message
            };
        }
    }
}
