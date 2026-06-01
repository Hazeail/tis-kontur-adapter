/*
  ФАЙЛ: KonturAdapterRecipientTitleGateway.cs
  НАЗНАЧЕНИЕ: Adapter-backed реализация порта отправки ответных титулов T2/T3/T4.
  Оборачивает текущий KonturAdapter без переписывания существующего API-кода.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  29.05.2026 - Первичное создание реализации gateway-порта ответных титулов.
*/

using System;
using Tis.KonturIntegration.Models;
using KonturApiAdapter = Tis.KonturIntegration.KonturAdapter.KonturAdapter;

namespace Tis.KonturIntegration.Integration
{
    /// <summary>
    /// Реализует отправку ответных титулов через существующий KonturAdapter.
    /// </summary>
    public class KonturAdapterRecipientTitleGateway : IKonturRecipientTitleGateway
    {
        /// <summary>
        /// Инициализирует gateway текущим адаптером Контур.
        /// </summary>
        /// <param name="adapter">Существующий адаптер операторного слоя.</param>
        /// <remarks>Gateway является тонкой оберткой и не переносит в себя бизнес-логику этапов.</remarks>
        public KonturAdapterRecipientTitleGateway(KonturApiAdapter adapter)
        {
            if (adapter == null)
            {
                throw new ArgumentNullException("adapter");
            }

            Adapter = adapter;
        }

        /// <summary>
        /// Получает существующий адаптер Контур, используемый как внешний adapter-слой.
        /// </summary>
        public KonturApiAdapter Adapter { get; private set; }

        /// <summary>
        /// Отправляет ответный титул T2, T3 или T4 в существующий операторный документооборот.
        /// </summary>
        /// <param name="titleCode">Код титула: T2, T3 или T4.</param>
        /// <param name="timelineId">Идентификатор timeline документа в ТИС.</param>
        /// <param name="xmlPath">Путь к XML-файлу ответного титула.</param>
        /// <param name="signaturePath">Путь к detached-подписи ответного титула.</param>
        /// <param name="senderBoxId">DiadocBoxId отправителя, выбранный для этапа.</param>
        /// <returns>Унифицированный результат отправки этапа.</returns>
        /// <remarks>Выбор конкретного метода адаптера локализован здесь, чтобы use case не зависел от большого KonturAdapter.</remarks>
        public KonturStageExecutionResult SendRecipientTitle(string titleCode, long timelineId, string xmlPath, string signaturePath, string senderBoxId)
        {
            var normalizedTitleCode = NormalizeTitleCode(titleCode);

            if (string.Equals(normalizedTitleCode, "T2", StringComparison.OrdinalIgnoreCase))
            {
                return MapT2(Adapter.StartDanaflexT2(timelineId, xmlPath, signaturePath, senderBoxId));
            }

            if (string.Equals(normalizedTitleCode, "T3", StringComparison.OrdinalIgnoreCase))
            {
                return MapT3(Adapter.StartDanaflexT3(timelineId, xmlPath, signaturePath, senderBoxId));
            }

            if (string.Equals(normalizedTitleCode, "T4", StringComparison.OrdinalIgnoreCase))
            {
                return MapT4(Adapter.StartDanaflexT4(timelineId, xmlPath, signaturePath, senderBoxId));
            }

            return new KonturStageExecutionResult
            {
                IsSuccess = false,
                StageCode = normalizedTitleCode,
                TimelineId = timelineId,
                Message = "UnsupportedRecipientTitle: " + normalizedTitleCode
            };
        }

        /// <summary>
        /// Нормализует код ответного титула.
        /// </summary>
        /// <param name="titleCode">Исходный код титула.</param>
        /// <returns>Код титула в верхнем регистре или пустую строку.</returns>
        /// <remarks>Нормализация защищает use case от различий регистра в UI-командах.</remarks>
        private string NormalizeTitleCode(string titleCode)
        {
            return string.IsNullOrEmpty(titleCode) ? string.Empty : titleCode.Trim().ToUpperInvariant();
        }

        /// <summary>
        /// Преобразует результат T2 к единой модели этапа.
        /// </summary>
        /// <param name="result">Результат текущего T2-метода адаптера.</param>
        /// <returns>Унифицированный результат выполнения этапа.</returns>
        /// <remarks>Маппинг нужен как временный адаптер между legacy-результатом и новой моделью use case.</remarks>
        private KonturStageExecutionResult MapT2(KonturT2ExecutionResult result)
        {
            if (result == null)
            {
                return Fail("T2", 0, "T2GatewayReturnedNull");
            }

            return new KonturStageExecutionResult
            {
                IsSuccess = result.IsSuccess,
                StageCode = "T2",
                TimelineId = result.TimelineId,
                TransportationId = result.TransportationId,
                TitleId = result.TitleId,
                Message = result.Message
            };
        }

        /// <summary>
        /// Преобразует результат T3 к единой модели этапа.
        /// </summary>
        /// <param name="result">Результат текущего T3-метода адаптера.</param>
        /// <returns>Унифицированный результат выполнения этапа.</returns>
        /// <remarks>Маппинг нужен как временный адаптер между legacy-результатом и новой моделью use case.</remarks>
        private KonturStageExecutionResult MapT3(KonturT3ExecutionResult result)
        {
            if (result == null)
            {
                return Fail("T3", 0, "T3GatewayReturnedNull");
            }

            return new KonturStageExecutionResult
            {
                IsSuccess = result.IsSuccess,
                StageCode = "T3",
                TimelineId = result.TimelineId,
                TransportationId = result.TransportationId,
                TitleId = result.TitleId,
                Message = result.Message
            };
        }

        /// <summary>
        /// Преобразует результат T4 к единой модели этапа.
        /// </summary>
        /// <param name="result">Результат текущего T4-метода адаптера.</param>
        /// <returns>Унифицированный результат выполнения этапа.</returns>
        /// <remarks>Маппинг нужен как временный адаптер между legacy-результатом и новой моделью use case.</remarks>
        private KonturStageExecutionResult MapT4(KonturT4ExecutionResult result)
        {
            if (result == null)
            {
                return Fail("T4", 0, "T4GatewayReturnedNull");
            }

            return new KonturStageExecutionResult
            {
                IsSuccess = result.IsSuccess,
                StageCode = "T4",
                TimelineId = result.TimelineId,
                TransportationId = result.TransportationId,
                TitleId = result.TitleId,
                Message = result.Message
            };
        }

        /// <summary>
        /// Формирует неуспешный унифицированный результат.
        /// </summary>
        /// <param name="stageCode">Код этапа.</param>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="message">Техническое сообщение ошибки.</param>
        /// <returns>Неуспешный результат выполнения этапа.</returns>
        /// <remarks>Метод используется для защитной обработки пустого результата adapter-слоя.</remarks>
        private KonturStageExecutionResult Fail(string stageCode, long timelineId, string message)
        {
            return new KonturStageExecutionResult
            {
                IsSuccess = false,
                StageCode = stageCode,
                TimelineId = timelineId,
                Message = message
            };
        }
    }
}
