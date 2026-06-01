/*
  ФАЙЛ: KonturTestModeRepository.cs
  НАЗНАЧЕНИЕ: Хранилище состояния тестового режима Kontur-only по TimelineId.
  Изолирует чтение и запись флага тестового сценария от code-behind и интеграционных сервисов.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  28.05.2026 - Первичное создание репозитория состояния Kontur-only режима.
*/

using System;
using System.Data;
using System.Data.SqlClient;
using Tis.KonturIntegration.Models;

namespace Tis.KonturIntegration.Storage
{
    /// <summary>
    /// Репозиторий работы с состоянием тестового режима Kontur-only.
    /// </summary>
    public class KonturTestModeRepository
    {
        /// <summary>
        /// Инициализирует репозиторий строкой подключения к ТИС и Perdoc.
        /// </summary>
        /// <param name="connectionString">Строка подключения к SQL Server.</param>
        public KonturTestModeRepository(string connectionString)
        {
            ConnectionString = connectionString;
        }

        /// <summary>
        /// Получает строку подключения, используемую репозиторием.
        /// </summary>
        public string ConnectionString { get; private set; }

        /// <summary>
        /// Возвращает сохраненное состояние тестового режима по timeline.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <returns>Сохраненное состояние или null, если режим не зафиксирован.</returns>
        public KonturTestModeState GetState(long timelineId)
        {
            const string sql = @"
SELECT TOP (1)
       Id,
       TimelineId,
       IsEnabled,
       UpdatedByUserId,
       UpdatedAt
  FROM Perdoc.dbo.TEpdKonturTestMode
 WHERE TimelineId = @TimelineId;";

            using (var cn = new SqlConnection(ConnectionString))
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.Add("@TimelineId", SqlDbType.BigInt).Value = timelineId;
                cn.Open();

                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return null;
                    }

                    return new KonturTestModeState
                    {
                        Id = reader.GetInt64(0),
                        TimelineId = reader.GetInt64(1),
                        IsEnabled = reader.GetBoolean(2),
                        UpdatedByUserId = reader.IsDBNull(3) ? 0 : reader.GetInt64(3),
                        UpdatedAt = reader.GetDateTime(4)
                    };
                }
            }
        }

        /// <summary>
        /// Сохраняет состояние тестового режима по timeline.
        /// </summary>
        /// <param name="state">Состояние для сохранения.</param>
        /// <remarks>Для одного timeline хранится только одна актуальная запись состояния.</remarks>
        public void SaveState(KonturTestModeState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException("state");
            }

            const string sql = @"
MERGE Perdoc.dbo.TEpdKonturTestMode AS target
USING
(
    SELECT
        @TimelineId AS TimelineId,
        @IsEnabled AS IsEnabled,
        @UpdatedByUserId AS UpdatedByUserId
) AS source
ON target.TimelineId = source.TimelineId
WHEN MATCHED THEN
    UPDATE SET
        IsEnabled = source.IsEnabled,
        UpdatedByUserId = source.UpdatedByUserId,
        UpdatedAt = GETDATE()
WHEN NOT MATCHED THEN
    INSERT (TimelineId, IsEnabled, UpdatedByUserId, UpdatedAt)
    VALUES (source.TimelineId, source.IsEnabled, source.UpdatedByUserId, GETDATE());";

            using (var cn = new SqlConnection(ConnectionString))
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.Add("@TimelineId", SqlDbType.BigInt).Value = state.TimelineId;
                cmd.Parameters.Add("@IsEnabled", SqlDbType.Bit).Value = state.IsEnabled;
                cmd.Parameters.Add("@UpdatedByUserId", SqlDbType.BigInt).Value = state.UpdatedByUserId;
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Удаляет сохраненное состояние тестового режима по timeline.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        public void DeleteState(long timelineId)
        {
            const string sql = @"
DELETE FROM Perdoc.dbo.TEpdKonturTestMode
 WHERE TimelineId = @TimelineId;";

            using (var cn = new SqlConnection(ConnectionString))
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.Add("@TimelineId", SqlDbType.BigInt).Value = timelineId;
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }
    }
}
