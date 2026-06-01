/*
  ФАЙЛ: KonturTimelineRepository.cs
  НАЗНАЧЕНИЕ: Чтение статуса и базового состояния документа из EPD timeline.
  Нужен адаптеру Контур для предвалидации шага перед сетевым вызовом API.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  08.05.2026 - Первичное создание файла.
*/

using System;
using System.Data.SqlClient;

namespace Tis.KonturIntegration.Storage
{
    /// <summary>
    /// Репозиторий чтения текущего состояния документа из Perdoc.dbo.epd_timeline.
    /// </summary>
    public class KonturTimelineRepository
    {
        /// <summary>
        /// Инициализирует репозиторий строкой подключения к SQL.
        /// </summary>
        /// <param name="connectionString">Строка подключения к базе с таблицей epd_timeline.</param>
        /// <remarks>Репозиторий только читает данные и не меняет состояние timeline.</remarks>
        public KonturTimelineRepository(string connectionString)
        {
            ConnectionString = connectionString;
        }

        /// <summary>
        /// Получает строку подключения для SQL-операций.
        /// </summary>
        public string ConnectionString { get; private set; }

        /// <summary>
        /// Возвращает текущее значение last_status по TimelineId.
        /// </summary>
        /// <param name="timelineId">Идентификатор документа в EPD timeline.</param>
        /// <returns>Значение last_status или пустая строка, если запись не найдена.</returns>
        /// <remarks>
        /// Метод используется до вызова Контур API, чтобы отсеивать попытки запуска
        /// в заведомо неподходящей стадии документооборота.
        /// </remarks>
        public string GetLastStatus(long timelineId)
        {
            const string sql = @"
SELECT TOP 1 last_status
FROM Perdoc.dbo.epd_timeline
WHERE id = @TimelineId";

            using (var connection = new SqlConnection(ConnectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@TimelineId", timelineId);
                connection.Open();

                var scalar = command.ExecuteScalar();
                if (scalar == null || scalar == DBNull.Value)
                {
                    return string.Empty;
                }

                return Convert.ToString(scalar);
            }
        }
    }
}

