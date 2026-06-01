/*
  ФАЙЛ: KonturStoredStageCompletionGateway.cs
  НАЗНАЧЕНИЕ: Диагностическая реализация порта evidence по уже сохраненным refs, raw-log и legacy timeline.
  Не выполняет новых сетевых вызовов и безопасна для первого внедрения в legacy-контур.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  29.05.2026 - Первичное создание stored-gateway для проверки завершения T1 без прямого API polling.
*/

using System;
using Tis.KonturIntegration.Models;
using Tis.KonturIntegration.Storage;

namespace Tis.KonturIntegration.Integration
{
    /// <summary>
    /// Собирает evidence завершения этапа из уже сохраненных диагностических источников.
    /// </summary>
    /// <remarks>
    /// Эта реализация не доказывает завершение T1 сама по себе. Она фиксирует то, что уже известно системе,
    /// чтобы на реальном прогоне увидеть, каких внешних признаков не хватает до автоматического подтверждения.
    /// </remarks>
    public class KonturStoredStageCompletionGateway : IKonturStageCompletionGateway
    {
        /// <summary>
        /// Инициализирует gateway существующими repository refs, raw-log и timeline.
        /// </summary>
        /// <param name="operatorRefRepository">Repository внешних идентификаторов оператора.</param>
        /// <param name="rawLogRepository">Repository raw-log ответов оператора.</param>
        /// <param name="timelineRepository">Repository чтения legacy timeline.</param>
        /// <remarks>Все зависимости передаются снаружи, чтобы gateway не создавал SQL-адаптеры самостоятельно.</remarks>
        public KonturStoredStageCompletionGateway(
            KonturOperatorRefRepository operatorRefRepository,
            KonturRawLogRepository rawLogRepository,
            KonturTimelineRepository timelineRepository)
        {
            if (operatorRefRepository == null)
            {
                throw new ArgumentNullException("operatorRefRepository");
            }

            if (rawLogRepository == null)
            {
                throw new ArgumentNullException("rawLogRepository");
            }

            if (timelineRepository == null)
            {
                throw new ArgumentNullException("timelineRepository");
            }

            OperatorRefRepository = operatorRefRepository;
            RawLogRepository = rawLogRepository;
            TimelineRepository = timelineRepository;
        }

        /// <summary>
        /// Получает repository внешних идентификаторов оператора.
        /// </summary>
        public KonturOperatorRefRepository OperatorRefRepository { get; private set; }

        /// <summary>
        /// Получает repository raw-log ответов оператора.
        /// </summary>
        public KonturRawLogRepository RawLogRepository { get; private set; }

        /// <summary>
        /// Получает repository legacy timeline.
        /// </summary>
        public KonturTimelineRepository TimelineRepository { get; private set; }

        /// <summary>
        /// Возвращает evidence-снимок по уже сохраненным refs, raw-log и timeline.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа в ТИС.</param>
        /// <param name="stageCode">Код этапа, например T1_INITIAL.</param>
        /// <returns>Evidence-снимок для последующей оценки use case-ом.</returns>
        /// <remarks>Метод не вызывает API Контур и не меняет состояние этапа.</remarks>
        public KonturStageCompletionEvidence ReadEvidence(long timelineId, string stageCode)
        {
            var normalizedStageCode = NormalizeStageCode(stageCode);
            var titleCode = StageToTitle(normalizedStageCode);
            var lastResponse = RawLogRepository.GetLastResponseLog(timelineId, "Kontur", normalizedStageCode);
            var lastTimelineStatus = TimelineRepository.GetLastStatus(timelineId);

            return new KonturStageCompletionEvidence
            {
                TimelineId = timelineId,
                StageCode = normalizedStageCode,
                TitleCode = titleCode,
                TransportationId = ReadTransportationId(timelineId),
                TitleId = ReadTitleId(timelineId, titleCode),
                ExternalDocumentStatus = lastTimelineStatus,
                ExternalTitleStatus = string.Empty,
                ExternalActionCode = string.Empty,
                IsDraft = IsDraftEvidence(normalizedStageCode, lastResponse),
                HasActiveError = HasErrorEvidence(lastResponse),
                HttpStatus = lastResponse == null ? null : lastResponse.HttpStatus,
                RawEvidenceSummary = BuildSummary(lastResponse),
                CompletionSource = "StoredDiagnostic",
                CheckedAt = DateTime.Now
            };
        }

        /// <summary>
        /// Читает последний TransportationId по timeline.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <returns>TransportationId или пустую строку.</returns>
        /// <remarks>Значение само по себе не считается подтверждением завершения T1.</remarks>
        private string ReadTransportationId(long timelineId)
        {
            return OperatorRefRepository.GetLatestRefValue(timelineId, "Kontur", "TransportationId");
        }

        /// <summary>
        /// Читает последний TitleId по коду титула.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="titleCode">Код титула.</param>
        /// <returns>TitleId или пустую строку.</returns>
        /// <remarks>Для T1 поддерживается legacy refType TitleId, для ответных титулов stage-specific refType.</remarks>
        private string ReadTitleId(long timelineId, string titleCode)
        {
            if (string.Equals(titleCode, "T1", StringComparison.OrdinalIgnoreCase))
            {
                return OperatorRefRepository.GetLatestRefValue(timelineId, "Kontur", "TitleId");
            }

            return OperatorRefRepository.GetLatestRefValue(timelineId, "Kontur", titleCode + "TitleId");
        }

        /// <summary>
        /// Определяет, похож ли evidence на черновик.
        /// </summary>
        /// <param name="stageCode">Код этапа.</param>
        /// <param name="lastResponse">Последний response-лог этапа.</param>
        /// <returns>True, если evidence содержит признаки draft-ветки.</returns>
        /// <remarks>Проверка нужна, чтобы не открыть T2 по черновику.</remarks>
        private bool IsDraftEvidence(string stageCode, KonturRawLogRepository.LastResponseLog lastResponse)
        {
            if (string.Equals(stageCode, "T1_DRAFT", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var payload = lastResponse == null ? string.Empty : lastResponse.SanitizedPayload;
            var endpoint = lastResponse == null ? string.Empty : lastResponse.Endpoint;
            return ContainsIgnoreCase(payload, "draftId") || ContainsIgnoreCase(endpoint, "/draft");
        }

        /// <summary>
        /// Определяет, есть ли в последнем evidence явная ошибка.
        /// </summary>
        /// <param name="lastResponse">Последний response-лог этапа.</param>
        /// <returns>True, если HTTP-статус или payload похожи на ошибку.</returns>
        /// <remarks>Ошибка блокирует автоматическое подтверждение завершения этапа.</remarks>
        private bool HasErrorEvidence(KonturRawLogRepository.LastResponseLog lastResponse)
        {
            if (lastResponse == null)
            {
                return false;
            }

            if (lastResponse.HttpStatus.HasValue && (lastResponse.HttpStatus.Value < 200 || lastResponse.HttpStatus.Value >= 300))
            {
                return true;
            }

            var payload = lastResponse.SanitizedPayload;
            return ContainsIgnoreCase(payload, "error") || ContainsIgnoreCase(payload, "exception") || ContainsIgnoreCase(payload, "MessageToPost.DocumentAttachments");
        }

        /// <summary>
        /// Формирует краткое описание evidence из последнего response-лога.
        /// </summary>
        /// <param name="lastResponse">Последний response-лог этапа.</param>
        /// <returns>Краткое описание evidence для диагностики.</returns>
        /// <remarks>Текст ограничивается, чтобы не протаскивать длинный payload в состояние процесса.</remarks>
        private string BuildSummary(KonturRawLogRepository.LastResponseLog lastResponse)
        {
            if (lastResponse == null)
            {
                return "StoredDiagnostic: response log not found.";
            }

            var status = lastResponse.HttpStatus.HasValue ? lastResponse.HttpStatus.Value.ToString() : "<empty>";
            var payload = string.IsNullOrEmpty(lastResponse.SanitizedPayload) ? string.Empty : lastResponse.SanitizedPayload;
            if (payload.Length > 500)
            {
                payload = payload.Substring(0, 500);
            }

            return "StoredDiagnostic: rawLogId=" + lastResponse.Id + "; http=" + status + "; endpoint=" + lastResponse.Endpoint + "; payload=" + payload;
        }

        /// <summary>
        /// Проверяет наличие фрагмента в строке без учета регистра.
        /// </summary>
        /// <param name="value">Строка для проверки.</param>
        /// <param name="fragment">Искомый фрагмент.</param>
        /// <returns>True, если фрагмент найден.</returns>
        /// <remarks>Метод совместим с .NET Framework 4.0 и не использует современные overload.</remarks>
        private bool ContainsIgnoreCase(string value, string fragment)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(fragment))
            {
                return false;
            }

            return value.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
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
        /// <remarks>Для T1 поддерживаются варианты T1_INITIAL и T1_DRAFT.</remarks>
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