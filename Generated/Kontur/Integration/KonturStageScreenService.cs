/*
  ФАЙЛ: KonturStageScreenService.cs
  НАЗНАЧЕНИЕ: Сервис подготовки экранной модели этапа Контур ЭТрН.
  Соединяет явную модель состояния с текущим fallback read-model без изменения code-behind страницы.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  28.05.2026 - Первичное создание сервиса экранной модели этапа.
*/

using System;
using Tis.KonturIntegration.Models;
using Tis.KonturIntegration.Storage;

namespace Tis.KonturIntegration.Integration
{
    /// <summary>
    /// Формирует read-model для будущего тонкого экрана Контур ЭТрН и отделяет UI от правил выбора источника состояния.
    /// </summary>
    public class KonturStageScreenService
    {
        /// <summary>
        /// Инициализирует сервис зависимостями чтения явного состояния и fallback-диагностики.
        /// </summary>
        /// <param name="stageStateRepository">Repository явного состояния этапа.</param>
        /// <param name="fallbackFlowService">Текущий вычисляемый read-model этапа.</param>
        /// <remarks>
        /// Обе зависимости передаются снаружи, чтобы сервис не создавал SQL-адаптеры и диагностические сервисы внутри себя.
        /// </remarks>
        public KonturStageScreenService(IKonturStageStateRepository stageStateRepository, KonturProbeStageFlowService fallbackFlowService)
        {
            if (stageStateRepository == null)
            {
                throw new ArgumentNullException("stageStateRepository");
            }

            if (fallbackFlowService == null)
            {
                throw new ArgumentNullException("fallbackFlowService");
            }

            StageStateRepository = stageStateRepository;
            FallbackFlowService = fallbackFlowService;
        }

        /// <summary>
        /// Получает repository явного состояния этапа.
        /// </summary>
        public IKonturStageStateRepository StageStateRepository { get; private set; }

        /// <summary>
        /// Получает текущий fallback-сервис вычисляемого состояния.
        /// </summary>
        public KonturProbeStageFlowService FallbackFlowService { get; private set; }

        /// <summary>
        /// Строит экранную модель для выбранного этапа.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа в ТИС.</param>
        /// <param name="stageCode">Код этапа UI или сценария.</param>
        /// <param name="xmlPath">Путь к выбранному XML, если UI уже передал файл.</param>
        /// <param name="signaturePath">Путь к выбранной подписи, если UI уже передал файл.</param>
        /// <returns>Экранная модель с явным состоянием и fallback-диагностикой.</returns>
        /// <remarks>
        /// Метод только читает состояние и не меняет текущий процесс отправки. Это нужно для безопасного сравнения новой модели с существующим UI.
        /// </remarks>
        public KonturStageScreenModel Build(long timelineId, string stageCode, string xmlPath, string signaturePath)
        {
            var normalizedStageCode = NormalizeStageCode(stageCode);
            var explicitState = StageStateRepository.Get(timelineId, normalizedStageCode);
            var fallbackState = FallbackFlowService.BuildState(timelineId, normalizedStageCode, xmlPath, signaturePath);

            // Fallback остается обязательным источником диагностики, пока runtime-путь страницы не переведен на явное состояние.
            var model = new KonturStageScreenModel
            {
                TimelineId = timelineId,
                StageCode = normalizedStageCode,
                TitleCode = ResolveTitleCode(explicitState, normalizedStageCode),
                HasExplicitState = explicitState != null,
                StateSource = explicitState == null ? "FallbackReadModel" : "ExplicitStageState",
                StageState = explicitState,
                FallbackFlowState = fallbackState,
                ResultSummary = BuildResultSummary(explicitState, fallbackState)
            };

            return model;
        }

        /// <summary>
        /// Нормализует код этапа для чтения состояния.
        /// </summary>
        /// <param name="stageCode">Исходный код этапа.</param>
        /// <returns>Код этапа в верхнем регистре или пустая строка.</returns>
        /// <remarks>Нормализация нужна, чтобы экранный слой обращался к тем же ключам, что и repository состояния.</remarks>
        private string NormalizeStageCode(string stageCode)
        {
            return string.IsNullOrEmpty(stageCode) ? string.Empty : stageCode.Trim().ToUpperInvariant();
        }

        /// <summary>
        /// Определяет код титула для экранной модели.
        /// </summary>
        /// <param name="state">Явное состояние этапа, если оно уже сохранено.</param>
        /// <param name="stageCode">Нормализованный код этапа.</param>
        /// <returns>Код титула T1/T2/T3/T4 или исходный код этапа.</returns>
        /// <remarks>Приоритет отдается сохраненному состоянию, чтобы экран показывал тот же title-code, который зафиксировал use case.</remarks>
        private string ResolveTitleCode(KonturStageState state, string stageCode)
        {
            if (state != null && !string.IsNullOrEmpty(state.TitleCode))
            {
                return state.TitleCode;
            }

            return FallbackFlowService.StageToTitle(stageCode);
        }

        /// <summary>
        /// Формирует итоговый текст экранной модели.
        /// </summary>
        /// <param name="state">Явное состояние этапа, если оно уже сохранено.</param>
        /// <param name="fallbackState">Вычисляемое fallback-состояние этапа.</param>
        /// <returns>Текст результата для отображения оператору.</returns>
        /// <remarks>Явное состояние используется только при наличии сохраненной записи, иначе сохраняется прежнее поведение read-model.</remarks>
        private string BuildResultSummary(KonturStageState state, KonturProbeStageFlowState fallbackState)
        {
            if (state == null)
            {
                return fallbackState == null ? string.Empty : fallbackState.ResultSummary;
            }

            if (!string.IsNullOrEmpty(state.LastErrorMessage))
            {
                return state.LastErrorMessage;
            }

            if (!string.IsNullOrEmpty(state.LastOperatorStatus))
            {
                return "Статус оператора: " + state.LastOperatorStatus;
            }

            if (state.Completed)
            {
                return "Этап завершен.";
            }

            if (state.Sent)
            {
                return "Этап отправлен, ожидается подтверждение результата.";
            }

            if (state.SignatureImported)
            {
                return "Подпись этапа подготовлена.";
            }

            if (state.XmlBuilt)
            {
                return "XML этапа сформирован.";
            }

            return "Состояние этапа создано, выполнение еще не начато.";
        }
    }
}
