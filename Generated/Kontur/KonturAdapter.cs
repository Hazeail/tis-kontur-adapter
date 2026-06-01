/*
  ФАЙЛ: KonturAdapter.cs
  НАЗНАЧЕНИЕ: Адаптер операторного слоя Контур для EPD-ядра ТИС.
  Содержит сценарии запуска T1 и общую инфраструктуру фиксации refs/raw-логов.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  05.05.2026 - Первичное создание каркаса адаптера для ветки Контур + Danaflex.
  07.05.2026 - Разделены сценарии первичной отправки T1 и отправки T1 через draft.
  08.05.2026 - Добавлена предвалидация стадии timeline перед запуском draft-ветки T1.
  08.05.2026 - Добавлен сценарий отправки T2 как ответного титула.
  12.05.2026 - Добавлен fallback подписи T2 из EpdRepo и локальная верификация подписи перед отправкой.
  12.05.2026 - Добавлены перегрузки с явным senderBoxId для ролевой маршрутизации без ручных переключений.
  12.05.2026 - Добавлено разделение raw-логов по этапу и попытке (request/response + runId).
  13.05.2026 - Убрана маркировка этапа из Direction для совместимости с ограничением длины поля в SQL.
  13.05.2026 - Добавлен сценарий отправки T3 как ответного титула грузополучателя.
  13.05.2026 - Добавлена отправка T3 из внутреннего артефакта без пользовательских XML/SGN-файлов.
  13.05.2026 - Добавлен сценарий отправки T4 как ответного титула перевозчика.
  26.05.2026 - Fallback boxId переведен на stage-specific ключи вместо одного общего DanaflexSenderBoxId.
  28.05.2026 - Исправлен fallback подписи T2: при пустом пути используется sig2_detached, а не подпись T1.
  28.05.2026 - Добавлен fallback на sig2_detached при выборе устаревшего T2 .sgn-файла, если подпись из БД совпадает с текущим XML.
*/

using System;
using System.IO;
using TIS.EPD;
using Tis.KonturIntegration.Integration;
using KonturApiClient = Tis.KonturIntegration.KonturClient.KonturClient;
using Tis.KonturIntegration.Models;
using Tis.KonturIntegration.Storage;

namespace Tis.KonturIntegration.KonturAdapter
{
    /// <summary>
    /// Адаптер Контур для изоляции операторной логики от UI и базового EPD-контура.
    /// </summary>
    public class KonturAdapter
    {
        /// <summary>
        /// Инициализирует адаптер готовым клиентом оператора.
        /// </summary>
        /// <param name="client">Клиент транспортного взаимодействия с Kontur API.</param>
        /// <remarks>Адаптер не создает клиент сам, чтобы конфигурация задавалась извне.</remarks>
        public KonturAdapter(KonturApiClient client)
        {
            Client = client;
        }

        /// <summary>
        /// Получает клиент оператора, используемый адаптером.
        /// </summary>
        public KonturApiClient Client { get; private set; }

        /// <summary>
        /// Получает или задает репозиторий чтения операторных настроек.
        /// </summary>
        public KonturSettingsRepository SettingsRepository { get; set; }

        /// <summary>
        /// Получает или задает репозиторий хранения внешних идентификаторов.
        /// </summary>
        public KonturOperatorRefRepository OperatorRefRepository { get; set; }

        /// <summary>
        /// Получает или задает репозиторий диагностических raw-логов.
        /// </summary>
        public KonturRawLogRepository RawLogRepository { get; set; }

        /// <summary>
        /// Получает или задает репозиторий чтения текущего статуса документа в timeline.
        /// </summary>
        public KonturTimelineRepository TimelineRepository { get; set; }

        /// <summary>
        /// Выполняет предварительную проверку готовности адаптера к работе.
        /// </summary>
        /// <returns>Истина, если клиент настроен и адаптер может запускать операторные шаги.</returns>
        /// <remarks>Проверка нужна для безопасного старта сценария без ложных сетевых вызовов.</remarks>
        public bool IsReady()
        {
            return Client != null && Client.IsConfigured();
        }

        /// <summary>
        /// Выполняет сценарий первичной отправки T1 Danaflex через Контур.
        /// </summary>
        /// <param name="timelineId">Внутренний идентификатор документа в timeline ТИС.</param>
        /// <param name="xmlPath">Путь к XML титула T1.</param>
        /// <returns>Итог выполнения с признаками успеха и ключевыми идентификаторами.</returns>
        /// <remarks>Метод используется для старта нового документооборота в Контур.</remarks>
        public KonturT1ExecutionResult StartDanaflexT1Initial(long timelineId, string xmlPath)
        {
            return StartDanaflexT1Internal(timelineId, xmlPath, false, string.Empty);
        }

        /// <summary>
        /// Выполняет первичную отправку T1 с явным указанием boxId отправителя.
        /// </summary>
        /// <param name="timelineId">Внутренний идентификатор документа в timeline ТИС.</param>
        /// <param name="xmlPath">Путь к XML титула T1.</param>
        /// <param name="senderBoxId">DiadocBoxId отправителя для текущего этапа.</param>
        /// <returns>Итог выполнения с признаками успеха и ключевыми идентификаторами.</returns>
        /// <remarks>Метод используется в продуктовой ролевой маршрутизации, где boxId выбирается заранее.</remarks>
        public KonturT1ExecutionResult StartDanaflexT1Initial(long timelineId, string xmlPath, string senderBoxId)
        {
            return StartDanaflexT1Internal(timelineId, xmlPath, false, senderBoxId);
        }

        /// <summary>
        /// Выполняет сценарий отправки T1 через draft-ветку Контур.
        /// </summary>
        /// <param name="timelineId">Внутренний идентификатор документа в timeline ТИС.</param>
        /// <param name="xmlPath">Путь к XML титула T1.</param>
        /// <returns>Итог выполнения с признаками успеха и ключевыми идентификаторами.</returns>
        /// <remarks>
        /// Метод должен вызываться только при допустимом статусе перевозки.
        /// Предвалидация статуса выполняется до сетевого вызова.
        /// </remarks>
        public KonturT1ExecutionResult StartDanaflexT1Draft(long timelineId, string xmlPath)
        {
            return StartDanaflexT1Internal(timelineId, xmlPath, true, string.Empty);
        }

        /// <summary>
        /// Выполняет отправку T1 через draft с явным указанием boxId отправителя.
        /// </summary>
        /// <param name="timelineId">Внутренний идентификатор документа в timeline ТИС.</param>
        /// <param name="xmlPath">Путь к XML титула T1.</param>
        /// <param name="senderBoxId">DiadocBoxId отправителя для текущего этапа.</param>
        /// <returns>Итог выполнения с признаками успеха и ключевыми идентификаторами.</returns>
        /// <remarks>Метод используется в продуктовой ролевой маршрутизации, где boxId выбирается заранее.</remarks>
        public KonturT1ExecutionResult StartDanaflexT1Draft(long timelineId, string xmlPath, string senderBoxId)
        {
            return StartDanaflexT1Internal(timelineId, xmlPath, true, senderBoxId);
        }

        /// <summary>
        /// Выполняет сценарий отправки T2 как ответного титула.
        /// </summary>
        /// <param name="timelineId">Внутренний идентификатор документа в timeline ТИС.</param>
        /// <param name="xmlPath">Путь к XML титула T2.</param>
        /// <param name="signaturePath">Опциональный путь к файлу подписи T2; при пустом значении подпись берется из EpdRepo.</param>
        /// <returns>Итог выполнения T2 с ключевыми идентификаторами.</returns>
        /// <remarks>
        /// Перед отправкой проверяются: стадия timeline и наличие TransportationId,
        /// полученного на предыдущем шаге. Подпись валидируется локально до сетевого вызова.
        /// </remarks>
        public KonturT2ExecutionResult StartDanaflexT2(long timelineId, string xmlPath, string signaturePath)
        {
            return StartDanaflexT2(timelineId, xmlPath, signaturePath, string.Empty);
        }

        /// <summary>
        /// Выполняет сценарий отправки T2 как ответного титула с явным указанием boxId отправителя.
        /// </summary>
        /// <param name="timelineId">Внутренний идентификатор документа в timeline ТИС.</param>
        /// <param name="xmlPath">Путь к XML титула T2.</param>
        /// <param name="signaturePath">Опциональный путь к файлу открепленной подписи T2.</param>
        /// <param name="senderBoxId">DiadocBoxId отправителя для текущего этапа.</param>
        /// <returns>Итог выполнения T2 с ключевыми идентификаторами.</returns>
        /// <remarks>При непустом senderBoxId значение из настроек не используется.</remarks>
        public KonturT2ExecutionResult StartDanaflexT2(long timelineId, string xmlPath, string signaturePath, string senderBoxId)
        {
            // Проверяем базовую готовность адаптера и подключенных репозиториев.
            if (!IsReady())
            {
                return new KonturT2ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    Message = "AdapterNotReady"
                };
            }

            if (SettingsRepository == null || OperatorRefRepository == null || RawLogRepository == null || TimelineRepository == null)
            {
                return new KonturT2ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    Message = "StorageRepositoriesNotConfigured"
                };
            }

            // Проверяем стадию timeline перед отправкой T2.
            var lastStatus = TimelineRepository.GetLastStatus(timelineId);
            if (!KonturStartPolicy.IsT2StartAllowed(lastStatus))
            {
                return new KonturT2ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    Message = "T2StartNotAllowedForStatus: " + (string.IsNullOrEmpty(lastStatus) ? "<empty>" : lastStatus)
                };
            }

            // Проверяем обязательную настройку ящика отправителя.
            var senderBoxIdResolved = senderBoxId;
            if (string.IsNullOrEmpty(senderBoxIdResolved))
            {
                senderBoxIdResolved = ResolveSenderBoxIdFallback("T2");
            }

            if (string.IsNullOrEmpty(senderBoxIdResolved))
            {
                return new KonturT2ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    Message = "SenderBoxIdMissing"
                };
            }

            // Проверяем обязательный входной XML-файл T2.
            if (string.IsNullOrEmpty(xmlPath) || !File.Exists(xmlPath))
            {
                return new KonturT2ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    Message = "XmlFileNotFound: " + (string.IsNullOrEmpty(xmlPath) ? "<empty>" : xmlPath)
                };
            }

            var xmlBytes = File.ReadAllBytes(xmlPath);
            var xmlFileName = Path.GetFileName(xmlPath);
            if (string.IsNullOrEmpty(xmlFileName))
            {
                return new KonturT2ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    Message = "XmlFileNameMissing"
                };
            }

            // Проверяем, что есть связь на перевозку из предыдущего шага.
            var transportationId = OperatorRefRepository.GetLatestRefValue(timelineId, "Kontur", "TransportationId");
            if (string.IsNullOrEmpty(transportationId))
            {
                return new KonturT2ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    Message = "TransportationIdMissing"
                };
            }

            const string endpoint = "/v1/transportations/documents";

            // Получаем подпись: либо из файла, либо из хранилища EPD.
            byte[] signatureBytes = null;
            string signatureFileName = "t2_from_store.sig";
            if (!string.IsNullOrEmpty(signaturePath))
            {
                if (!File.Exists(signaturePath))
                {
                    return new KonturT2ExecutionResult
                    {
                        IsSuccess = false,
                        TimelineId = timelineId,
                        TransportationId = transportationId,
                        Message = "SignatureFileNotFound: " + signaturePath
                    };
                }

                signatureBytes = File.ReadAllBytes(signaturePath);
                var candidateFileName = Path.GetFileName(signaturePath);
                if (!string.IsNullOrEmpty(candidateFileName))
                    signatureFileName = candidateFileName;
            }
            else
            {
                // Для T2 требуется подпись перевозчика (sig2_detached).
                // Использование подписи T1 (sig_detached) приводит к неизбежному рассинхрону хеша.
                signatureBytes = EpdRepo.GetSig2Bytes(timelineId);
            }

            // Подпись обязательна для T2; если ее нет, останавливаем сценарий до API-вызова.
            if (signatureBytes == null || signatureBytes.Length == 0)
            {
                return new KonturT2ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    TransportationId = transportationId,
                    Message = "SignatureMissingForT2"
                };
            }

            // Локально верифицируем detached CMS, чтобы не отправлять заведомо некорректную подпись.
            string verifyInfo;
            var isSignatureValid = EpdRepo.VerifyDetachedCms(xmlBytes, signatureBytes, out verifyInfo);
            if (!isSignatureValid && !string.IsNullOrEmpty(signaturePath))
            {
                // Если в UI остался выбран старый .sgn, пробуем системную sig2_detached,
                // которую SignEpd сохраняет в БД по текущему титулу T2.
                var storeSignatureBytes = EpdRepo.GetSig2Bytes(timelineId);
                if (storeSignatureBytes != null && storeSignatureBytes.Length > 0)
                {
                    string storeVerifyInfo;
                    if (EpdRepo.VerifyDetachedCms(xmlBytes, storeSignatureBytes, out storeVerifyInfo))
                    {
                        signatureBytes = storeSignatureBytes;
                        signatureFileName = "t2_from_store.sig";
                        isSignatureValid = true;
                        verifyInfo = storeVerifyInfo;
                    }
                }
            }

            if (!isSignatureValid)
            {
                return new KonturT2ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    TransportationId = transportationId,
                    Message = "SignatureVerifyFailed: " + (string.IsNullOrEmpty(verifyInfo) ? "unknown" : verifyInfo)
                };
            }

            // Формируем идентификатор попытки, чтобы request/response конкретного запуска T2 читались как единый блок.
            var runId = CreateRunId();
            var requestDirection = "request";
            var responseDirection = "response";
            var requestPayload = BuildRequestPayload("StartDanaflexT2", "T2", runId, timelineId, senderBoxIdResolved);

            // Фиксируем запуск T2 перед сетевым вызовом с меткой этапа и runId.
            RawLogRepository.InsertLog(timelineId, "Kontur", requestDirection, endpoint, null, requestPayload);

            // Выполняем отправку T2 байтами, чтобы использовать подпись как из файла, так и из EPD-хранилища.
            var sendResult = Client.SendT2ResponseBytes(
                xmlBytes,
                xmlFileName,
                signatureBytes,
                signatureFileName,
                senderBoxIdResolved,
                transportationId);

            // Всегда фиксируем ответ для диагностики.
            var responsePayload = BuildResponsePayload("T2", runId, sendResult.SanitizedResponsePayload);
            RawLogRepository.InsertLog(
                timelineId,
                "Kontur",
                responseDirection,
                endpoint,
                sendResult.HttpStatus,
                responsePayload);

            // При неуспехе возвращаем причину без записи refs.
            if (!sendResult.IsSuccess)
            {
                return new KonturT2ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    TransportationId = transportationId,
                    Message = "KonturSendFailed: " + sendResult.ErrorMessage
                };
            }

            // Фиксируем refs T2 для последующих этапов.
            OperatorRefRepository.InsertRef(timelineId, "Kontur", "T2TitleId", sendResult.TitleId, string.Empty);
            OperatorRefRepository.InsertRef(timelineId, "Kontur", "DiadocBoxId", senderBoxIdResolved, string.Empty);

            return new KonturT2ExecutionResult
            {
                IsSuccess = true,
                TimelineId = timelineId,
                TransportationId = sendResult.TransportationId,
                TitleId = sendResult.TitleId,
                Message = "T2Started"
            };
        }

        /// <summary>
        /// Выполняет сценарий отправки T3 как ответного титула.
        /// </summary>
        /// <param name="timelineId">Внутренний идентификатор документа в timeline ТИС.</param>
        /// <param name="xmlPath">Путь к XML титула T3.</param>
        /// <param name="signaturePath">Опциональный путь к файлу подписи T3; при пустом значении подпись берется из EpdRepo.</param>
        /// <returns>Итог выполнения T3 с ключевыми идентификаторами.</returns>
        /// <remarks>
        /// Перед отправкой проверяются: стадия timeline и наличие TransportationId,
        /// полученного на предыдущем шаге. Подпись валидируется локально до сетевого вызова.
        /// </remarks>
        public KonturT3ExecutionResult StartDanaflexT3(long timelineId, string xmlPath, string signaturePath)
        {
            return StartDanaflexT3(timelineId, xmlPath, signaturePath, string.Empty);
        }

        /// <summary>
        /// Выполняет сценарий отправки T3 как ответного титула из внутреннего артефакта ТИС.
        /// </summary>
        /// <param name="timelineId">Внутренний идентификатор документа в timeline ТИС.</param>
        /// <param name="artifact">XML и открепленная подпись титула T3.</param>
        /// <param name="senderBoxId">DiadocBoxId отправителя для текущего этапа.</param>
        /// <returns>Итог выполнения T3 с ключевыми идентификаторами.</returns>
        /// <remarks>Метод нужен для запуска T3 без пользовательских путей к XML/SGN-файлам.</remarks>
        public KonturT3ExecutionResult StartDanaflexT3Artifact(long timelineId, KonturTitleArtifact artifact, string senderBoxId)
        {
            // Проверяем базовую готовность адаптера до любых операций с данными этапа.
            if (!IsReady())
            {
                return new KonturT3ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    Message = "AdapterNotReady"
                };
            }

            if (SettingsRepository == null || OperatorRefRepository == null || RawLogRepository == null || TimelineRepository == null)
            {
                return new KonturT3ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    Message = "StorageRepositoriesNotConfigured"
                };
            }

            // Проверяем стадию timeline перед отправкой T3, чтобы не получить предсказуемую ошибку API.
            var lastStatus = TimelineRepository.GetLastStatus(timelineId);
            if (!KonturStartPolicy.IsT3StartAllowed(lastStatus))
            {
                return new KonturT3ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    Message = "T3StartNotAllowedForStatus: " + (string.IsNullOrEmpty(lastStatus) ? "<empty>" : lastStatus)
                };
            }

            var senderBoxIdResolved = senderBoxId;
            if (string.IsNullOrEmpty(senderBoxIdResolved))
            {
                senderBoxIdResolved = ResolveSenderBoxIdFallback("T3");
            }

            if (string.IsNullOrEmpty(senderBoxIdResolved))
            {
                return new KonturT3ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    Message = "SenderBoxIdMissing"
                };
            }

            if (artifact == null || !artifact.HasXml)
            {
                return new KonturT3ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    Message = "T3XmlArtifactMissing"
                };
            }

            var transportationId = OperatorRefRepository.GetLatestRefValue(timelineId, "Kontur", "TransportationId");
            if (string.IsNullOrEmpty(transportationId))
            {
                return new KonturT3ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    Message = "TransportationIdMissing"
                };
            }

            if (!artifact.HasSignature)
            {
                return new KonturT3ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    TransportationId = transportationId,
                    Message = "SignatureMissingForT3"
                };
            }

            // Повторная проверка перед отправкой защищает от рассинхронизации сохраненного XML и подписи.
            string verifyInfo;
            var isSignatureValid = EpdRepo.VerifyDetachedCms(artifact.TitleXml, artifact.TitleSgn, out verifyInfo);
            if (!isSignatureValid)
            {
                return new KonturT3ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    TransportationId = transportationId,
                    Message = "SignatureVerifyFailed: " + (string.IsNullOrEmpty(verifyInfo) ? "unknown" : verifyInfo)
                };
            }

            const string endpoint = "/v1/transportations/documents";
            var runId = CreateRunId();
            var requestDirection = "request";
            var responseDirection = "response";
            var requestPayload = BuildRequestPayload("StartDanaflexT3Artifact", "T3", runId, timelineId, senderBoxIdResolved);

            // Логируем попытку до сетевого вызова, чтобы не терять диагностику при транспортных ошибках.
            RawLogRepository.InsertLog(timelineId, "Kontur", requestDirection, endpoint, null, requestPayload);

            var sendResult = Client.SendT3ResponseBytes(
                artifact.TitleXml,
                string.IsNullOrEmpty(artifact.XmlFileName) ? "t3.xml" : artifact.XmlFileName,
                artifact.TitleSgn,
                string.IsNullOrEmpty(artifact.SignatureFileName) ? "t3.sig" : artifact.SignatureFileName,
                senderBoxIdResolved);

            var responsePayload = BuildResponsePayload("T3", runId, sendResult.SanitizedResponsePayload);
            RawLogRepository.InsertLog(
                timelineId,
                "Kontur",
                responseDirection,
                endpoint,
                sendResult.HttpStatus,
                responsePayload);

            if (!sendResult.IsSuccess)
            {
                return new KonturT3ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    TransportationId = transportationId,
                    Message = "KonturSendFailed: " + sendResult.ErrorMessage
                };
            }

            // Фиксируем refs T3 для последующих этапов и повторной диагностики.
            OperatorRefRepository.InsertRef(timelineId, "Kontur", "T3TitleId", sendResult.TitleId, string.Empty);
            OperatorRefRepository.InsertRef(timelineId, "Kontur", "DiadocBoxId", senderBoxIdResolved, string.Empty);

            return new KonturT3ExecutionResult
            {
                IsSuccess = true,
                TimelineId = timelineId,
                TransportationId = sendResult.TransportationId,
                TitleId = sendResult.TitleId,
                Message = "T3Started"
            };
        }

        /// <summary>
        /// Выполняет сценарий отправки T3 как ответного титула с явным указанием boxId отправителя.
        /// </summary>
        /// <param name="timelineId">Внутренний идентификатор документа в timeline ТИС.</param>
        /// <param name="xmlPath">Путь к XML титула T3.</param>
        /// <param name="signaturePath">Опциональный путь к файлу открепленной подписи T3.</param>
        /// <param name="senderBoxId">DiadocBoxId отправителя для текущего этапа.</param>
        /// <returns>Итог выполнения T3 с ключевыми идентификаторами.</returns>
        /// <remarks>При непустом senderBoxId значение из настроек не используется.</remarks>
        public KonturT3ExecutionResult StartDanaflexT3(long timelineId, string xmlPath, string signaturePath, string senderBoxId)
        {
            // Проверяем базовую готовность адаптера и подключенных репозиториев.
            if (!IsReady())
            {
                return new KonturT3ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    Message = "AdapterNotReady"
                };
            }

            if (SettingsRepository == null || OperatorRefRepository == null || RawLogRepository == null || TimelineRepository == null)
            {
                return new KonturT3ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    Message = "StorageRepositoriesNotConfigured"
                };
            }

            // Проверяем стадию timeline перед отправкой T3.
            var lastStatus = TimelineRepository.GetLastStatus(timelineId);
            if (!KonturStartPolicy.IsT3StartAllowed(lastStatus))
            {
                return new KonturT3ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    Message = "T3StartNotAllowedForStatus: " + (string.IsNullOrEmpty(lastStatus) ? "<empty>" : lastStatus)
                };
            }

            // Проверяем обязательную настройку ящика отправителя.
            var senderBoxIdResolved = senderBoxId;
            if (string.IsNullOrEmpty(senderBoxIdResolved))
            {
                senderBoxIdResolved = ResolveSenderBoxIdFallback("T3");
            }

            if (string.IsNullOrEmpty(senderBoxIdResolved))
            {
                return new KonturT3ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    Message = "SenderBoxIdMissing"
                };
            }

            // Проверяем обязательный входной XML-файл T3.
            if (string.IsNullOrEmpty(xmlPath) || !File.Exists(xmlPath))
            {
                return new KonturT3ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    Message = "XmlFileNotFound: " + (string.IsNullOrEmpty(xmlPath) ? "<empty>" : xmlPath)
                };
            }

            var xmlBytes = File.ReadAllBytes(xmlPath);
            var xmlFileName = Path.GetFileName(xmlPath);
            if (string.IsNullOrEmpty(xmlFileName))
            {
                return new KonturT3ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    Message = "XmlFileNameMissing"
                };
            }

            // Проверяем, что есть связь на перевозку из предыдущего шага.
            var transportationId = OperatorRefRepository.GetLatestRefValue(timelineId, "Kontur", "TransportationId");
            if (string.IsNullOrEmpty(transportationId))
            {
                return new KonturT3ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    Message = "TransportationIdMissing"
                };
            }

            const string endpoint = "/v1/transportations/documents";

            // Получаем подпись: либо из файла, либо из хранилища EPD.
            byte[] signatureBytes = null;
            string signatureFileName = "t3_from_store.sig";
            if (!string.IsNullOrEmpty(signaturePath))
            {
                if (!File.Exists(signaturePath))
                {
                    return new KonturT3ExecutionResult
                    {
                        IsSuccess = false,
                        TimelineId = timelineId,
                        TransportationId = transportationId,
                        Message = "SignatureFileNotFound: " + signaturePath
                    };
                }

                signatureBytes = File.ReadAllBytes(signaturePath);
                var candidateFileName = Path.GetFileName(signaturePath);
                if (!string.IsNullOrEmpty(candidateFileName))
                    signatureFileName = candidateFileName;
            }
            else
            {
                signatureBytes = EpdRepo.GetLatestSigBytes(timelineId);
            }

            // Подпись обязательна для T3; если ее нет, останавливаем сценарий до API-вызова.
            if (signatureBytes == null || signatureBytes.Length == 0)
            {
                return new KonturT3ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    TransportationId = transportationId,
                    Message = "SignatureMissingForT3"
                };
            }

            // Локально верифицируем detached CMS, чтобы не отправлять заведомо некорректную подпись.
            string verifyInfo;
            var isSignatureValid = EpdRepo.VerifyDetachedCms(xmlBytes, signatureBytes, out verifyInfo);
            if (!isSignatureValid)
            {
                return new KonturT3ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    TransportationId = transportationId,
                    Message = "SignatureVerifyFailed: " + (string.IsNullOrEmpty(verifyInfo) ? "unknown" : verifyInfo)
                };
            }

            // Формируем идентификатор попытки, чтобы request/response конкретного запуска T3 читались как единый блок.
            var runId = CreateRunId();
            var requestDirection = "request";
            var responseDirection = "response";
            var requestPayload = BuildRequestPayload("StartDanaflexT3", "T3", runId, timelineId, senderBoxIdResolved);

            // Фиксируем запуск T3 перед сетевым вызовом с меткой этапа и runId.
            RawLogRepository.InsertLog(timelineId, "Kontur", requestDirection, endpoint, null, requestPayload);

            // Выполняем отправку T3 байтами, чтобы использовать подпись как из файла, так и из EPD-хранилища.
            var sendResult = Client.SendT3ResponseBytes(
                xmlBytes,
                xmlFileName,
                signatureBytes,
                signatureFileName,
                senderBoxIdResolved);

            // Всегда фиксируем ответ для диагностики.
            var responsePayload = BuildResponsePayload("T3", runId, sendResult.SanitizedResponsePayload);
            RawLogRepository.InsertLog(
                timelineId,
                "Kontur",
                responseDirection,
                endpoint,
                sendResult.HttpStatus,
                responsePayload);

            // При неуспехе возвращаем причину без записи refs.
            if (!sendResult.IsSuccess)
            {
                return new KonturT3ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    TransportationId = transportationId,
                    Message = "KonturSendFailed: " + sendResult.ErrorMessage
                };
            }

            // Фиксируем refs T3 для последующих этапов.
            OperatorRefRepository.InsertRef(timelineId, "Kontur", "T3TitleId", sendResult.TitleId, string.Empty);
            OperatorRefRepository.InsertRef(timelineId, "Kontur", "DiadocBoxId", senderBoxIdResolved, string.Empty);

            return new KonturT3ExecutionResult
            {
                IsSuccess = true,
                TimelineId = timelineId,
                TransportationId = sendResult.TransportationId,
                TitleId = sendResult.TitleId,
                Message = "T3Started"
            };
        }

        /// <summary>
        /// Выполняет сценарий отправки T4 как ответного титула.
        /// </summary>
        /// <param name="timelineId">Внутренний идентификатор документа в timeline ТИС.</param>
        /// <param name="xmlPath">Путь к XML титула T4.</param>
        /// <param name="signaturePath">Опциональный путь к файлу подписи T4; при пустом значении подпись берется из EpdRepo.</param>
        /// <returns>Итог выполнения T4 с ключевыми идентификаторами.</returns>
        /// <remarks>
        /// Перед отправкой проверяются: стадия timeline и наличие TransportationId,
        /// полученного на предыдущем шаге. Подпись валидируется локально до сетевого вызова.
        /// </remarks>
        public KonturT4ExecutionResult StartDanaflexT4(long timelineId, string xmlPath, string signaturePath)
        {
            return StartDanaflexT4(timelineId, xmlPath, signaturePath, string.Empty);
        }

        /// <summary>
        /// Выполняет сценарий отправки T4 как ответного титула с явным указанием boxId отправителя.
        /// </summary>
        /// <param name="timelineId">Внутренний идентификатор документа в timeline ТИС.</param>
        /// <param name="xmlPath">Путь к XML титула T4.</param>
        /// <param name="signaturePath">Опциональный путь к файлу открепленной подписи T4.</param>
        /// <param name="senderBoxId">DiadocBoxId отправителя для текущего этапа.</param>
        /// <returns>Итог выполнения T4 с ключевыми идентификаторами.</returns>
        /// <remarks>При непустом senderBoxId значение из настроек не используется.</remarks>
        public KonturT4ExecutionResult StartDanaflexT4(long timelineId, string xmlPath, string signaturePath, string senderBoxId)
        {
            // Проверяем базовую готовность адаптера и подключенных репозиториев.
            if (!IsReady())
            {
                return new KonturT4ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    Message = "AdapterNotReady"
                };
            }

            if (SettingsRepository == null || OperatorRefRepository == null || RawLogRepository == null || TimelineRepository == null)
            {
                return new KonturT4ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    Message = "StorageRepositoriesNotConfigured"
                };
            }

            // Проверяем стадию timeline перед отправкой T4.
            var lastStatus = TimelineRepository.GetLastStatus(timelineId);
            if (!KonturStartPolicy.IsT4StartAllowed(lastStatus))
            {
                return new KonturT4ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    Message = "T4StartNotAllowedForStatus: " + (string.IsNullOrEmpty(lastStatus) ? "<empty>" : lastStatus)
                };
            }

            // Проверяем обязательную настройку ящика отправителя.
            var senderBoxIdResolved = senderBoxId;
            if (string.IsNullOrEmpty(senderBoxIdResolved))
            {
                senderBoxIdResolved = ResolveSenderBoxIdFallback("T4");
            }

            if (string.IsNullOrEmpty(senderBoxIdResolved))
            {
                return new KonturT4ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    Message = "SenderBoxIdMissing"
                };
            }

            // Проверяем обязательный входной XML-файл T4.
            if (string.IsNullOrEmpty(xmlPath) || !File.Exists(xmlPath))
            {
                return new KonturT4ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    Message = "XmlFileNotFound: " + (string.IsNullOrEmpty(xmlPath) ? "<empty>" : xmlPath)
                };
            }

            var xmlBytes = File.ReadAllBytes(xmlPath);
            var xmlFileName = Path.GetFileName(xmlPath);
            if (string.IsNullOrEmpty(xmlFileName))
            {
                return new KonturT4ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    Message = "XmlFileNameMissing"
                };
            }

            // Проверяем, что есть связь на перевозку из предыдущего шага.
            var transportationId = OperatorRefRepository.GetLatestRefValue(timelineId, "Kontur", "TransportationId");
            if (string.IsNullOrEmpty(transportationId))
            {
                return new KonturT4ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    Message = "TransportationIdMissing"
                };
            }

            const string endpoint = "/v1/transportations/documents";

            // Получаем подпись: либо из файла, либо из хранилища EPD.
            byte[] signatureBytes = null;
            string signatureFileName = "t4_from_store.sig";
            if (!string.IsNullOrEmpty(signaturePath))
            {
                if (!File.Exists(signaturePath))
                {
                    return new KonturT4ExecutionResult
                    {
                        IsSuccess = false,
                        TimelineId = timelineId,
                        TransportationId = transportationId,
                        Message = "SignatureFileNotFound: " + signaturePath
                    };
                }

                signatureBytes = File.ReadAllBytes(signaturePath);
                var candidateFileName = Path.GetFileName(signaturePath);
                if (!string.IsNullOrEmpty(candidateFileName))
                    signatureFileName = candidateFileName;
            }
            else
            {
                signatureBytes = EpdRepo.GetLatestSigBytes(timelineId);
            }

            // Подпись обязательна для T4; если ее нет, останавливаем сценарий до API-вызова.
            if (signatureBytes == null || signatureBytes.Length == 0)
            {
                return new KonturT4ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    TransportationId = transportationId,
                    Message = "SignatureMissingForT4"
                };
            }

            // Локально верифицируем detached CMS, чтобы не отправлять заведомо некорректную подпись.
            string verifyInfo;
            var isSignatureValid = EpdRepo.VerifyDetachedCms(xmlBytes, signatureBytes, out verifyInfo);
            if (!isSignatureValid)
            {
                return new KonturT4ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    TransportationId = transportationId,
                    Message = "SignatureVerifyFailed: " + (string.IsNullOrEmpty(verifyInfo) ? "unknown" : verifyInfo)
                };
            }

            // Формируем идентификатор попытки, чтобы request/response конкретного запуска T4 читались как единый блок.
            var runId = CreateRunId();
            var requestDirection = "request";
            var responseDirection = "response";
            var requestPayload = BuildRequestPayload("StartDanaflexT4", "T4", runId, timelineId, senderBoxIdResolved);

            // Фиксируем запуск T4 перед сетевым вызовом с меткой этапа и runId.
            RawLogRepository.InsertLog(timelineId, "Kontur", requestDirection, endpoint, null, requestPayload);

            // Выполняем отправку T4 байтами, чтобы использовать подпись как из файла, так и из EPD-хранилища.
            var sendResult = Client.SendT4ResponseBytes(
                xmlBytes,
                xmlFileName,
                signatureBytes,
                signatureFileName,
                senderBoxIdResolved);

            // Всегда фиксируем ответ для диагностики.
            var responsePayload = BuildResponsePayload("T4", runId, sendResult.SanitizedResponsePayload);
            RawLogRepository.InsertLog(
                timelineId,
                "Kontur",
                responseDirection,
                endpoint,
                sendResult.HttpStatus,
                responsePayload);

            // При неуспехе возвращаем причину без записи refs.
            if (!sendResult.IsSuccess)
            {
                return new KonturT4ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    TransportationId = transportationId,
                    Message = "KonturSendFailed: " + sendResult.ErrorMessage
                };
            }

            // Фиксируем refs T4 для последующей диагностики.
            OperatorRefRepository.InsertRef(timelineId, "Kontur", "T4TitleId", sendResult.TitleId, string.Empty);
            OperatorRefRepository.InsertRef(timelineId, "Kontur", "DiadocBoxId", senderBoxIdResolved, string.Empty);

            return new KonturT4ExecutionResult
            {
                IsSuccess = true,
                TimelineId = timelineId,
                TransportationId = sendResult.TransportationId,
                TitleId = sendResult.TitleId,
                Message = "T4Started"
            };
        }

        /// <summary>
        /// Выполняет общий конвейер отправки T1 с выбором режима initial или draft.
        /// </summary>
        /// <param name="timelineId">Внутренний идентификатор документа в timeline ТИС.</param>
        /// <param name="xmlPath">Путь к XML титула T1.</param>
        /// <param name="useDraftMode">Признак режима: true для draft, false для initial.</param>
        /// <returns>Итог выполнения с признаками успеха и ключевыми идентификаторами.</returns>
        /// <remarks>Общий конвейер снижает дублирование и обеспечивает единые правила фиксации логов и refs.</remarks>
        private KonturT1ExecutionResult StartDanaflexT1Internal(long timelineId, string xmlPath, bool useDraftMode, string senderBoxId)
        {
            // Проверяем базовую готовность адаптера и подключенных репозиториев.
            if (!IsReady())
            {
                return new KonturT1ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    Message = "AdapterNotReady"
                };
            }

            if (SettingsRepository == null || OperatorRefRepository == null || RawLogRepository == null || TimelineRepository == null)
            {
                return new KonturT1ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    Message = "StorageRepositoriesNotConfigured"
                };
            }

            // Проверяем обязательную настройку ящика отправителя.
            var senderBoxIdResolved = senderBoxId;
            if (string.IsNullOrEmpty(senderBoxIdResolved))
            {
                senderBoxIdResolved = ResolveSenderBoxIdFallback("T1");
            }

            if (string.IsNullOrEmpty(senderBoxIdResolved))
            {
                return new KonturT1ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    Message = "SenderBoxIdMissing"
                };
            }

            // Для draft-ветки заранее проверяем статус в timeline, чтобы не получать предсказуемый 400 от API.
            if (useDraftMode)
            {
                var lastStatus = TimelineRepository.GetLastStatus(timelineId);
                if (!KonturStartPolicy.IsDraftStartAllowed(lastStatus))
                {
                    return new KonturT1ExecutionResult
                    {
                        IsSuccess = false,
                        TimelineId = timelineId,
                        Message = "DraftStartNotAllowedForStatus: " + (string.IsNullOrEmpty(lastStatus) ? "<empty>" : lastStatus)
                    };
                }
            }

            var endpoint = useDraftMode ? "/v1/transportations/documents/draft" : "/v1/transportations/documents";
            var operation = useDraftMode ? "StartDanaflexT1Draft" : "StartDanaflexT1Initial";
            var stageCode = useDraftMode ? "T1_DRAFT" : "T1_INITIAL";
            var runId = CreateRunId();
            var requestDirection = "request";
            var responseDirection = "response";
            var requestPayload = BuildRequestPayload(operation, stageCode, runId, timelineId, senderBoxIdResolved);

            // Фиксируем факт запуска перед сетевым вызовом.
            RawLogRepository.InsertLog(timelineId, "Kontur", requestDirection, endpoint, null, requestPayload);

            // Выполняем сетевой вызов в выбранном режиме.
            var sendResult = useDraftMode
                ? Client.SendT1Draft(xmlPath, senderBoxIdResolved)
                : Client.SendT1Initial(xmlPath, senderBoxIdResolved);

            // Всегда фиксируем ответ для диагностики, включая неуспешные случаи.
            var responsePayload = BuildResponsePayload(stageCode, runId, sendResult.SanitizedResponsePayload);
            RawLogRepository.InsertLog(
                timelineId,
                "Kontur",
                responseDirection,
                endpoint,
                sendResult.HttpStatus,
                responsePayload);

            // При неуспехе возвращаем причину без записи refs.
            if (!sendResult.IsSuccess)
            {
                var sendErrorMessage = "KonturSendFailed: " + sendResult.ErrorMessage;
                if (!useDraftMode && IsRoamingDraftError(sendResult.SanitizedResponsePayload))
                {
                    sendErrorMessage =
                        "KonturSendFailed: Участники T1 содержат роуминг-организации (CannotSaveDraftWithRoamingParticipants). " +
                        "Проверьте состав участников в XML титула T1 для текущего timeline и используйте тестовые организации без роуминга.";
                }

                return new KonturT1ExecutionResult
                {
                    IsSuccess = false,
                    TimelineId = timelineId,
                    Message = sendErrorMessage
                };
            }

            // При успехе фиксируем внешние идентификаторы для следующих этапов T2/T3/T4.
            OperatorRefRepository.InsertRef(timelineId, "Kontur", "TransportationId", sendResult.TransportationId, string.Empty);
            OperatorRefRepository.InsertRef(timelineId, "Kontur", "TitleId", sendResult.TitleId, string.Empty);
            OperatorRefRepository.InsertRef(timelineId, "Kontur", "DiadocBoxId", senderBoxIdResolved, string.Empty);

            return new KonturT1ExecutionResult
            {
                IsSuccess = true,
                TimelineId = timelineId,
                TransportationId = sendResult.TransportationId,
                TitleId = sendResult.TitleId,
                Message = "T1Started"
            };
        }

        /// <summary>
        /// Формирует идентификатор попытки отправки для связывания request/response в raw-логе.
        /// </summary>
        /// <returns>Строковый runId в формате N без разделителей.</returns>
        /// <remarks>runId используется только для диагностики и не влияет на бизнес-семантику документа.</remarks>
        private string CreateRunId()
        {
            return Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// Формирует payload request-лога с контекстом этапа и попытки.
        /// </summary>
        /// <param name="operation">Имя внутренней операции адаптера.</param>
        /// <param name="stageCode">Код этапа отправки.</param>
        /// <param name="runId">Идентификатор попытки отправки.</param>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="senderBoxId">BoxId отправителя для текущей операции.</param>
        /// <returns>Строка payload для request-записи raw-лога.</returns>
        /// <remarks>В payload сохраняется только техническая диагностика без секретов и тела XML.</remarks>
        private string BuildRequestPayload(string operation, string stageCode, string runId, long timelineId, string senderBoxId)
        {
            return "stage=" + (string.IsNullOrEmpty(stageCode) ? "UNKNOWN" : stageCode)
                + ";op=" + operation
                + ";runId=" + runId
                + ";timelineId=" + timelineId
                + ";senderBoxId=" + (string.IsNullOrEmpty(senderBoxId) ? "<empty>" : senderBoxId);
        }

        /// <summary>
        /// Формирует payload response-лога с сохранением runId и ответа API.
        /// </summary>
        /// <param name="stageCode">Код этапа отправки.</param>
        /// <param name="runId">Идентификатор попытки отправки.</param>
        /// <param name="sanitizedResponsePayload">Очищенный payload ответа Контур API.</param>
        /// <returns>Строка payload для response-записи raw-лога.</returns>
        /// <remarks>runId в префиксе позволяет однозначно связать ответ с конкретным request.</remarks>
        private string BuildResponsePayload(string stageCode, string runId, string sanitizedResponsePayload)
        {
            var payload = string.IsNullOrEmpty(sanitizedResponsePayload) ? string.Empty : sanitizedResponsePayload;
            return "stage=" + (string.IsNullOrEmpty(stageCode) ? "UNKNOWN" : stageCode)
                + ";runId=" + runId
                + ";payload=" + payload;
        }

        /// <summary>
        /// Разрешает fallback-значение sender boxId по коду титула.
        /// </summary>
        /// <param name="titleCode">Код титула T1/T2/T3/T4.</param>
        /// <returns>Первое найденное непустое значение boxId или пустая строка.</returns>
        /// <remarks>
        /// Метод нужен для обратной совместимости прямых вызовов адаптера,
        /// где senderBoxId еще не был разрешен на уровне сервисов T1-T4.
        /// </remarks>
        private string ResolveSenderBoxIdFallback(string titleCode)
        {
            if (SettingsRepository == null)
            {
                return string.Empty;
            }

            if (titleCode == "T1")
            {
                return ReadFirstNotEmptySetting("SenderBoxId_T1", "DanaflexSenderBoxId_T1", "DanaflexSenderBoxId_Consignor", "DanaflexSenderBoxId");
            }

            if (titleCode == "T2")
            {
                return ReadFirstNotEmptySetting("SenderBoxId_T2", "DanaflexSenderBoxId_T2", "DanaflexSenderBoxId_Carrier", "DanaflexSenderBoxId");
            }

            if (titleCode == "T3")
            {
                return ReadFirstNotEmptySetting("SenderBoxId_T3", "DanaflexSenderBoxId_T3", "DanaflexSenderBoxId_Consignee", "DanaflexSenderBoxId");
            }

            if (titleCode == "T4")
            {
                return ReadFirstNotEmptySetting("SenderBoxId_T4", "DanaflexSenderBoxId_T4", "DanaflexSenderBoxId_Carrier", "DanaflexSenderBoxId");
            }

            return ReadFirstNotEmptySetting("DanaflexSenderBoxId");
        }

        /// <summary>
        /// Читает первое непустое значение настройки boxId из списка ключей.
        /// </summary>
        /// <param name="keys">Упорядоченный список ключей настроек.</param>
        /// <returns>Первое найденное непустое значение или пустая строка.</returns>
        /// <remarks>Вспомогательный метод устраняет дублирование fallback-цепочек внутри адаптера.</remarks>
        private string ReadFirstNotEmptySetting(params string[] keys)
        {
            if (keys == null || SettingsRepository == null)
            {
                return string.Empty;
            }

            for (var i = 0; i < keys.Length; i++)
            {
                var value = SettingsRepository.GetSettingValue("Kontur", keys[i]);
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Проверяет наличие кода ошибки роуминга участников при попытке сохранить черновик.
        /// </summary>
        /// <param name="sanitizedResponsePayload">Очищенный payload ответа Контур API.</param>
        /// <returns>Истина, если обнаружен код CannotSaveDraftWithRoamingParticipants.</returns>
        /// <remarks>Используется для точной диагностики T1 initial без изменения бизнес-ветки отправки.</remarks>
        private bool IsRoamingDraftError(string sanitizedResponsePayload)
        {
            if (string.IsNullOrEmpty(sanitizedResponsePayload))
            {
                return false;
            }

            return sanitizedResponsePayload.IndexOf("CannotSaveDraftWithRoamingParticipants", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
