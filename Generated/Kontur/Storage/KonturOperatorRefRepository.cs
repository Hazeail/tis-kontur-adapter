/*
  ФАЙЛ: KonturOperatorRefRepository.cs
  НАЗНАЧЕНИЕ: Запись внешних идентификаторов Контур в Perdoc.dbo.TEpdOperatorRef.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  05.05.2026 - Первичное создание репозитория операторных идентификаторов.
  08.05.2026 - Добавлено чтение последнего значения ref по TimelineId и RefType.
  29.05.2026 - Репозиторий переведен на порт IKonturOperatorRefRepository для use case синхронизации состояния этапа.
*/

using System.Data.SqlClient;

namespace Tis.KonturIntegration.Storage
{
    /// <summary>
    /// Репозиторий хранения ссылок на внешние идентификаторы оператора.
    /// </summary>
    public class KonturOperatorRefRepository : IKonturOperatorRefRepository
    {
        /// <summary>
        /// Инициализирует репозиторий строкой подключения к SQL.
        /// </summary>
        /// <param name="connectionString">Строка подключения к базе с таблицей TEpdOperatorRef.</param>
        /// <remarks>Все записи создаются с полным указанием схемы Perdoc.dbo.</remarks>
        public KonturOperatorRefRepository(string connectionString)
        {
            ConnectionString = connectionString;
        }

        /// <summary>
        /// Получает строку подключения для SQL-операций.
        /// </summary>
        public string ConnectionString { get; private set; }

        /// <summary>
        /// Сохраняет одну запись внешнего идентификатора оператора.
        /// </summary>
        /// <param name="timelineId">Идентификатор документа ТИС в timeline.</param>
        /// <param name="operatorCode">Код оператора, например Kontur.</param>
        /// <param name="refType">Тип внешнего идентификатора, например TransportationId.</param>
        /// <param name="refValue">Значение внешнего идентификатора.</param>
        /// <param name="sourceEventId">Идентификатор источника события, если есть.</param>
        /// <returns>Количество затронутых строк SQL.</returns>
        /// <remarks>Метод используется адаптером после успешного вызова оператора.</remarks>
        public int InsertRef(long timelineId, string operatorCode, string refType, string refValue, string sourceEventId)
        {
            const string sql = @"
INSERT INTO Perdoc.dbo.TEpdOperatorRef
    (TimelineId, OperatorCode, RefType, RefValue, SourceEventId)
VALUES
    (@TimelineId, @OperatorCode, @RefType, @RefValue, @SourceEventId)";

            using (var connection = new SqlConnection(ConnectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@TimelineId", timelineId);
                command.Parameters.AddWithValue("@OperatorCode", operatorCode);
                command.Parameters.AddWithValue("@RefType", refType);
                command.Parameters.AddWithValue("@RefValue", refValue);
                command.Parameters.AddWithValue("@SourceEventId", (object)sourceEventId ?? (object)System.DBNull.Value);
                connection.Open();
                return command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Возвращает последнее значение внешнего идентификатора по типу.
        /// </summary>
        /// <param name="timelineId">Идентификатор документа ТИС в timeline.</param>
        /// <param name="operatorCode">Код оператора, например Kontur.</param>
        /// <param name="refType">Тип идентификатора, например TransportationId.</param>
        /// <returns>Значение идентификатора или пустая строка, если запись отсутствует.</returns>
        /// <remarks>Метод используется для последующих титулов, которые зависят от уже сохраненных связок.</remarks>
        public string GetLatestRefValue(long timelineId, string operatorCode, string refType)
        {
            const string sql = @"
SELECT TOP 1 RefValue
FROM Perdoc.dbo.TEpdOperatorRef
WHERE TimelineId = @TimelineId
  AND OperatorCode = @OperatorCode
  AND RefType = @RefType
ORDER BY Id DESC";

            using (var connection = new SqlConnection(ConnectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@TimelineId", timelineId);
                command.Parameters.AddWithValue("@OperatorCode", operatorCode);
                command.Parameters.AddWithValue("@RefType", refType);
                connection.Open();

                var scalar = command.ExecuteScalar();
                if (scalar == null || scalar == System.DBNull.Value)
                {
                    return string.Empty;
                }

                return System.Convert.ToString(scalar);
            }
        }
    }
}
