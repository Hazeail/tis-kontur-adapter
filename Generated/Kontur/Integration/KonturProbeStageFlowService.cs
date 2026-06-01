/*
  ФАЙЛ: KonturProbeStageFlowService.cs
  НАЗНАЧЕНИЕ: Сервис расчета статусного состояния этапа для страницы KonturProbe.
  Выносит из WebForms code-behind логику анализа XML, подписи и последнего ответа оператора, чтобы страница оставалась оркестратором UI.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  23.05.2026 - Первичное создание сервиса статусного состояния этапов KonturProbe.
*/

using System;
using System.IO;
using TIS.EPD;
using Tis.KonturIntegration.Models;
using Tis.KonturIntegration.Storage;

namespace Tis.KonturIntegration.Integration
{
    /// <summary>
    /// Вычисляет текущее состояние шагов этапа KonturProbe по артефактам и последнему ответу Контур API.
    /// </summary>
    /// <remarks>
    /// Сервис нужен, чтобы страница не выполняла SQL- и файловую диагностику напрямую в каждом геттере разметки.
    /// Он работает без сетевых вызовов и использует только локальные данные ТИС и Perdoc.
    /// </remarks>
    public class KonturProbeStageFlowService
    {
        /// <summary>
        /// Инициализирует сервис строкой подключения и корневой папкой рабочих артефактов.
        /// </summary>
        /// <param name="connectionString">Строка подключения к ТИС и Perdoc.</param>
        /// <param name="serverFilesRoot">Корневая папка App_Data\Temp\KonturEtrn.</param>
        /// <remarks>
        /// Корневая папка нужна для поиска актуального рабочего XML, если он еще не попал в SQL-хранилище.
        /// </remarks>
        public KonturProbeStageFlowService(string connectionString, string serverFilesRoot)
        {
            ConnectionString = connectionString ?? string.Empty;
            ServerFilesRoot = serverFilesRoot ?? string.Empty;
        }

        /// <summary>
        /// Получает строку подключения к ТИС и Perdoc.
        /// </summary>
        public string ConnectionString { get; private set; }

        /// <summary>
        /// Получает корневую папку рабочих XML/SGN артефактов страницы.
        /// </summary>
        public string ServerFilesRoot { get; private set; }

        /// <summary>
        /// Строит агрегированное состояние шагов для выбранного этапа.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код этапа UI.</param>
        /// <param name="xmlPath">Выбранный оператором путь к XML.</param>
        /// <param name="signaturePath">Выбранный оператором путь к .sgn.</param>
        /// <returns>Готовое состояние четырех шагов и итогового результата.</returns>
        /// <remarks>
        /// Метод не выполняет модифицирующих действий. Он только диагностирует факт готовности XML, подписи и ответа оператора.
        /// </remarks>
        public KonturProbeStageFlowState BuildState(long timelineId, string stageCode, string xmlPath, string signaturePath)
        {
            var titleCode = StageToTitle(stageCode);
            var hasXml = HasStoredStageXml(timelineId, titleCode, xmlPath);
            var hasSignature = HasStoredStageSignature(timelineId, titleCode, signaturePath);
            var lastResponse = GetLastStageResponseLog(timelineId, stageCode);

            var state = new KonturProbeStageFlowState
            {
                XmlStep = BuildWarnStepState("XML не сформирован"),
                SignatureStep = BuildWarnStepState("Ожидает XML"),
                SendStep = BuildWarnStepState("Ожидает шаги 1-2"),
                ResultStep = BuildWarnStepState("Результат отсутствует"),
                ResultSummary = "Этап еще не запускался."
            };

            if (hasXml)
            {
                state.XmlStep = BuildReadyStepState("XML готов");
                state.SignatureStep = hasSignature
                    ? BuildReadyStepState("Подпись готова")
                    : BuildWarnStepState("Подпись не подготовлена");
            }

            if (lastResponse == null)
            {
                if (hasXml && hasSignature)
                {
                    state.SendStep = BuildReadyStepState("Готов к отправке");
                    state.ResultSummary = "Артефакты этапа готовы. Можно отправлять в Контур.";
                }
                else if (hasXml)
                {
                    state.SendStep = BuildWarnStepState("Ожидает подпись");
                    state.ResultSummary = "XML уже сформирован, но подпись текущего этапа еще не подготовлена.";
                }
                else
                {
                    state.ResultSummary = "Сначала сформируйте актуальный XML для текущего этапа.";
                }

                return state;
            }

            if (lastResponse.HttpStatus.HasValue && lastResponse.HttpStatus.Value >= 200 && lastResponse.HttpStatus.Value < 400)
            {
                state.SendStep = BuildReadyStepState("Отправлен успешно");
                state.ResultStep = BuildReadyStepState("Есть ответ оператора");
                state.ResultSummary = BuildResultSummaryText(lastResponse);
                return state;
            }

            state.SendStep = BuildErrorStepState("Ошибка отправки");
            state.ResultStep = BuildErrorStepState(BuildResultErrorText(lastResponse));
            state.ResultSummary = BuildResultSummaryText(lastResponse);
            return state;
        }

        /// <summary>
        /// Преобразует код этапа UI в код титула ЭТрН.
        /// </summary>
        /// <param name="stageCode">Код этапа UI.</param>
        /// <returns>Код титула T1/T2/T3/T4.</returns>
        /// <remarks>Сервис статусов должен работать в тех же кодах титулов, что и stage-runner.</remarks>
        public string StageToTitle(string stageCode)
        {
            if (string.Equals(stageCode, "T1_INITIAL", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stageCode, "T1_DRAFT", StringComparison.OrdinalIgnoreCase))
            {
                return "T1";
            }

            if (string.Equals(stageCode, "T2", StringComparison.OrdinalIgnoreCase))
            {
                return "T2";
            }

            if (string.Equals(stageCode, "T3", StringComparison.OrdinalIgnoreCase))
            {
                return "T3";
            }

            if (string.Equals(stageCode, "T4", StringComparison.OrdinalIgnoreCase))
            {
                return "T4";
            }

            return stageCode;
        }

        /// <summary>
        /// Проверяет наличие актуального XML для выбранного этапа.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="titleCode">Код титула T1/T2/T3/T4.</param>
        /// <param name="xmlPath">Выбранный путь к XML.</param>
        /// <returns>True, если XML уже доступен для работы.</returns>
        /// <remarks>
        /// Последовательность источников такая: выбранный файл, EPD-хранилище, SQL-артефакт, затем рабочая папка.
        /// </remarks>
        private bool HasStoredStageXml(long timelineId, string titleCode, string xmlPath)
        {
            if (!string.IsNullOrEmpty(xmlPath) && File.Exists(xmlPath) && IsStageServerFile(Path.GetFileName(xmlPath), titleCode))
            {
                return true;
            }

            if (string.Equals(titleCode, "T1", StringComparison.OrdinalIgnoreCase))
            {
                var payload = EpdRepo.GetXmlPayloadTitul(timelineId);
                return (payload != null && payload.Length > 0) || !string.IsNullOrEmpty(FindLatestStageXmlPath(timelineId, titleCode));
            }

            if (string.Equals(titleCode, "T2", StringComparison.OrdinalIgnoreCase))
            {
                var t2Bytes = EpdRepo.GetTitul2Xml(timelineId);
                if (t2Bytes != null && t2Bytes.Length > 0)
                {
                    return true;
                }
            }

            var artifact = new KonturTitleArtifactRepository(ConnectionString).GetLatest(timelineId, titleCode);
            if (artifact != null && artifact.HasXml)
            {
                return true;
            }

            return !string.IsNullOrEmpty(FindLatestStageXmlPath(timelineId, titleCode));
        }

        /// <summary>
        /// Проверяет наличие актуальной подписи для выбранного этапа.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="titleCode">Код титула T1/T2/T3/T4.</param>
        /// <param name="signaturePath">Выбранный путь к .sgn.</param>
        /// <returns>True, если подпись уже доступна.</returns>
        /// <remarks>
        /// Для T1/T2 используются системные подписи, потому что они участвуют в последующих builder-цепочках.
        /// Для T3/T4 допускается прямая опора на SQL-артефакт или выбранный .sgn.
        /// </remarks>
        private bool HasStoredStageSignature(long timelineId, string titleCode, string signaturePath)
        {
            if (!string.IsNullOrEmpty(signaturePath) && File.Exists(signaturePath) && IsStageServerFile(Path.GetFileName(signaturePath), titleCode))
            {
                return true;
            }

            if (string.Equals(titleCode, "T1", StringComparison.OrdinalIgnoreCase))
            {
                var t1Signature = EpdRepo.GetLatestSigBytes(timelineId);
                return t1Signature != null && t1Signature.Length > 0;
            }

            if (string.Equals(titleCode, "T2", StringComparison.OrdinalIgnoreCase))
            {
                var sig2Bytes = EpdRepo.GetSig2Bytes(timelineId);
                if (sig2Bytes != null && sig2Bytes.Length > 0)
                {
                    return true;
                }
            }

            var artifact = new KonturTitleArtifactRepository(ConnectionString).GetLatest(timelineId, titleCode);
            return artifact != null && artifact.HasSignature;
        }

        /// <summary>
        /// Возвращает путь к последнему XML выбранного этапа в рабочей папке.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="titleCode">Код титула T1/T2/T3/T4.</param>
        /// <returns>Абсолютный путь к файлу или пустую строку.</returns>
        private string FindLatestStageXmlPath(long timelineId, string titleCode)
        {
            var currentPath = new KonturStageArtifactWorkspaceService(ServerFilesRoot).FindCurrentXmlPath(timelineId, titleCode);
            if (!string.IsNullOrEmpty(currentPath))
            {
                return currentPath;
            }

            if (!Directory.Exists(ServerFilesRoot))
            {
                return string.Empty;
            }

            if (string.Equals(titleCode, "T1", StringComparison.OrdinalIgnoreCase))
            {
                var overridePath = GetLatestFileByMask(string.Format("t1_override_{0}_*.xml", timelineId));
                if (!string.IsNullOrEmpty(overridePath))
                {
                    return overridePath;
                }

                return GetLatestFileByMask(string.Format("t1_timeline{0}_*.xml", timelineId));
            }

            if (string.Equals(titleCode, "T2", StringComparison.OrdinalIgnoreCase))
            {
                return GetLatestFileByMask(string.Format("t2_timeline{0}_*.xml", timelineId));
            }

            if (string.Equals(titleCode, "T3", StringComparison.OrdinalIgnoreCase))
            {
                return GetLatestFileByMask(string.Format("t3_timeline{0}_*.xml", timelineId));
            }

            if (string.Equals(titleCode, "T4", StringComparison.OrdinalIgnoreCase))
            {
                return GetLatestFileByMask(string.Format("t4_timeline{0}_*.xml", timelineId));
            }

            return string.Empty;
        }

        /// <summary>
        /// Возвращает последний файл по маске в рабочей папке.
        /// </summary>
        /// <param name="mask">Маска поиска файла.</param>
        /// <returns>Абсолютный путь к последнему файлу или пустую строку.</returns>
        private string GetLatestFileByMask(string mask)
        {
            var files = Directory.GetFiles(ServerFilesRoot, mask, SearchOption.TopDirectoryOnly);
            if (files == null || files.Length == 0)
            {
                return string.Empty;
            }

            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            return files[files.Length - 1];
        }

        /// <summary>
        /// Проверяет, что файл относится к текущему этапу.
        /// </summary>
        /// <param name="fileName">Имя файла без пути.</param>
        /// <param name="titleCode">Код титула T1/T2/T3/T4.</param>
        /// <returns>True, если файл относится к ожидаемому этапу.</returns>
        private bool IsStageServerFile(string fileName, string titleCode)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return false;
            }

            var lower = fileName.ToLowerInvariant();
            switch ((titleCode ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "T1":
                    return lower.StartsWith("t1_") || lower.StartsWith("on_trnaclgrot");
                case "T2":
                    return lower.StartsWith("t2_") || lower.StartsWith("on_trnaclpprin");
                case "T3":
                    return lower.StartsWith("t3_");
                case "T4":
                    return lower.StartsWith("t4_");
                default:
                    return false;
            }
        }

        /// <summary>
        /// Возвращает последний response-лог оператора для текущего этапа.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код этапа UI.</param>
        /// <returns>Последний ответ оператора или null.</returns>
        private KonturRawLogRepository.LastResponseLog GetLastStageResponseLog(long timelineId, string stageCode)
        {
            return new KonturRawLogRepository(ConnectionString).GetLastResponseLog(
                timelineId,
                "Kontur",
                NormalizeStageForLog(stageCode));
        }

        /// <summary>
        /// Преобразует код этапа UI в код этапа, используемый в raw-логе.
        /// </summary>
        /// <param name="stageCode">Код этапа из UI.</param>
        /// <returns>Нормализованный код этапа для поиска в raw-логе.</returns>
        private string NormalizeStageForLog(string stageCode)
        {
            var normalizedTitle = StageToTitle(stageCode);
            return string.Equals(normalizedTitle, "T1", StringComparison.OrdinalIgnoreCase)
                ? "T1_INITIAL"
                : normalizedTitle;
        }

        /// <summary>
        /// Формирует краткий текст результата по последнему ответу оператора.
        /// </summary>
        /// <param name="lastResponse">Последний response-лог этапа.</param>
        /// <returns>Короткий текст для блока результата.</returns>
        private string BuildResultSummaryText(KonturRawLogRepository.LastResponseLog lastResponse)
        {
            if (lastResponse == null)
            {
                return "Результат отсутствует.";
            }

            var summary = "HTTP " + (lastResponse.HttpStatus.HasValue ? lastResponse.HttpStatus.Value.ToString() : "<null>");
            var traceId = ExtractValue(lastResponse.SanitizedPayload, "traceId");
            var errorCode = ExtractValue(lastResponse.SanitizedPayload, "code");
            var errorMessage = ExtractValue(lastResponse.SanitizedPayload, "message");

            if (!string.IsNullOrEmpty(errorCode))
            {
                summary += "; code=" + errorCode;
            }

            if (!string.IsNullOrEmpty(errorMessage))
            {
                summary += "; message=" + errorMessage;
            }

            if (!string.IsNullOrEmpty(traceId))
            {
                summary += "; traceId=" + traceId;
            }

            return summary;
        }

        /// <summary>
        /// Формирует короткий текст ошибки для шага результата.
        /// </summary>
        /// <param name="lastResponse">Последний response-лог этапа.</param>
        /// <returns>Короткий текст ошибки оператора.</returns>
        private string BuildResultErrorText(KonturRawLogRepository.LastResponseLog lastResponse)
        {
            if (lastResponse == null)
            {
                return "Результат отсутствует";
            }

            var errorCode = ExtractValue(lastResponse.SanitizedPayload, "code");
            if (!string.IsNullOrEmpty(errorCode))
            {
                return errorCode;
            }

            return lastResponse.HttpStatus.HasValue ? ("HTTP " + lastResponse.HttpStatus.Value) : "Ошибка оператора";
        }

        /// <summary>
        /// Извлекает значение поля из строкового sanitized payload без тяжелого JSON-парсинга.
        /// </summary>
        /// <param name="payload">Строка sanitized payload из raw-лога.</param>
        /// <param name="fieldName">Имя поля.</param>
        /// <returns>Найденное значение или пустую строку.</returns>
        /// <remarks>
        /// В raw-логе значения уже часто лежат в форме key=value или как вкрапления JSON, поэтому здесь достаточно
        /// легкого поиска по строке без повторного разбора всей структуры.
        /// </remarks>
        private string ExtractValue(string payload, string fieldName)
        {
            var text = payload ?? string.Empty;
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(fieldName))
            {
                return string.Empty;
            }

            var keyPosition = text.IndexOf(fieldName, StringComparison.OrdinalIgnoreCase);
            if (keyPosition < 0)
            {
                return string.Empty;
            }

            var valueStart = text.IndexOfAny(new[] { '=', ':' }, keyPosition);
            if (valueStart < 0 || valueStart + 1 >= text.Length)
            {
                return string.Empty;
            }

            var slice = text.Substring(valueStart + 1).Trim().Trim('"');
            var delimiterIndex = slice.IndexOfAny(new[] { ';', ',', '\r', '\n', '}' });
            if (delimiterIndex >= 0)
            {
                slice = slice.Substring(0, delimiterIndex);
            }

            return slice.Trim().Trim('"');
        }

        /// <summary>
        /// Строит состояние шага с признаком готовности.
        /// </summary>
        /// <param name="text">Короткий текст состояния.</param>
        /// <returns>Объект состояния шага для UI.</returns>
        private KonturProbeFlowStepState BuildReadyStepState(string text)
        {
            return new KonturProbeFlowStepState
            {
                CssClass = "step-state step-state-ready",
                Text = text
            };
        }

        /// <summary>
        /// Строит состояние шага с предупреждением.
        /// </summary>
        /// <param name="text">Короткий текст состояния.</param>
        /// <returns>Объект состояния шага для UI.</returns>
        private KonturProbeFlowStepState BuildWarnStepState(string text)
        {
            return new KonturProbeFlowStepState
            {
                CssClass = "step-state step-state-warn",
                Text = text
            };
        }

        /// <summary>
        /// Строит состояние шага с ошибкой.
        /// </summary>
        /// <param name="text">Короткий текст состояния.</param>
        /// <returns>Объект состояния шага для UI.</returns>
        private KonturProbeFlowStepState BuildErrorStepState(string text)
        {
            return new KonturProbeFlowStepState
            {
                CssClass = "step-state step-state-error",
                Text = text
            };
        }
    }
}
