/*
  ФАЙЛ: ImportStageSignatureUseCase.cs
  НАЗНАЧЕНИЕ: Сценарий импорта и проверки detached-подписи титула этапа Контур ЭТрН.
  Отделяет шаг подписи от UI, сборки XML, отправки оператору и деталей SQL-хранения состояния.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  29.05.2026 - Первичное создание сценария импорта подписи титула этапа в реконструкционном слое.
*/

using System;
using Tis.KonturIntegration.Models;
using Tis.KonturIntegration.Storage;

namespace Tis.KonturIntegration.Integration
{
    /// <summary>
    /// Выполняет прикладной сценарий импорта detached-подписи и фиксации явного состояния этапа.
    /// Используется как add-only граница между будущим тонким UI, сервисом подписи и состоянием этапа.
    /// </summary>
    public class ImportStageSignatureUseCase
    {
        /// <summary>
        /// Инициализирует сценарий зависимостями проверки подписи, хранения артефактов и состояния этапа.
        /// </summary>
        /// <param name="signatureService">Порт получения и проверки detached-подписи.</param>
        /// <param name="artifactRepository">Repository XML/SGN-артефактов титулов.</param>
        /// <param name="stageStateRepository">Repository явного состояния этапа.</param>
        /// <remarks>Все зависимости передаются снаружи, чтобы сценарий не создавал SQL-адаптеры и сервисы подписи внутри себя.</remarks>
        public ImportStageSignatureUseCase(
            IKonturSignatureService signatureService,
            KonturTitleArtifactRepository artifactRepository,
            IKonturStageStateRepository stageStateRepository)
        {
            if (signatureService == null)
            {
                throw new ArgumentNullException("signatureService");
            }

            if (artifactRepository == null)
            {
                throw new ArgumentNullException("artifactRepository");
            }

            if (stageStateRepository == null)
            {
                throw new ArgumentNullException("stageStateRepository");
            }

            SignatureService = signatureService;
            ArtifactRepository = artifactRepository;
            StageStateRepository = stageStateRepository;
        }

        /// <summary>
        /// Получает порт получения и проверки detached-подписи.
        /// </summary>
        public IKonturSignatureService SignatureService { get; private set; }

        /// <summary>
        /// Получает repository хранения XML/SGN-артефактов.
        /// </summary>
        public KonturTitleArtifactRepository ArtifactRepository { get; private set; }

        /// <summary>
        /// Получает repository явного состояния этапа.
        /// </summary>
        public IKonturStageStateRepository StageStateRepository { get; private set; }

        /// <summary>
        /// Импортирует detached-подпись выбранного этапа, проверяет ее относительно XML и фиксирует состояние этапа.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа в ТИС.</param>
        /// <param name="stageCode">Код этапа UI или сценария, например T1_INITIAL, T2, T3 или T4.</param>
        /// <param name="signaturePath">Необязательный путь к пользовательскому .sgn-файлу.</param>
        /// <returns>Результат получения и проверки detached-подписи.</returns>
        /// <remarks>Сценарий не отправляет титул оператору и не подключается к UI автоматически.</remarks>
        public KonturSignatureResult Execute(long timelineId, string stageCode, string signaturePath)
        {
            var normalizedStageCode = NormalizeStageCode(stageCode);
            var titleCode = StageToTitle(normalizedStageCode);

            var state = StageStateRepository.Get(timelineId, normalizedStageCode);
            if (state == null || !state.XmlBuilt)
            {
                SaveFailedState(timelineId, normalizedStageCode, titleCode, "StageXmlStateMissing", "Явное состояние этапа не подтверждает готовность XML титула.", false);
                return Fail("StageXmlStateMissing");
            }

            if (state.Sent || state.Completed)
            {
                SaveFailedState(timelineId, normalizedStageCode, titleCode, "StageAlreadySent", "Подпись нельзя импортировать после отправки этапа без новой сборки XML.", true);
                return Fail("StageAlreadySent");
            }

            var artifact = ArtifactRepository.GetLatest(timelineId, titleCode);
            if (artifact == null || !artifact.HasXml)
            {
                SaveFailedState(timelineId, normalizedStageCode, titleCode, "TitleXmlMissing", "XML титула не найден. Сначала нужно выполнить сборку XML этапа.", false);
                return Fail("TitleXmlMissing");
            }

            var signatureResult = SignatureService.Resolve(timelineId, titleCode, artifact.TitleXml, signaturePath);
            if (signatureResult == null)
            {
                SaveFailedState(timelineId, normalizedStageCode, titleCode, "SignatureReturnedNull", "Сервис подписи вернул пустой результат.", false);
                return Fail("SignatureReturnedNull");
            }

            if (!signatureResult.IsSuccess || signatureResult.SignatureBytes == null || signatureResult.SignatureBytes.Length == 0)
            {
                SaveFailedState(timelineId, normalizedStageCode, titleCode, "SignatureImportFailed", signatureResult.Message, false);
                return signatureResult;
            }

            if (!artifact.HasSignature || !string.IsNullOrEmpty(signaturePath))
            {
                // Подпись сохраняется рядом с XML-артефактом, чтобы последующая отправка не зависела от файлового выбора UI.
                ArtifactRepository.SaveSignature(
                    timelineId,
                    titleCode,
                    signatureResult.SignatureFileName,
                    signatureResult.SignatureBytes,
                    signatureResult.Thumbprint,
                    signatureResult.SignerRole,
                    DateTime.Now);
            }

            SaveSignatureImportedState(timelineId, normalizedStageCode, titleCode, signatureResult.Message);
            return signatureResult;
        }

        /// <summary>
        /// Сохраняет состояние успешного импорта подписи этапа.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Нормализованный код этапа.</param>
        /// <param name="titleCode">Код титула этапа.</param>
        /// <param name="operatorStatus">Диагностическое сообщение проверки подписи.</param>
        /// <remarks>Фиксация подписи не означает отправку титула оператору и не переводит этап в Completed.</remarks>
        private void SaveSignatureImportedState(long timelineId, string stageCode, string titleCode, string operatorStatus)
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
            state.Sent = false;
            state.Completed = false;
            state.NextStageAllowed = false;
            state.LastOperatorStatus = string.IsNullOrEmpty(operatorStatus) ? "SignatureImported" : operatorStatus;
            state.LastErrorCode = string.Empty;
            state.LastErrorMessage = string.Empty;

            StageStateRepository.Save(state);
        }

        /// <summary>
        /// Сохраняет состояние неуспешного импорта подписи этапа.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Нормализованный код этапа.</param>
        /// <param name="titleCode">Код титула этапа.</param>
        /// <param name="errorCode">Код ошибки сценария.</param>
        /// <param name="errorMessage">Диагностическое сообщение ошибки.</param>
        /// <param name="preserveSentState">Признак сохранения состояния отправленного этапа без отката флагов отправки.</param>
        /// <remarks>Ошибка подписи фиксируется явно, чтобы UI не восстанавливал причину только по файлам или raw-log.</remarks>
        private void SaveFailedState(long timelineId, string stageCode, string titleCode, string errorCode, string errorMessage, bool preserveSentState)
        {
            var current = StageStateRepository.Get(timelineId, stageCode);
            var state = current ?? new KonturStageState
            {
                TimelineId = timelineId,
                StageCode = stageCode,
                TitleCode = titleCode
            };

            state.TitleCode = titleCode;
            if (!preserveSentState)
            {
                // Неверная подпись делает этап неподготовленным к отправке, но не отменяет уже сформированный XML.
                state.SignatureImported = false;
                state.Sent = false;
                state.Completed = false;
                state.NextStageAllowed = false;
            }

            state.LastErrorCode = errorCode;
            state.LastErrorMessage = string.IsNullOrEmpty(errorMessage) ? errorCode : errorMessage;

            StageStateRepository.Save(state);
        }

        /// <summary>
        /// Формирует неуспешный результат импорта подписи.
        /// </summary>
        /// <param name="message">Техническое сообщение ошибки.</param>
        /// <returns>Неуспешный результат сервиса подписи.</returns>
        /// <remarks>Метод используется для ошибок, возникших до вызова сервиса подписи или при пустом ответе сервиса.</remarks>
        private KonturSignatureResult Fail(string message)
        {
            return new KonturSignatureResult
            {
                IsSuccess = false,
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
        /// Преобразует код этапа в код титула ЭТрН.
        /// </summary>
        /// <param name="stageCode">Нормализованный код этапа.</param>
        /// <returns>Код титула T1/T2/T3/T4 или UNKNOWN.</returns>
        /// <remarks>Разделение stage-code и title-code нужно, потому что T1_INITIAL не равен завершенному жизненному циклу T1.</remarks>
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
