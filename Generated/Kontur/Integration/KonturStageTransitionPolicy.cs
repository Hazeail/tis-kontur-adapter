/*
  ФАЙЛ: KonturStageTransitionPolicy.cs
  НАЗНАЧЕНИЕ: Правила готовности этапов Контур ЭТрН к запуску.
  Фиксирует переходы T1/T2/T3/T4 поверх явного состояния этапов без обращения к UI и операторному API.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  29.05.2026 - Первичное создание policy готовности этапов перед вводом send use case-ов.
*/

using System;
using Tis.KonturIntegration.Models;
using Tis.KonturIntegration.Storage;

namespace Tis.KonturIntegration.Integration
{
    /// <summary>
    /// Проверяет готовность этапа Контур ЭТрН к запуску по явному состоянию процесса.
    /// Используется как будущая защита send use case-ов от преждевременного запуска ответных титулов.
    /// </summary>
    public class KonturStageTransitionPolicy
    {
        /// <summary>
        /// Инициализирует policy repository явного состояния этапов.
        /// </summary>
        /// <param name="stageStateRepository">Repository явного состояния этапов.</param>
        /// <remarks>Policy читает только сохраненное состояние и не вычисляет готовность по raw-log или файлам.</remarks>
        public KonturStageTransitionPolicy(IKonturStageStateRepository stageStateRepository)
        {
            if (stageStateRepository == null)
            {
                throw new ArgumentNullException("stageStateRepository");
            }

            StageStateRepository = stageStateRepository;
        }

        /// <summary>
        /// Получает repository явного состояния этапов.
        /// </summary>
        public IKonturStageStateRepository StageStateRepository { get; private set; }

        /// <summary>
        /// Проверяет, можно ли запускать выбранный этап.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа в ТИС.</param>
        /// <param name="stageCode">Код этапа UI или сценария, например T1_INITIAL, T2, T3 или T4.</param>
        /// <returns>Решение о готовности этапа к запуску с диагностикой.</returns>
        /// <remarks>
        /// Для T1 проверяется собственная подготовка XML и подписи.
        /// Для T2/T3/T4 дополнительно требуется подтвержденный предыдущий этап с разрешением следующего шага.
        /// </remarks>
        public KonturStageTransitionDecision CanStart(long timelineId, string stageCode)
        {
            var normalizedStageCode = NormalizeStageCode(stageCode);
            var titleCode = StageToTitle(normalizedStageCode);

            if (!IsSupportedTitle(titleCode))
            {
                return Deny(normalizedStageCode, titleCode, string.Empty, "UnsupportedStage", "Этап не поддерживается моделью переходов Контур ЭТрН.");
            }

            var currentState = StageStateRepository.Get(timelineId, normalizedStageCode);
            var currentCheck = CheckCurrentStageReady(normalizedStageCode, titleCode, currentState);
            if (!currentCheck.IsAllowed)
            {
                return currentCheck;
            }

            if (string.Equals(titleCode, "T1", StringComparison.OrdinalIgnoreCase))
            {
                return Allow(normalizedStageCode, titleCode, string.Empty, "StageReady", "T1 подготовлен к запуску первого титула.");
            }

            var previousStageCode = ResolvePreviousStageCode(timelineId, normalizedStageCode);
            var previousState = StageStateRepository.Get(timelineId, previousStageCode);
            if (previousState == null)
            {
                return Deny(normalizedStageCode, titleCode, previousStageCode, "PreviousStageStateMissing", "Предыдущий этап не имеет явного состояния.");
            }

            if (!previousState.Completed || !previousState.NextStageAllowed)
            {
                return Deny(normalizedStageCode, titleCode, previousStageCode, "PreviousStageNotConfirmed", "Предыдущий этап еще не подтвержден как завершенный и не разрешает следующий шаг.");
            }

            return Allow(normalizedStageCode, titleCode, previousStageCode, "StageReady", "Этап подготовлен к запуску ответного титула.");
        }

        /// <summary>
        /// Проверяет собственную готовность текущего этапа.
        /// </summary>
        /// <param name="stageCode">Нормализованный код этапа.</param>
        /// <param name="titleCode">Код титула этапа.</param>
        /// <param name="state">Сохраненное состояние этапа.</param>
        /// <returns>Решение по собственной готовности этапа.</returns>
        /// <remarks>Проверка не смотрит на внешний TransportationId, потому что он не заменяет состояние процесса.</remarks>
        private KonturStageTransitionDecision CheckCurrentStageReady(string stageCode, string titleCode, KonturStageState state)
        {
            if (state == null)
            {
                return Deny(stageCode, titleCode, string.Empty, "StageStateMissing", "Явное состояние этапа не найдено.");
            }

            if (!state.XmlBuilt)
            {
                return Deny(stageCode, titleCode, string.Empty, "XmlNotBuilt", "XML этапа еще не сформирован.");
            }

            if (!state.SignatureImported)
            {
                return Deny(stageCode, titleCode, string.Empty, "SignatureNotImported", "Подпись этапа еще не импортирована.");
            }

            if (state.Sent || state.Completed)
            {
                return Deny(stageCode, titleCode, string.Empty, "StageAlreadyStarted", "Этап уже был отправлен или завершен; повторный запуск требует новой сборки XML.");
            }

            return Allow(stageCode, titleCode, string.Empty, "CurrentStageReady", "Текущий этап подготовлен.");
        }

        /// <summary>
        /// Определяет предыдущий этап для проверки ответного титула.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Нормализованный код текущего этапа.</param>
        /// <returns>Код предыдущего этапа или пустую строку.</returns>
        /// <remarks>Для T2 учитывается фактический T1-этап, сохраненный в явном состоянии.</remarks>
        private string ResolvePreviousStageCode(long timelineId, string stageCode)
        {
            if (string.Equals(stageCode, "T2", StringComparison.OrdinalIgnoreCase))
            {
                if (IsPreviousStageConfirmed(timelineId, "T1_INITIAL"))
                {
                    return "T1_INITIAL";
                }

                return "T1_DRAFT";
            }

            if (string.Equals(stageCode, "T3", StringComparison.OrdinalIgnoreCase))
            {
                return "T2";
            }

            if (string.Equals(stageCode, "T4", StringComparison.OrdinalIgnoreCase))
            {
                return "T3";
            }

            return string.Empty;
        }

        /// <summary>
        /// Проверяет, подтвержден ли указанный предыдущий этап.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код предыдущего этапа.</param>
        /// <returns>True, если этап завершен и разрешил следующий шаг.</returns>
        /// <remarks>Метод нужен для выбора между T1_INITIAL и T1_DRAFT как источником разрешения T2.</remarks>
        private bool IsPreviousStageConfirmed(long timelineId, string stageCode)
        {
            var state = StageStateRepository.Get(timelineId, stageCode);
            return state != null && state.Completed && state.NextStageAllowed;
        }

        /// <summary>
        /// Формирует положительное решение перехода.
        /// </summary>
        /// <param name="stageCode">Код текущего этапа.</param>
        /// <param name="titleCode">Код титула текущего этапа.</param>
        /// <param name="previousStageCode">Код предыдущего этапа, если он участвовал в проверке.</param>
        /// <param name="reasonCode">Код причины решения.</param>
        /// <param name="message">Диагностическое сообщение.</param>
        /// <returns>Положительное решение перехода.</returns>
        /// <remarks>Единый конструктор результата сохраняет одинаковый формат диагностики.</remarks>
        private KonturStageTransitionDecision Allow(string stageCode, string titleCode, string previousStageCode, string reasonCode, string message)
        {
            return BuildDecision(true, stageCode, titleCode, previousStageCode, reasonCode, message);
        }

        /// <summary>
        /// Формирует отрицательное решение перехода.
        /// </summary>
        /// <param name="stageCode">Код текущего этапа.</param>
        /// <param name="titleCode">Код титула текущего этапа.</param>
        /// <param name="previousStageCode">Код предыдущего этапа, если он участвовал в проверке.</param>
        /// <param name="reasonCode">Код причины решения.</param>
        /// <param name="message">Диагностическое сообщение.</param>
        /// <returns>Отрицательное решение перехода.</returns>
        /// <remarks>Отказ возвращается как result-объект, чтобы use case не ловил штатные исключения готовности.</remarks>
        private KonturStageTransitionDecision Deny(string stageCode, string titleCode, string previousStageCode, string reasonCode, string message)
        {
            return BuildDecision(false, stageCode, titleCode, previousStageCode, reasonCode, message);
        }

        /// <summary>
        /// Собирает решение перехода этапа.
        /// </summary>
        /// <param name="isAllowed">Признак разрешения запуска.</param>
        /// <param name="stageCode">Код текущего этапа.</param>
        /// <param name="titleCode">Код титула текущего этапа.</param>
        /// <param name="previousStageCode">Код предыдущего этапа, если он участвовал в проверке.</param>
        /// <param name="reasonCode">Код причины решения.</param>
        /// <param name="message">Диагностическое сообщение.</param>
        /// <returns>Решение перехода этапа.</returns>
        /// <remarks>Метод не содержит бизнес-правил и только нормализует возвращаемую модель.</remarks>
        private KonturStageTransitionDecision BuildDecision(bool isAllowed, string stageCode, string titleCode, string previousStageCode, string reasonCode, string message)
        {
            return new KonturStageTransitionDecision
            {
                IsAllowed = isAllowed,
                StageCode = stageCode,
                TitleCode = titleCode,
                RequiredPreviousStageCode = previousStageCode,
                ReasonCode = reasonCode,
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
        /// <remarks>Разделение stage-code и title-code сохраняет отличие запуска T1 от завершения первого титула.</remarks>
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

        /// <summary>
        /// Проверяет, поддерживается ли код титула моделью переходов.
        /// </summary>
        /// <param name="titleCode">Код титула.</param>
        /// <returns>True, если титул входит в процесс T1/T2/T3/T4.</returns>
        /// <remarks>Неизвестные этапы блокируются до операторного вызова.</remarks>
        private bool IsSupportedTitle(string titleCode)
        {
            return string.Equals(titleCode, "T1", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(titleCode, "T2", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(titleCode, "T3", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(titleCode, "T4", StringComparison.OrdinalIgnoreCase);
        }
    }
}
