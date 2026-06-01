/*
  ФАЙЛ: BuildStageTitleUseCase.cs
  НАЗНАЧЕНИЕ: Сценарий сборки XML титула этапа Контур ЭТрН.
  Отделяет шаг подготовки XML от UI, подписи, отправки оператору и деталей SQL-хранения состояния.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  29.05.2026 - Добавлен сброс признаков подписи и отправки при пересборке XML, чтобы состояние этапа не ссылалось на старую подготовку.
  28.05.2026 - Первичное создание сценария сборки XML титула этапа.
*/

using System;
using Tis.KonturIntegration.Models;
using Tis.KonturIntegration.Storage;

namespace Tis.KonturIntegration.Integration
{
    /// <summary>
    /// Выполняет прикладной сценарий сборки XML титула и фиксации явного состояния этапа.
    /// Используется как будущая граница между тонким UI и builder-слоем реконструкции Контур ЭТрН.
    /// </summary>
    public class BuildStageTitleUseCase
    {
        /// <summary>
        /// Инициализирует сценарий зависимостями сборки XML, хранения артефактов и состояния этапа.
        /// </summary>
        /// <param name="titleBuilder">Порт сборки XML титула.</param>
        /// <param name="artifactRepository">Repository XML/SGN-артефактов титулов.</param>
        /// <param name="stageStateRepository">Repository явного состояния этапа.</param>
        /// <remarks>Зависимости передаются снаружи, чтобы сценарий не создавал SQL-адаптеры и legacy-builder напрямую.</remarks>
        public BuildStageTitleUseCase(
            IKonturTitleBuilder titleBuilder,
            KonturTitleArtifactRepository artifactRepository,
            IKonturStageStateRepository stageStateRepository)
        {
            if (titleBuilder == null)
            {
                throw new ArgumentNullException("titleBuilder");
            }

            if (artifactRepository == null)
            {
                throw new ArgumentNullException("artifactRepository");
            }

            if (stageStateRepository == null)
            {
                throw new ArgumentNullException("stageStateRepository");
            }

            TitleBuilder = titleBuilder;
            ArtifactRepository = artifactRepository;
            StageStateRepository = stageStateRepository;
        }

        /// <summary>
        /// Получает порт сборки XML титула.
        /// </summary>
        public IKonturTitleBuilder TitleBuilder { get; private set; }

        /// <summary>
        /// Получает repository хранения XML/SGN-артефактов.
        /// </summary>
        public KonturTitleArtifactRepository ArtifactRepository { get; private set; }

        /// <summary>
        /// Получает repository явного состояния этапа.
        /// </summary>
        public IKonturStageStateRepository StageStateRepository { get; private set; }

        /// <summary>
        /// Собирает XML титула выбранного этапа, сохраняет артефакт и фиксирует состояние этапа.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа в ТИС.</param>
        /// <param name="stageCode">Код этапа UI или сценария, например T1_INITIAL, T2, T3 или T4.</param>
        /// <param name="tisEntityId">Идентификатор сущности ТИС, если он нужен конкретному builder.</param>
        /// <returns>Результат сборки XML титула с артефактом или диагностикой ошибки.</returns>
        /// <remarks>
        /// Сценарий не импортирует подпись и не отправляет титул оператору. Он только фиксирует готовность XML как отдельный шаг процесса.
        /// </remarks>
        public KonturTitleBuildResult Execute(long timelineId, string stageCode, string tisEntityId)
        {
            var normalizedStageCode = NormalizeStageCode(stageCode);
            var titleCode = StageToTitle(normalizedStageCode);

            var buildResult = TitleBuilder.Build(timelineId, titleCode, tisEntityId);
            if (buildResult == null)
            {
                SaveFailedState(timelineId, normalizedStageCode, titleCode, "BuildReturnedNull", "Builder вернул пустой результат.");
                return Fail(timelineId, titleCode, "BuildReturnedNull");
            }

            if (!buildResult.IsSuccess || buildResult.Artifact == null || !buildResult.Artifact.HasXml)
            {
                SaveFailedState(timelineId, normalizedStageCode, titleCode, "BuildFailed", buildResult.Message);
                return buildResult;
            }

            buildResult.Artifact.TimelineId = timelineId;
            buildResult.Artifact.TitleCode = titleCode;

            // XML фиксируется в отдельном хранилище артефактов, чтобы состояние процесса не зависело от файлового выбора UI.
            ArtifactRepository.SaveDraftArtifact(buildResult.Artifact);
            SaveXmlBuiltState(timelineId, normalizedStageCode, titleCode);

            return buildResult;
        }

        /// <summary>
        /// Сохраняет состояние успешной сборки XML этапа.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Нормализованный код этапа.</param>
        /// <param name="titleCode">Код титула этапа.</param>
        /// <remarks>Сохранение состояния делает шаг XML явным для будущего экранного слоя и последующих use case.</remarks>
        private void SaveXmlBuiltState(long timelineId, string stageCode, string titleCode)
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
            // Новая версия XML требует новой подписи и новой отправки, иначе этап может использовать устаревший SGN.
            state.SignatureImported = false;
            state.Sent = false;
            state.Completed = false;
            state.NextStageAllowed = false;
            state.LastOperatorStatus = string.Empty;
            state.LastErrorCode = string.Empty;
            state.LastErrorMessage = string.Empty;

            StageStateRepository.Save(state);
        }

        /// <summary>
        /// Сохраняет состояние неуспешной сборки XML этапа.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Нормализованный код этапа.</param>
        /// <param name="titleCode">Код титула этапа.</param>
        /// <param name="errorCode">Код ошибки сценария.</param>
        /// <param name="errorMessage">Диагностическое сообщение ошибки.</param>
        /// <remarks>Даже ошибка сборки фиксируется явно, чтобы UI не восстанавливал причину только по raw-log или файлам.</remarks>
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
            state.XmlBuilt = false;
            // Неуспешная пересборка делает предыдущие признаки готовности небезопасными для следующего шага.
            state.SignatureImported = false;
            state.Sent = false;
            state.Completed = false;
            state.NextStageAllowed = false;
            state.LastOperatorStatus = string.Empty;
            state.LastErrorCode = errorCode;
            state.LastErrorMessage = string.IsNullOrEmpty(errorMessage) ? errorCode : errorMessage;

            StageStateRepository.Save(state);
        }

        /// <summary>
        /// Формирует неуспешный результат сборки XML.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="titleCode">Код титула.</param>
        /// <param name="message">Техническое сообщение ошибки.</param>
        /// <returns>Неуспешный результат builder-сценария.</returns>
        /// <remarks>Метод нужен только для случаев, когда builder не вернул собственный result-объект.</remarks>
        private KonturTitleBuildResult Fail(long timelineId, string titleCode, string message)
        {
            return new KonturTitleBuildResult
            {
                IsSuccess = false,
                TimelineId = timelineId,
                TitleCode = titleCode,
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
