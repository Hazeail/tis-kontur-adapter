/*
  ФАЙЛ: KonturStageStateRepository.cs
  НАЗНАЧЕНИЕ: SQL-хранилище явного состояния этапа Контур ЭТрН в реконструкционном слое.
  Отделяет процесс этапа от артефактов XML/SGN, raw-log и внешних refs.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  28.05.2026 - Первичное создание repository состояния этапа с контрактом Get/Save.
  29.05.2026 - Добавлено хранение TransportationId и TitleId в явном состоянии этапа.
*/

using System;
using System.Data;
using System.Data.SqlClient;
using Tis.KonturIntegration.Models;

namespace Tis.KonturIntegration.Storage
{
    /// <summary>
    /// Репозиторий чтения и сохранения явного состояния этапа Контур ЭТрН в SQL-границе Perdoc.
    /// </summary>
    public class KonturStageStateRepository : IKonturStageStateRepository
    {
        /// <summary>
        /// Инициализирует repository строкой подключения к SQL Server.
        /// </summary>
        /// <param name="connectionString">Строка подключения к базе, где доступна схема Perdoc.</param>
        /// <remarks>Repository открывает соединение только на время отдельной операции чтения или сохранения.</remarks>
        public KonturStageStateRepository(string connectionString)
        {
            ConnectionString = connectionString;
        }

        /// <summary>
        /// Получает строку подключения для SQL-операций.
        /// </summary>
        public string ConnectionString { get; private set; }

        /// <summary>
        /// Возвращает сохраненное состояние этапа по timeline и коду этапа.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа в ТИС.</param>
        /// <param name="stageCode">Код этапа, например T1_INITIAL, T2, T3 или T4.</param>
        /// <returns>Сохраненное состояние этапа или null, если состояние еще не создано.</returns>
        /// <remarks>Метод читает только таблицу явного состояния и не использует fallback-диагностику по raw-log.</remarks>
        public KonturStageState Get(long timelineId, string stageCode)
        {
            const string sql = @"
SELECT TOP (1)
       Id,
       TimelineId,
       StageCode,
       TitleCode,
       XmlBuilt,
       SignatureImported,
       Sent,
       Completed,
       NextStageAllowed,
       TransportationId,
       TitleId,
       LastOperatorStatus,
       LastErrorCode,
       LastErrorMessage,
       UpdatedAt
  FROM Perdoc.dbo.TEpdKonturStageState
 WHERE TimelineId = @TimelineId
   AND StageCode = @StageCode;";

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

                    return ReadState(reader);
                }
            }
        }

        /// <summary>
        /// Сохраняет текущий снимок состояния этапа.
        /// </summary>
        /// <param name="state">Состояние этапа для вставки или обновления.</param>
        /// <remarks>
        /// Метод выполняет upsert по TimelineId и StageCode, чтобы read-model всегда видел один актуальный снимок этапа.
        /// </remarks>
        public void Save(KonturStageState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException("state");
            }

            if (state.TimelineId <= 0)
            {
                throw new ArgumentOutOfRangeException("state", "TimelineId должен быть положительным.");
            }

            var stageCode = NormalizeStageCode(state.StageCode);
            if (string.IsNullOrEmpty(stageCode))
            {
                throw new ArgumentException("StageCode должен быть указан.", "state");
            }

            const string sql = @"
MERGE Perdoc.dbo.TEpdKonturStageState AS target
USING
(
    SELECT
        @TimelineId AS TimelineId,
        @StageCode AS StageCode
) AS source
ON target.TimelineId = source.TimelineId
   AND target.StageCode = source.StageCode
WHEN MATCHED THEN
    UPDATE SET
        TitleCode = @TitleCode,
        XmlBuilt = @XmlBuilt,
        SignatureImported = @SignatureImported,
        Sent = @Sent,
        Completed = @Completed,
        NextStageAllowed = @NextStageAllowed,
        TransportationId = @TransportationId,
        TitleId = @TitleId,
        LastOperatorStatus = @LastOperatorStatus,
        LastErrorCode = @LastErrorCode,
        LastErrorMessage = @LastErrorMessage,
        UpdatedAt = GETDATE()
WHEN NOT MATCHED THEN
    INSERT
    (
        TimelineId,
        StageCode,
        TitleCode,
        XmlBuilt,
        SignatureImported,
        Sent,
        Completed,
        NextStageAllowed,
        TransportationId,
        TitleId,
        LastOperatorStatus,
        LastErrorCode,
        LastErrorMessage,
        UpdatedAt
    )
    VALUES
    (
        @TimelineId,
        @StageCode,
        @TitleCode,
        @XmlBuilt,
        @SignatureImported,
        @Sent,
        @Completed,
        @NextStageAllowed,
        @TransportationId,
        @TitleId,
        @LastOperatorStatus,
        @LastErrorCode,
        @LastErrorMessage,
        GETDATE()
    );";

            using (var cn = new SqlConnection(ConnectionString))
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.Add("@TimelineId", SqlDbType.BigInt).Value = state.TimelineId;
                cmd.Parameters.Add("@StageCode", SqlDbType.NVarChar, 32).Value = stageCode;
                cmd.Parameters.Add("@TitleCode", SqlDbType.NVarChar, 10).Value = NormalizeTitleCode(state.TitleCode, stageCode);
                cmd.Parameters.Add("@XmlBuilt", SqlDbType.Bit).Value = state.XmlBuilt;
                cmd.Parameters.Add("@SignatureImported", SqlDbType.Bit).Value = state.SignatureImported;
                cmd.Parameters.Add("@Sent", SqlDbType.Bit).Value = state.Sent;
                cmd.Parameters.Add("@Completed", SqlDbType.Bit).Value = state.Completed;
                cmd.Parameters.Add("@NextStageAllowed", SqlDbType.Bit).Value = state.NextStageAllowed;
                cmd.Parameters.Add("@TransportationId", SqlDbType.NVarChar, 100).Value = ToDbString(state.TransportationId);
                cmd.Parameters.Add("@TitleId", SqlDbType.NVarChar, 100).Value = ToDbString(state.TitleId);
                cmd.Parameters.Add("@LastOperatorStatus", SqlDbType.NVarChar, 100).Value = ToDbString(state.LastOperatorStatus);
                cmd.Parameters.Add("@LastErrorCode", SqlDbType.NVarChar, 100).Value = ToDbString(state.LastErrorCode);
                cmd.Parameters.Add("@LastErrorMessage", SqlDbType.NVarChar, 500).Value = ToDbString(state.LastErrorMessage);

                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Читает модель состояния из текущей строки SQL reader.
        /// </summary>
        /// <param name="reader">Reader, установленный на строку состояния этапа.</param>
        /// <returns>Заполненная модель состояния этапа.</returns>
        /// <remarks>Метод не перемещает reader и не выполняет дополнительную диагностику состояния.</remarks>
        private KonturStageState ReadState(SqlDataReader reader)
        {
            return new KonturStageState
            {
                Id = reader.GetInt64(0),
                TimelineId = reader.GetInt64(1),
                StageCode = reader.GetString(2),
                TitleCode = reader.GetString(3),
                XmlBuilt = reader.GetBoolean(4),
                SignatureImported = reader.GetBoolean(5),
                Sent = reader.GetBoolean(6),
                Completed = reader.GetBoolean(7),
                NextStageAllowed = reader.GetBoolean(8),
                TransportationId = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                TitleId = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                LastOperatorStatus = reader.IsDBNull(11) ? string.Empty : reader.GetString(11),
                LastErrorCode = reader.IsDBNull(12) ? string.Empty : reader.GetString(12),
                LastErrorMessage = reader.IsDBNull(13) ? string.Empty : reader.GetString(13),
                UpdatedAt = reader.GetDateTime(14)
            };
        }

        /// <summary>
        /// Нормализует код этапа для ключа хранения.
        /// </summary>
        /// <param name="stageCode">Исходный код этапа.</param>
        /// <returns>Код этапа в верхнем регистре или пустая строка.</returns>
        /// <remarks>Единая нормализация защищает от расхождения ключей T2/t2 при повторных сохранениях.</remarks>
        private string NormalizeStageCode(string stageCode)
        {
            return string.IsNullOrEmpty(stageCode) ? string.Empty : stageCode.Trim().ToUpperInvariant();
        }

        /// <summary>
        /// Нормализует код титула для хранения состояния.
        /// </summary>
        /// <param name="titleCode">Исходный код титула.</param>
        /// <param name="stageCode">Нормализованный код этапа для fallback-определения титула.</param>
        /// <returns>Код титула T1/T2/T3/T4 или UNKNOWN.</returns>
        /// <remarks>Fallback нужен, чтобы первый слой состояния можно было сохранять даже до появления отдельного маппера этапов.</remarks>
        private string NormalizeTitleCode(string titleCode, string stageCode)
        {
            if (!string.IsNullOrEmpty(titleCode))
            {
                return titleCode.Trim().ToUpperInvariant();
            }

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
        /// Преобразует строку в значение SQL-параметра.
        /// </summary>
        /// <param name="value">Исходная строка.</param>
        /// <returns>Строка или DBNull.Value.</returns>
        /// <remarks>Пустые диагностические поля хранятся как NULL, чтобы не смешивать отсутствие ошибки с текстовым значением.</remarks>
        private object ToDbString(string value)
        {
            return string.IsNullOrEmpty(value) ? (object)DBNull.Value : value;
        }
    }
}
