/*
  ФАЙЛ: KonturRawLogRepository.cs
  НАЗНАЧЕНИЕ: Запись sanitized raw-логов операторного обмена в Perdoc.dbo.TEpdOperatorRawLog.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  05.05.2026 - Первичное создание репозитория raw-логов оператора.
  12.05.2026 - Добавлено чтение последнего response-лога для продуктовой диагностики UI.
  12.05.2026 - Добавлена фильтрация последнего response по коду этапа для разделения логов T1/T2/T3/T4.
  13.05.2026 - Фильтрация этапа переведена на stage-маркер в payload при Direction=response.
  13.05.2026 - Добавлена безопасная обрезка строковых полей перед INSERT, чтобы исключить SQL 8152.
*/

using System;
using System.Data.SqlClient;

namespace Tis.KonturIntegration.Storage
{
    /// <summary>
    /// Репозиторий диагностических raw-логов для операторного слоя Контур.
    /// </summary>
    public class KonturRawLogRepository
    {
        /// <summary>
        /// DTO последней response-записи raw-лога для отображения в UI.
        /// </summary>
        public class LastResponseLog
        {
            /// <summary>
            /// Получает или задает идентификатор записи raw-лога.
            /// </summary>
            public long Id { get; set; }

            /// <summary>
            /// Получает или задает endpoint вызова Контур API.
            /// </summary>
            public string Endpoint { get; set; }

            /// <summary>
            /// Получает или задает HTTP-статус ответа.
            /// </summary>
            public int? HttpStatus { get; set; }

            /// <summary>
            /// Получает или задает timestamp создания записи.
            /// </summary>
            public DateTime CreatedAt { get; set; }

            /// <summary>
            /// Получает или задает очищенный payload ответа.
            /// </summary>
            public string SanitizedPayload { get; set; }
        }

        /// <summary>
        /// Инициализирует репозиторий строкой подключения к SQL.
        /// </summary>
        /// <param name="connectionString">Строка подключения к базе с таблицей TEpdOperatorRawLog.</param>
        /// <remarks>Хранится только sanitized payload без секретных данных.</remarks>
        public KonturRawLogRepository(string connectionString)
        {
            ConnectionString = connectionString;
        }

        /// <summary>
        /// Получает строку подключения для SQL-операций.
        /// </summary>
        public string ConnectionString { get; private set; }

        /// <summary>
        /// Записывает диагностический raw-лог по операторному вызову.
        /// </summary>
        /// <param name="timelineId">Идентификатор документа ТИС в timeline, если уже известен.</param>
        /// <param name="operatorCode">Код оператора, например Kontur.</param>
        /// <param name="direction">Направление записи, например request или response.</param>
        /// <param name="endpoint">Адрес endpoint, который вызывался.</param>
        /// <param name="httpStatus">HTTP-статус ответа, если применимо.</param>
        /// <param name="sanitizedPayload">Очищенный payload без секретов.</param>
        /// <returns>Количество затронутых строк SQL.</returns>
        /// <remarks>Метод используется для трассировки первой инженерной поставки T1.</remarks>
        public int InsertLog(long? timelineId, string operatorCode, string direction, string endpoint, int? httpStatus, string sanitizedPayload)
        {
            const string sql = @"
INSERT INTO Perdoc.dbo.TEpdOperatorRawLog
    (TimelineId, OperatorCode, Direction, Endpoint, HttpStatus, SanitizedPayload)
VALUES
    (@TimelineId, @OperatorCode, @Direction, @Endpoint, @HttpStatus, @SanitizedPayload)";

            using (var connection = new SqlConnection(ConnectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                // Защита от 8152: ограничиваем длину полей под legacy-схему таблицы логов.
                // В payload сохраняем начало сообщения (stage/runId/traceId/code), т.к. оно важнее хвоста.
                var safeOperatorCode = TrimToLength(operatorCode, 32);
                var safeDirection = TrimToLength(direction, 32);
                var safeEndpoint = TrimToLength(endpoint, 256);
                var safePayload = TrimToLength(sanitizedPayload, 700);

                command.Parameters.AddWithValue("@TimelineId", (object)timelineId ?? (object)System.DBNull.Value);
                command.Parameters.AddWithValue("@OperatorCode", (object)safeOperatorCode ?? (object)System.DBNull.Value);
                command.Parameters.AddWithValue("@Direction", (object)safeDirection ?? (object)System.DBNull.Value);
                command.Parameters.AddWithValue("@Endpoint", (object)safeEndpoint ?? (object)System.DBNull.Value);
                command.Parameters.AddWithValue("@HttpStatus", (object)httpStatus ?? (object)System.DBNull.Value);
                command.Parameters.AddWithValue("@SanitizedPayload", (object)safePayload ?? (object)System.DBNull.Value);
                connection.Open();
                return command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Возвращает последнюю запись response-лога по timeline, оператору и этапу.
        /// </summary>
        /// <param name="timelineId">Идентификатор документа в timeline ТИС.</param>
        /// <param name="operatorCode">Код оператора, для Контур ожидается значение Kontur.</param>
        /// <param name="stageCode">Код этапа, например T1_INITIAL или T2; допускается пустое значение.</param>
        /// <returns>Последняя response-запись или null, если записей нет.</returns>
        /// <remarks>
        /// Если stageCode не задан, выбирается последний response любой стадии.
        /// Если stageCode задан, поиск выполняется по stage-маркеру в SanitizedPayload.
        /// </remarks>
        public LastResponseLog GetLastResponseLog(long timelineId, string operatorCode, string stageCode)
        {
            const string sql = @"
SELECT TOP 1
    Id,
    Endpoint,
    HttpStatus,
    CreatedAt,
    SanitizedPayload
FROM Perdoc.dbo.TEpdOperatorRawLog WITH (NOLOCK)
WHERE TimelineId = @TimelineId
  AND OperatorCode = @OperatorCode
  AND Direction = 'response'
  AND (@StageMarker = '' OR SanitizedPayload LIKE @StageMarker)
ORDER BY Id DESC";

            using (var connection = new SqlConnection(ConnectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                var normalizedStage = NormalizeStage(stageCode);
                var stageMarker = string.IsNullOrEmpty(normalizedStage) ? string.Empty : "stage=" + normalizedStage + ";%";

                command.Parameters.AddWithValue("@TimelineId", timelineId);
                command.Parameters.AddWithValue("@OperatorCode", operatorCode);
                command.Parameters.AddWithValue("@StageMarker", stageMarker);
                connection.Open();

                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return null;
                    }

                    return new LastResponseLog
                    {
                        Id = reader.GetInt64(0),
                        Endpoint = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                        HttpStatus = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2),
                        CreatedAt = reader.GetDateTime(3),
                        SanitizedPayload = reader.IsDBNull(4) ? string.Empty : reader.GetString(4)
                    };
                }
            }
        }

        /// <summary>
        /// Нормализует код этапа для фильтрации raw-лога.
        /// </summary>
        /// <param name="stageCode">Исходный код этапа.</param>
        /// <returns>Нормализованный код в верхнем регистре либо пустая строка.</returns>
        /// <remarks>Выделено в отдельный метод, чтобы единообразно формировать значение Direction.</remarks>
        private string NormalizeStage(string stageCode)
        {
            return string.IsNullOrEmpty(stageCode) ? string.Empty : stageCode.Trim().ToUpperInvariant();
        }

        /// <summary>
        /// Обрезает строку до заданной длины для безопасной записи в legacy-таблицу.
        /// </summary>
        /// <param name="value">Исходная строка.</param>
        /// <param name="maxLength">Максимально допустимая длина.</param>
        /// <returns>Строка не длиннее maxLength или null.</returns>
        /// <remarks>
        /// Метод нужен для защиты от SQL 8152 без изменения схемы БД.
        /// Если строка пустая, возвращается null для хранения как NULL.
        /// </remarks>
        private string TrimToLength(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            var normalized = value.Trim();
            if (normalized.Length <= maxLength)
            {
                return normalized;
            }

            return normalized.Substring(0, maxLength);
        }
    }
}
