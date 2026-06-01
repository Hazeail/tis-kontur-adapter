/*
  ФАЙЛ: KonturTestSigningContextService.cs
  НАЗНАЧЕНИЕ: Отдельный сервис тестовых подписантов Kontur-only для этапов ЭТрН.
  Нужен для сценария, в котором подпись выполняется тестовыми сертификатами Контур без включения этих физлиц в штатный реестр подписантов ТИС.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  28.05.2026 - Первичное создание сервиса тестового контекста подписантов Kontur-only.
*/

using System;
using Tis.KonturIntegration.Models;

namespace Tis.KonturIntegration.Integration
{
    /// <summary>
    /// Сервис тестового контекста подписантов Kontur-only по timeline и этапу.
    /// </summary>
    public class KonturTestSigningContextService
    {
        /// <summary>
        /// Инициализирует сервис строкой подключения к ТИС и Perdoc.
        /// </summary>
        /// <param name="connectionString">Строка подключения к SQL Server.</param>
        public KonturTestSigningContextService(string connectionString)
        {
            ConnectionString = connectionString;
            BaseSignerService = new KonturStageSignerService(connectionString);
        }

        /// <summary>
        /// Получает строку подключения, используемую сервисом.
        /// </summary>
        public string ConnectionString { get; private set; }

        /// <summary>
        /// Получает базовый сервис штатного контекста подписантов.
        /// </summary>
        public KonturStageSignerService BaseSignerService { get; private set; }

        /// <summary>
        /// Возвращает контекст тестовых подписантов для этапа.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код этапа T1/T2/T3/T4.</param>
        /// <returns>Контекст роли этапа и списка тестовых кандидатов.</returns>
        /// <remarks>
        /// Базовая роль и организация этапа берутся из штатного сервиса ТИС,
        /// а список кандидатов заменяется тестовыми субъектами Контур.
        /// </remarks>
        public KonturStageSignerContext GetContext(long timelineId, string stageCode)
        {
            var normalizedStageCode = NormalizeStageCode(stageCode);
            var baseContext = BaseSignerService.GetContext(timelineId, normalizedStageCode);
            var context = CloneContext(baseContext, normalizedStageCode);
            if (!context.IsResolved)
            {
                return context;
            }

            var candidate = BuildTestCandidate(context, normalizedStageCode);
            if (candidate == null)
            {
                context.ErrorMessage = "Для этапа " + normalizedStageCode + " тестовый подписант Kontur-only пока не настроен.";
                return context;
            }

            context.Candidates.Add(candidate);
            return context;
        }

        /// <summary>
        /// Возвращает сохраненный выбор подписанта этапа.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код этапа T1/T2/T3/T4.</param>
        /// <returns>Сохраненный выбор или null.</returns>
        public KonturStageSignerSelection GetSelection(long timelineId, string stageCode)
        {
            return BaseSignerService.GetSelection(timelineId, NormalizeStageCode(stageCode));
        }

        /// <summary>
        /// Сохраняет выбор тестового подписанта этапа.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код этапа T1/T2/T3/T4.</param>
        /// <param name="signerFizLicoId">Идентификатор тестового подписанта.</param>
        /// <param name="updatedByUserId">Идентификатор пользователя ТИС, сохраняющего выбор.</param>
        /// <remarks>
        /// Для test-only режима в БД сохраняется тот же format selection, что и для штатного режима,
        /// но с отрицательными идентификаторами виртуальных тестовых подписантов.
        /// </remarks>
        public void SaveSelection(long timelineId, string stageCode, long signerFizLicoId, long updatedByUserId)
        {
            var context = GetContext(timelineId, stageCode);
            if (!context.IsResolved)
            {
                throw new ApplicationException(context.ErrorMessage ?? "Не удалось определить тестовый контекст подписанта этапа.");
            }

            var candidate = FindCandidateBySignerId(context, signerFizLicoId);
            if (candidate == null)
            {
                throw new ApplicationException(
                    "Выбранный тестовый подписант не входит в допустимый список Kontur-only." + Environment.NewLine +
                    "stage=" + NormalizeStageCode(stageCode) + Environment.NewLine +
                    "signerId=" + signerFizLicoId);
            }

            BaseSignerService.StageSignerRepository.SaveSelection(new KonturStageSignerSelection
            {
                TimelineId = timelineId,
                StageCode = NormalizeStageCode(stageCode),
                SignerFizLicoId = signerFizLicoId,
                UpdatedByUserId = updatedByUserId,
                UpdatedAt = DateTime.Now
            });
        }

        /// <summary>
        /// Удаляет сохраненный выбор тестового подписанта этапа.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код этапа T1/T2/T3/T4.</param>
        public void DeleteSelection(long timelineId, string stageCode)
        {
            BaseSignerService.DeleteSelection(timelineId, NormalizeStageCode(stageCode));
        }

        /// <summary>
        /// Возвращает выбранного тестового подписанта этапа или генерирует ошибку.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код этапа T1/T2/T3/T4.</param>
        /// <returns>Выбранный тестовый подписант.</returns>
        public KonturStageSignerCandidate ResolveSelectedSigner(long timelineId, string stageCode)
        {
            var normalizedStageCode = NormalizeStageCode(stageCode);
            var context = GetContext(timelineId, normalizedStageCode);
            if (!context.IsResolved)
            {
                throw new ApplicationException(context.ErrorMessage ?? "Не удалось определить тестовый контекст подписанта этапа.");
            }

            var selection = GetSelection(timelineId, normalizedStageCode);
            if (selection == null)
            {
                throw new ApplicationException(
                    "Для этапа " + normalizedStageCode + " не выбран тестовый подписант Kontur-only." + Environment.NewLine +
                    "action=Выберите тестового подписанта в блоке подписи этапа.");
            }

            var candidate = FindCandidateBySignerId(context, selection.SignerFizLicoId);
            if (candidate != null)
            {
                return candidate;
            }

            throw new ApplicationException(
                "Сохраненный тестовый подписант этапа больше не входит в допустимый список Kontur-only." + Environment.NewLine +
                "stage=" + normalizedStageCode + Environment.NewLine +
                "signerId=" + selection.SignerFizLicoId);
        }

        /// <summary>
        /// Пытается вернуть выбранного тестового подписанта этапа без генерации ошибки.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код этапа T1/T2/T3/T4.</param>
        /// <returns>Выбранный тестовый подписант или null.</returns>
        public KonturStageSignerCandidate TryResolveSelectedSigner(long timelineId, string stageCode)
        {
            try
            {
                return ResolveSelectedSigner(timelineId, stageCode);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Возвращает тестового подписанта по отрицательному идентификатору.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код этапа T1/T2/T3/T4.</param>
        /// <param name="signerFizLicoId">Отрицательный идентификатор тестового кандидата.</param>
        /// <returns>Тестовый подписант или null.</returns>
        public KonturStageSignerCandidate TryResolveSignerById(long timelineId, string stageCode, long signerFizLicoId)
        {
            var context = GetContext(timelineId, stageCode);
            if (!context.IsResolved)
            {
                return null;
            }

            return FindCandidateBySignerId(context, signerFizLicoId);
        }

        /// <summary>
        /// Нормализует код этапа до T1/T2/T3/T4.
        /// </summary>
        /// <param name="stageCode">Код этапа из UI или рантайма.</param>
        /// <returns>Нормализованный код титула.</returns>
        private string NormalizeStageCode(string stageCode)
        {
            var value = (stageCode ?? string.Empty).Trim().ToUpperInvariant();
            if (value == "T1_INITIAL" || value == "T1_DRAFT")
            {
                return "T1";
            }

            return value;
        }

        /// <summary>
        /// Клонирует базовый контекст роли этапа без штатных кандидатов.
        /// </summary>
        /// <param name="baseContext">Штатный контекст роли этапа.</param>
        /// <param name="titleCode">Нормализованный код титула.</param>
        /// <returns>Контекст для тестового режима.</returns>
        private KonturStageSignerContext CloneContext(KonturStageSignerContext baseContext, string titleCode)
        {
            if (baseContext == null)
            {
                return new KonturStageSignerContext
                {
                    TitleCode = titleCode,
                    ErrorMessage = "Штатный контекст подписанта этапа не определен."
                };
            }

            return new KonturStageSignerContext
            {
                TitleCode = string.IsNullOrEmpty(baseContext.TitleCode) ? titleCode : baseContext.TitleCode,
                RequiredRoleCode = baseContext.RequiredRoleCode,
                RequiredRoleName = baseContext.RequiredRoleName,
                RequiredKontragentId = baseContext.RequiredKontragentId,
                RequiredKontragentInn = baseContext.RequiredKontragentInn,
                RequiredKontragentName = baseContext.RequiredKontragentName,
                ErrorMessage = baseContext.ErrorMessage
            };
        }

        /// <summary>
        /// Создает тестового кандидата для указанного этапа.
        /// </summary>
        /// <param name="context">Контекст роли этапа.</param>
        /// <param name="stageCode">Нормализованный код этапа.</param>
        /// <returns>Тестовый кандидат или null.</returns>
        private KonturStageSignerCandidate BuildTestCandidate(KonturStageSignerContext context, string stageCode)
        {
            if (context == null)
            {
                return null;
            }

            if (stageCode == "T1")
            {
                return CreateCandidate(context, -1001, "Соколов Лука Тимофеевич", "635552018474", "Тестовый подписант Контур", "KonturTestCertificate");
            }

            if (stageCode == "T2")
            {
                return CreateCandidate(context, -1002, "Захаров Петр Русланович", "081206022988", "Тестовый подписант Контур", "KonturTestCertificate");
            }

            return null;
        }

        /// <summary>
        /// Создает модель тестового кандидата с наследованием роли и организации этапа.
        /// </summary>
        /// <param name="context">Контекст роли этапа.</param>
        /// <param name="signerId">Отрицательный идентификатор тестового подписанта.</param>
        /// <param name="signerFio">ФИО тестового подписанта.</param>
        /// <param name="signerInnFl">ИНН физлица тестового подписанта.</param>
        /// <param name="position">Отображаемая должность тестового подписанта.</param>
        /// <param name="authoritySource">Источник полномочий тестового подписанта.</param>
        /// <returns>Сконструированный тестовый кандидат.</returns>
        private KonturStageSignerCandidate CreateCandidate(KonturStageSignerContext context, long signerId, string signerFio, string signerInnFl, string position, string authoritySource)
        {
            return new KonturStageSignerCandidate
            {
                TitleCode = context.TitleCode,
                RequiredRoleCode = context.RequiredRoleCode,
                RequiredRoleName = context.RequiredRoleName,
                RequiredKontragentId = context.RequiredKontragentId,
                RequiredKontragentInn = context.RequiredKontragentInn,
                RequiredKontragentName = context.RequiredKontragentName,
                SignerFizLicoId = signerId,
                SignerFio = signerFio,
                Position = position,
                SignerInnFl = signerInnFl,
                AuthoritySource = authoritySource,
                AuthorityDescription = "Тестовый сертификат Контур",
                AuthorityDocType = "KonturTestMode",
                AuthorityDocDate = DateTime.Today
            };
        }

        /// <summary>
        /// Ищет тестового кандидата по идентификатору.
        /// </summary>
        /// <param name="context">Контекст тестовых кандидатов.</param>
        /// <param name="signerFizLicoId">Идентификатор кандидата.</param>
        /// <returns>Кандидат или null.</returns>
        private KonturStageSignerCandidate FindCandidateBySignerId(KonturStageSignerContext context, long signerFizLicoId)
        {
            if (context == null)
            {
                return null;
            }

            for (var index = 0; index < context.Candidates.Count; index++)
            {
                var candidate = context.Candidates[index];
                if (candidate != null && candidate.SignerFizLicoId == signerFizLicoId)
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
