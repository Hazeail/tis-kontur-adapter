/*
  ФАЙЛ: KonturEtrnStageService.cs
  НАЗНАЧЕНИЕ: Единая точка запуска этапов ЭТрН Контур внутри ТИС.
  Инкапсулирует маршрутизацию этапов в конкретные сервисы T1/T2/T3/T4 и возвращает унифицированный результат для UI.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  12.05.2026 - Первичное создание сервиса оркестрации этапов для продуктового UI ТИС.
  13.05.2026 - Добавлен этап T3 в единую маршрутизацию и маппинг результата.
  13.05.2026 - Подключен stage-runner для запуска T3 по внутренним артефактам без ручного XML.
  13.05.2026 - Добавлен этап T4 в единую маршрутизацию и маппинг результата.
*/

using Tis.KonturIntegration.Models;

namespace Tis.KonturIntegration.Integration
{
    /// <summary>
    /// Оркестратор этапов ЭТрН Контур для продуктового запуска из UI ТИС.
    /// </summary>
    public class KonturEtrnStageService
    {
        /// <summary>
        /// Инициализирует сервис строкой подключения к ТИС.
        /// </summary>
        /// <param name="connectionString">Строка подключения к базе данных ТИС.</param>
        /// <remarks>Сервис делегирует отправку в специализированные сервисы T1/T2.</remarks>
        public KonturEtrnStageService(string connectionString)
        {
            ConnectionString = connectionString;
        }

        /// <summary>
        /// Получает строку подключения к ТИС.
        /// </summary>
        public string ConnectionString { get; private set; }

        /// <summary>
        /// Запускает выбранный этап ЭТрН и возвращает унифицированный результат для UI.
        /// </summary>
        /// <param name="stageCode">Код этапа: T1_INITIAL, T1_DRAFT, T2, T3, T4.</param>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="xmlPath">Путь к XML текущего титула.</param>
        /// <param name="signaturePath">Путь к подписи для T2, может быть пустым.</param>
        /// <returns>Унифицированный результат этапа с идентификаторами и сообщением.</returns>
        /// <remarks>Сервисы T2/T3/T4 поддерживают запуск по внутренним артефактам при пустом пути XML.</remarks>
        public KonturStageExecutionResult Execute(string stageCode, long timelineId, string xmlPath, string signaturePath)
        {
            var normalizedStage = (stageCode ?? string.Empty).Trim().ToUpperInvariant();
            var stageRunner = new KonturStageRunner(ConnectionString);

            if (normalizedStage == "T1_INITIAL")
            {
                if (string.IsNullOrEmpty(xmlPath))
                {
                    return stageRunner.ExecuteT1Initial(timelineId, string.Empty);
                }

                var t1 = new KonturT1Service(ConnectionString).Execute(timelineId, xmlPath);
                return MapFromT1(normalizedStage, t1);
            }

            if (normalizedStage == "T1_DRAFT")
            {
                if (string.IsNullOrEmpty(xmlPath))
                {
                    return stageRunner.ExecuteT1Draft(timelineId, string.Empty);
                }

                var t1Draft = new KonturT1Service(ConnectionString).ExecuteDraft(timelineId, xmlPath);
                return MapFromT1(normalizedStage, t1Draft);
            }

            if (normalizedStage == "T2")
            {
                if (string.IsNullOrEmpty(xmlPath))
                {
                    return stageRunner.ExecuteT2(timelineId, string.Empty, signaturePath);
                }

                var t2 = new KonturT2Service(ConnectionString).Execute(timelineId, xmlPath, signaturePath);
                return MapFromT2(normalizedStage, t2);
            }

            if (normalizedStage == "T3")
            {
                if (string.IsNullOrEmpty(xmlPath))
                {
                    return stageRunner.ExecuteT3(timelineId, string.Empty, signaturePath);
                }

                var t3 = new KonturT3Service(ConnectionString).Execute(timelineId, xmlPath, signaturePath);
                return MapFromT3(normalizedStage, t3);
            }

            if (normalizedStage == "T4")
            {
                if (string.IsNullOrEmpty(xmlPath))
                {
                    return stageRunner.ExecuteT4(timelineId, string.Empty, signaturePath);
                }

                var t4 = new KonturT4Service(ConnectionString).Execute(timelineId, xmlPath, signaturePath);
                return MapFromT4(normalizedStage, t4);
            }

            return new KonturStageExecutionResult
            {
                IsSuccess = false,
                StageCode = normalizedStage,
                TimelineId = timelineId,
                Message = "UnsupportedStage"
            };
        }

        /// <summary>
        /// Преобразует результат T1 в унифицированную модель этапа.
        /// </summary>
        /// <param name="stageCode">Код этапа.</param>
        /// <param name="source">Исходный результат T1.</param>
        /// <returns>Унифицированный результат для UI.</returns>
        /// <remarks>Метод выделен отдельно для предсказуемого формата возврата в WebForms.</remarks>
        private KonturStageExecutionResult MapFromT1(string stageCode, KonturT1ExecutionResult source)
        {
            return new KonturStageExecutionResult
            {
                IsSuccess = source != null && source.IsSuccess,
                StageCode = stageCode,
                TimelineId = source != null ? source.TimelineId : 0,
                TransportationId = source != null ? source.TransportationId : string.Empty,
                TitleId = source != null ? source.TitleId : string.Empty,
                Message = source != null ? source.Message : "EmptyT1Result"
            };
        }

        /// <summary>
        /// Преобразует результат T2 в унифицированную модель этапа.
        /// </summary>
        /// <param name="stageCode">Код этапа.</param>
        /// <param name="source">Исходный результат T2.</param>
        /// <returns>Унифицированный результат для UI.</returns>
        /// <remarks>Метод выделен отдельно для предсказуемого формата возврата в WebForms.</remarks>
        private KonturStageExecutionResult MapFromT2(string stageCode, KonturT2ExecutionResult source)
        {
            return new KonturStageExecutionResult
            {
                IsSuccess = source != null && source.IsSuccess,
                StageCode = stageCode,
                TimelineId = source != null ? source.TimelineId : 0,
                TransportationId = source != null ? source.TransportationId : string.Empty,
                TitleId = source != null ? source.TitleId : string.Empty,
                Message = source != null ? source.Message : "EmptyT2Result"
            };
        }

        /// <summary>
        /// Преобразует результат T3 в унифицированную модель этапа.
        /// </summary>
        /// <param name="stageCode">Код этапа.</param>
        /// <param name="source">Исходный результат T3.</param>
        /// <returns>Унифицированный результат для UI.</returns>
        /// <remarks>Метод выделен отдельно для предсказуемого формата возврата в WebForms.</remarks>
        private KonturStageExecutionResult MapFromT3(string stageCode, KonturT3ExecutionResult source)
        {
            return new KonturStageExecutionResult
            {
                IsSuccess = source != null && source.IsSuccess,
                StageCode = stageCode,
                TimelineId = source != null ? source.TimelineId : 0,
                TransportationId = source != null ? source.TransportationId : string.Empty,
                TitleId = source != null ? source.TitleId : string.Empty,
                Message = source != null ? source.Message : "EmptyT3Result"
            };
        }

        /// <summary>
        /// Преобразует результат T4 в унифицированную модель этапа.
        /// </summary>
        /// <param name="stageCode">Код этапа.</param>
        /// <param name="source">Исходный результат T4.</param>
        /// <returns>Унифицированный результат для UI.</returns>
        /// <remarks>Метод выделен отдельно для предсказуемого формата возврата в WebForms.</remarks>
        private KonturStageExecutionResult MapFromT4(string stageCode, KonturT4ExecutionResult source)
        {
            return new KonturStageExecutionResult
            {
                IsSuccess = source != null && source.IsSuccess,
                StageCode = stageCode,
                TimelineId = source != null ? source.TimelineId : 0,
                TransportationId = source != null ? source.TransportationId : string.Empty,
                TitleId = source != null ? source.TitleId : string.Empty,
                Message = source != null ? source.Message : "EmptyT4Result"
            };
        }
    }
}
