/*
  ФАЙЛ: KonturStageCompletionEvidenceRepository.cs
  НАЗНАЧЕНИЕ: SQL-хранилище evidence завершения этапа Контур ЭТрН.
  Сохраняет диагностические признаки отдельно от явного состояния этапа.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  29.05.2026 - Первичное создание SQL repository для TEpdKonturStageCompletionEvidence.
*/

using System;
using System.Data;
using System.Data.SqlClient;
using Tis.KonturIntegration.Models;

namespace Tis.KonturIntegration.Storage
{
    /// <summary>
    /// Реализует хранение evidence завершения этапа в SQL-границе Perdoc.
    /// </summary>
    /// <remarks>Repository не вычисляет решение о завершении, а только сохраняет и читает evidence-снимки.</remarks>
    public class KonturStageCompletionEvidenceRepository : IKonturStageCompletionEvidenceRepository
    {
        /// <summary>
        /// Инициализирует repository строкой подключения.
        /// </summary>
        /// <param name="connectionString">Строка подключения к SQL Server, где доступна схема Perdoc.</param>
        /// <remarks>Соединение открывается только на время отдельной операции.</remarks>
        public KonturStageCompletionEvidenceRepository(string connectionString)
        {
            ConnectionString = connectionString;
        }

        /// <summary>
        /// Получает строку подключения для SQL-операций.
        /// </summary>
        public string ConnectionString { get; private set; }

        /// <summary>
        /// Возвращает последнее evidence по timeline и этапу.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код этапа.</param>
        /// <returns>Последнее evidence или null.</returns>
        /// <remarks>Чтение выполняется по CreatedAt/Id, чтобы увидеть последний результат проверки.</remarks>
        public KonturStageCompletionEvidence GetLatest(long timelineId, string stageCode)
        {
            const string sql = @"
SELECT TOP (1)
       Id,
       TimelineId,
       StageCode,
       TitleCode,
       TransportationId,
       TitleId,
       ExternalDocumentStatus,
       ExternalTitleStatus,
       ExternalActionCode,
       IsDraft,
       HasActiveError,
       HttpStatus,
       RawEvidenceSummary,
       CompletionSource,
       CheckedAt
  FROM Perdoc.dbo.TEpdKonturStageCompletionEvidence WITH (NOLOCK)
 WHERE TimelineId = @TimelineId
   AND StageCode = @StageCode
 ORDER BY Id DESC;";

            using (var cn = new SqlConnection(ConnectionString))
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.Add("@TimelineId", SqlDbType.BigInt).Value = timelineId;
                cmd.Parameters.Add("@StageCode", SqlDbType.NVarChar, 32).Value = NormalizeStageCode(stageCode);

                cn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return null;
                    }

                    return ReadEvidence(reader);
                }
            }
        }

        /// <summary>
        /// Сохраняет новый evidence-снимок.
        /// </summary>
        /// <param name="evidence">Evidence-снимок внешних признаков.</param>
        /// <remarks>Каждая проверка сохраняется отдельной строкой для последующего сравнения прогонов.</remarks>
        public void Save(KonturStageCompletionEvidence evidence)
        {
            if (evidence == null)
            {
                throw new ArgumentNullException("evidence");
            }

            if (evidence.TimelineId <= 0)
            {
                throw new ArgumentOutOfRangeException("evidence", "TimelineId должен быть положительным.");
            }

            var stageCode = NormalizeStageCode(evidence.StageCode);
            if (string.IsNullOrEmpty(stageCode))
            {
                throw new ArgumentException("StageCode должен быть указан.", "evidence");
            }

            const string sql = @"
INSERT INTO Perdoc.dbo.TEpdKonturStageCompletionEvidence
(
    TimelineId,
    StageCode,
    TitleCode,
    TransportationId,
    TitleId,
    ExternalDocumentStatus,
    ExternalTitleStatus,
    ExternalActionCode,
    IsDraft,
    HasActiveError,
    HttpStatus,
    RawEvidenceSummary,
    CompletionSource,
    CheckedAt
)
VALUES
(
    @TimelineId,
    @StageCode,
    @TitleCode,
    @TransportationId,
    @TitleId,
    @ExternalDocumentStatus,
    @ExternalTitleStatus,
    @ExternalActionCode,
    @IsDraft,
    @HasActiveError,
    @HttpStatus,
    @RawEvidenceSummary,
    @CompletionSource,
    @CheckedAt
);";

            using (var cn = new SqlConnection(ConnectionString))
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.Add("@TimelineId", SqlDbType.BigInt).Value = evidence.TimelineId;
                cmd.Parameters.Add("@StageCode", SqlDbType.NVarChar, 32).Value = stageCode;
                cmd.Parameters.Add("@TitleCode", SqlDbType.NVarChar, 10).Value = ToDbString(Trim(evidence.TitleCode, 10));
                cmd.Parameters.Add("@TransportationId", SqlDbType.NVarChar, 100).Value = ToDbString(Trim(evidence.TransportationId, 100));
                cmd.Parameters.Add("@TitleId", SqlDbType.NVarChar, 100).Value = ToDbString(Trim(evidence.TitleId, 100));
                cmd.Parameters.Add("@ExternalDocumentStatus", SqlDbType.NVarChar, 100).Value = ToDbString(Trim(evidence.ExternalDocumentStatus, 100));
                cmd.Parameters.Add("@ExternalTitleStatus", SqlDbType.NVarChar, 100).Value = ToDbString(Trim(evidence.ExternalTitleStatus, 100));
                cmd.Parameters.Add("@ExternalActionCode", SqlDbType.NVarChar, 100).Value = ToDbString(Trim(evidence.ExternalActionCode, 100));
                cmd.Parameters.Add("@IsDraft", SqlDbType.Bit).Value = evidence.IsDraft;
                cmd.Parameters.Add("@HasActiveError", SqlDbType.Bit).Value = evidence.HasActiveError;
                cmd.Parameters.Add("@HttpStatus", SqlDbType.Int).Value = evidence.HttpStatus.HasValue ? (object)evidence.HttpStatus.Value : DBNull.Value;
                cmd.Parameters.Add("@RawEvidenceSummary", SqlDbType.NVarChar, 1000).Value = ToDbString(Trim(evidence.RawEvidenceSummary, 1000));
                cmd.Parameters.Add("@CompletionSource", SqlDbType.NVarChar, 50).Value = ToDbString(Trim(evidence.CompletionSource, 50));
                cmd.Parameters.Add("@CheckedAt", SqlDbType.DateTime).Value = evidence.CheckedAt == DateTime.MinValue ? DateTime.Now : evidence.CheckedAt;

                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Читает evidence из текущей строки SQL reader.
        /// </summary>
        /// <param name="reader">Reader, установленный на строку evidence.</param>
        /// <returns>Заполненная модель evidence.</returns>
        /// <remarks>Метод не выполняет дополнительных SQL-запросов.</remarks>
        private KonturStageCompletionEvidence ReadEvidence(SqlDataReader reader)
        {
            return new KonturStageCompletionEvidence
            {
                Id = reader.GetInt64(0),
                TimelineId = reader.GetInt64(1),
                StageCode = reader.GetString(2),
                TitleCode = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                TransportationId = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                TitleId = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                ExternalDocumentStatus = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                ExternalTitleStatus = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                ExternalActionCode = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                IsDraft = reader.GetBoolean(9),
                HasActiveError = reader.GetBoolean(10),
                HttpStatus = reader.IsDBNull(11) ? (int?)null : reader.GetInt32(11),
                RawEvidenceSummary = reader.IsDBNull(12) ? string.Empty : reader.GetString(12),
                CompletionSource = reader.IsDBNull(13) ? string.Empty : reader.GetString(13),
                CheckedAt = reader.GetDateTime(14)
            };
        }

        /// <summary>
        /// Нормализует код этапа.
        /// </summary>
        /// <param name="stageCode">Исходный код этапа.</param>
        /// <returns>Код этапа в верхнем регистре или пустую строку.</returns>
        /// <remarks>Нормализация совпадает с ключом состояния этапа.</remarks>
        private string NormalizeStageCode(string stageCode)
        {
            return string.IsNullOrEmpty(stageCode) ? string.Empty : stageCode.Trim().ToUpperInvariant();
        }

        /// <summary>
        /// Обрезает строку до максимальной длины.
        /// </summary>
        /// <param name="value">Исходное значение.</param>
        /// <param name="maxLength">Максимальная длина.</param>
        /// <returns>Обрезанное значение или пустая строка.</returns>
        /// <remarks>Обрезка защищает legacy SQL от ошибок длины строки.</remarks>
        private string Trim(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var normalized = value.Trim();
            return normalized.Length <= maxLength ? normalized : normalized.Substring(0, maxLength);
        }

        /// <summary>
        /// Преобразует строку в значение SQL-параметра.
        /// </summary>
        /// <param name="value">Исходная строка.</param>
        /// <returns>Строка или DBNull.Value.</returns>
        /// <remarks>Пустые диагностические значения хранятся как NULL.</remarks>
        private object ToDbString(string value)
        {
            return string.IsNullOrEmpty(value) ? (object)DBNull.Value : value;
        }
    }
}