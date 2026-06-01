/*
  ФАЙЛ: KonturStageSignerService.cs
  НАЗНАЧЕНИЕ: Сервис выбора и разрешения подписанта этапа ЭТрН Контур.
  Выступает общей точкой истины по связке TimelineId + StageCode -> допустимые кандидаты, сохраненный выбор и TFizLico подписанта.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  22.05.2026 - Первичное создание сервиса разрешения подписанта этапа.
  22.05.2026 - Разрешение idzak переведено на прямое чтение из epd_timeline, чтобы ручные timeline работали без зависимости от EpdRepo.
*/

using System;
using System.Data.SqlClient;
using Tis.KonturIntegration.Models;
using Tis.KonturIntegration.Storage;

namespace Tis.KonturIntegration.Integration
{
    /// <summary>
    /// Сервис выбора и разрешения подписанта этапа ЭТрН Контур.
    /// </summary>
    public class KonturStageSignerService
    {
        /// <summary>
        /// Инициализирует сервис строкой подключения к ТИС.
        /// </summary>
        /// <param name="connectionString">Строка подключения к основной БД ТИС и Perdoc.</param>
        /// <remarks>Сервис собирает SQL-зависимости один раз и переиспользует их в каждом вызове.</remarks>
        public KonturStageSignerService(string connectionString)
        {
            ConnectionString = connectionString;
            StageSignerRepository = new KonturStageSignerRepository(connectionString);
        }

        /// <summary>
        /// Получает строку подключения, используемую сервисом.
        /// </summary>
        public string ConnectionString { get; private set; }

        /// <summary>
        /// Получает репозиторий кандидатов и сохраненного выбора подписанта.
        /// </summary>
        public KonturStageSignerRepository StageSignerRepository { get; private set; }

        /// <summary>
        /// Строит контекст выбора подписанта для этапа по timeline.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код этапа T1/T2/T3/T4.</param>
        /// <returns>Контекст роли этапа, организации и допустимых кандидатов.</returns>
        /// <remarks>
        /// Метод сначала восстанавливает idzak из timeline, а затем delegирует SQL-логику
        /// репозиторию кандидатов. Это устраняет зависимость UI от прямых SQL-проверок.
        /// </remarks>
        public KonturStageSignerContext GetContext(long timelineId, string stageCode)
        {
            var normalizedStageCode = NormalizeStageCode(stageCode);
            var idzak = ResolveIdzakByTimeline(timelineId);
            if (idzak <= 0)
            {
                return new KonturStageSignerContext
                {
                    TitleCode = normalizedStageCode,
                    ErrorMessage = "Для выбранного TimelineId не найден idzak, поэтому список подписантов недоступен."
                };
            }

            return StageSignerRepository.GetContext(idzak, normalizedStageCode);
        }

        /// <summary>
        /// Возвращает сохраненный выбор подписанта для этапа.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код этапа T1/T2/T3/T4.</param>
        /// <returns>Сохраненный выбор или null.</returns>
        public KonturStageSignerSelection GetSelection(long timelineId, string stageCode)
        {
            return StageSignerRepository.GetSelection(timelineId, NormalizeStageCode(stageCode));
        }

        /// <summary>
        /// Сохраняет выбранного подписанта этапа после проверки его допустимости.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код этапа T1/T2/T3/T4.</param>
        /// <param name="signerFizLicoId">Идентификатор TFizLico выбранного подписанта.</param>
        /// <param name="updatedByUserId">Идентификатор оператора, сохранившего выбор.</param>
        /// <remarks>
        /// Сервис валидирует выбранного подписанта против актуального списка полномочий,
        /// чтобы в БД не сохранялся произвольный TFizLico вне роли этапа.
        /// </remarks>
        public void SaveSelection(long timelineId, string stageCode, long signerFizLicoId, long updatedByUserId)
        {
            var context = GetContext(timelineId, stageCode);
            if (!context.IsResolved)
            {
                throw new ApplicationException(context.ErrorMessage ?? "Не удалось определить контекст подписанта этапа.");
            }

            if (context.Candidates.Count == 0)
            {
                throw new ApplicationException("Для этапа " + NormalizeStageCode(stageCode) + " не найдено допустимых подписантов.");
            }

            var candidate = FindCandidateBySignerId(context, signerFizLicoId);
            if (candidate == null)
            {
                throw new ApplicationException(
                    "Выбранный подписант не входит в актуальный список полномочий этапа." + Environment.NewLine +
                    "stage=" + NormalizeStageCode(stageCode) + Environment.NewLine +
                    "signerId=" + signerFizLicoId);
            }

            StageSignerRepository.SaveSelection(new KonturStageSignerSelection
            {
                TimelineId = timelineId,
                StageCode = NormalizeStageCode(stageCode),
                SignerFizLicoId = signerFizLicoId,
                UpdatedByUserId = updatedByUserId,
                UpdatedAt = DateTime.Now
            });
        }

        /// <summary>
        /// Удаляет сохраненный выбор подписанта для этапа.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код этапа T1/T2/T3/T4.</param>
        public void DeleteSelection(long timelineId, string stageCode)
        {
            StageSignerRepository.DeleteSelection(timelineId, NormalizeStageCode(stageCode));
        }

        /// <summary>
        /// Возвращает выбранного и допустимого подписанта этапа или вызывает ошибку.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код этапа T1/T2/T3/T4.</param>
        /// <returns>Выбранный допустимый подписант этапа.</returns>
        /// <remarks>
        /// Метод является основной точкой контроля бизнес-правила: без явного выбора
        /// допустимого подписанта сборка XML и запуск этапа запрещены.
        /// </remarks>
        public KonturStageSignerCandidate ResolveSelectedSigner(long timelineId, string stageCode)
        {
            var normalizedStageCode = NormalizeStageCode(stageCode);
            var context = GetContext(timelineId, normalizedStageCode);
            if (!context.IsResolved)
            {
                throw new ApplicationException(context.ErrorMessage ?? ("Не удалось определить контекст подписанта для этапа " + normalizedStageCode + "."));
            }

            if (context.Candidates.Count == 0)
            {
                throw new ApplicationException(
                    "Для этапа " + normalizedStageCode + " не найдено допустимых подписантов." + Environment.NewLine +
                    "role=" + Safe(context.RequiredRoleName) + Environment.NewLine +
                    "orgId=" + context.RequiredKontragentId + Environment.NewLine +
                    "orgName=" + Safe(context.RequiredKontragentName));
            }

            var selection = GetSelection(timelineId, normalizedStageCode);
            if (selection == null || selection.SignerFizLicoId <= 0)
            {
                throw new ApplicationException(
                    "Для этапа " + normalizedStageCode + " не выбран подписант." + Environment.NewLine +
                    "role=" + Safe(context.RequiredRoleName) + Environment.NewLine +
                    "orgName=" + Safe(context.RequiredKontragentName) + Environment.NewLine +
                    "action=Выберите подписанта в блоке подписи перед формированием или отправкой этапа.");
            }

            var candidate = FindCandidateBySignerId(context, selection.SignerFizLicoId);
            if (candidate != null)
            {
                return candidate;
            }

            throw new ApplicationException(
                "Выбранный подписант этапа больше не входит в допустимый список." + Environment.NewLine +
                "stage=" + normalizedStageCode + Environment.NewLine +
                "signerId=" + selection.SignerFizLicoId + Environment.NewLine +
                "action=Выберите нового подписанта в актуальном списке полномочий.");
        }

        /// <summary>
        /// Пытается вернуть выбранного подписанта без генерации ошибки.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код этапа T1/T2/T3/T4.</param>
        /// <returns>Выбранный подписант или null, если выбор пока недоступен.</returns>
        /// <remarks>Используется в мягких сценариях UI, где достаточно предупредить оператора.</remarks>
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
        /// Возвращает TFizLico.id выбранного подписанта этапа.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код этапа T1/T2/T3/T4.</param>
        /// <returns>Идентификатор TFizLico выбранного подписанта.</returns>
        public int ResolveSigningUserId(long timelineId, string stageCode)
        {
            var candidate = ResolveSelectedSigner(timelineId, stageCode);
            return (int)candidate.SignerFizLicoId;
        }

        /// <summary>
        /// Восстанавливает idzak из timeline документа.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <returns>Идентификатор заявки ТИС или 0, если он не определен.</returns>
        /// <remarks>
        /// Сервис читает idzak напрямую из Perdoc.dbo.epd_timeline и только затем применяет fallback
        /// на числовой tis_entity_id. Такой порядок нужен, чтобы ручные и тестовые timeline не зависели
        /// от стороннего helper-метода и разрешались одинаково во всех сценариях.
        /// </remarks>
        public long ResolveIdzakByTimeline(long timelineId)
        {
            var timelineIdentity = GetTimelineIdentity(timelineId);
            if (timelineIdentity.Idzak > 0)
            {
                return timelineIdentity.Idzak;
            }

            long parsed;
            if (long.TryParse((timelineIdentity.TisEntityId ?? string.Empty).Trim(), out parsed) && parsed > 0)
            {
                return parsed;
            }

            return 0;
        }

        /// <summary>
        /// Возвращает идентификационные поля timeline из epd_timeline.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <returns>Объект с idzak и tis_entity_id из строки timeline.</returns>
        /// <remarks>
        /// Единое чтение двух полей исключает рассинхрон между разными источниками и упрощает
        /// поддержку ручных timeline, созданных вне штатной страницы ЭТрН.
        /// </remarks>
        private TimelineIdentityInfo GetTimelineIdentity(long timelineId)
        {
            const string sql = @"
SELECT TOP (1)
       ISNULL(idzak, 0) AS idzak,
       RTRIM(ISNULL(tis_entity_id, '')) AS tis_entity_id
  FROM Perdoc.dbo.epd_timeline
 WHERE id = @timelineId;";

            using (var connection = new SqlConnection(ConnectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@timelineId", timelineId);
                connection.Open();

                var scalar = command.ExecuteScalar();
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return new TimelineIdentityInfo();
                    }

                    return new TimelineIdentityInfo
                    {
                        Idzak = reader.IsDBNull(0) ? 0 : Convert.ToInt64(reader.GetValue(0)),
                        TisEntityId = reader.IsDBNull(1) ? string.Empty : Convert.ToString(reader.GetValue(1))
                    };
                }
            }
        }

        /// <summary>
        /// Хранит идентификационные поля строки timeline для дальнейшего разрешения idzak.
        /// </summary>
        private sealed class TimelineIdentityInfo
        {
            /// <summary>
            /// Получает или задает idzak, сохраненный в epd_timeline.
            /// </summary>
            public long Idzak { get; set; }

            /// <summary>
            /// Получает или задает tis_entity_id, сохраненный в epd_timeline.
            /// </summary>
            public string TisEntityId { get; set; }
        }

        /// <summary>
        /// Нормализует код этапа к форме T1/T2/T3/T4.
        /// </summary>
        /// <param name="stageCode">Исходный код этапа.</param>
        /// <returns>Нормализованный код этапа.</returns>
        private string NormalizeStageCode(string stageCode)
        {
            var normalized = (stageCode ?? string.Empty).Trim().ToUpperInvariant();
            if (normalized == "T1_INITIAL" || normalized == "T1_DRAFT")
            {
                return "T1";
            }

            return normalized;
        }

        /// <summary>
        /// Ищет кандидата по идентификатору TFizLico.
        /// </summary>
        /// <param name="context">Контекст допустимых кандидатов этапа.</param>
        /// <param name="signerFizLicoId">Идентификатор TFizLico кандидата.</param>
        /// <returns>Найденный кандидат или null.</returns>
        private KonturStageSignerCandidate FindCandidateBySignerId(KonturStageSignerContext context, long signerFizLicoId)
        {
            if (context == null || context.Candidates == null)
            {
                return null;
            }

            for (var i = 0; i < context.Candidates.Count; i++)
            {
                var candidate = context.Candidates[i];
                if (candidate.SignerFizLicoId == signerFizLicoId)
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Безопасно преобразует строку к отображаемому значению.
        /// </summary>
        /// <param name="value">Исходная строка.</param>
        /// <returns>Обрезанное значение или пустую строку.</returns>
        private string Safe(string value)
        {
            return (value ?? string.Empty).Trim();
        }
    }
}
