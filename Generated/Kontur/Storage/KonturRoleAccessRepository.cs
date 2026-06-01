/*
  ФАЙЛ: KonturRoleAccessRepository.cs
  НАЗНАЧЕНИЕ: Чтение ролевого реестра доступа Контур для этапов ЭТрН.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  13.05.2026 - Первичное создание репозитория ролевого доступа.
*/

using System;
using System.Data.SqlClient;
using Tis.KonturIntegration.Models;

namespace Tis.KonturIntegration.Storage
{
    /// <summary>
    /// Репозиторий ролевого доступа по титулу и роли отправителя.
    /// Нужен для явного разделения маршрутизации между заказчиком, перевозчиком и грузополучателем.
    /// </summary>
    public class KonturRoleAccessRepository
    {
        /// <summary>
        /// Инициализирует репозиторий строкой подключения к SQL.
        /// </summary>
        /// <param name="connectionString">Строка подключения к базе с таблицей Perdoc.dbo.TEpdOperatorRoleAccess.</param>
        /// <remarks>Репозиторий используется только на чтение в рантайме отправки титулов.</remarks>
        public KonturRoleAccessRepository(string connectionString)
        {
            ConnectionString = connectionString;
        }

        /// <summary>
        /// Получает строку подключения.
        /// </summary>
        public string ConnectionString { get; private set; }

        /// <summary>
        /// Читает подходящую активную запись ролевого доступа.
        /// </summary>
        /// <param name="operatorCode">Код оператора, например Kontur.</param>
        /// <param name="titleCode">Код титула T1/T2/T3/T4.</param>
        /// <param name="senderRole">Роль отправителя титула.</param>
        /// <returns>Найденная запись или null.</returns>
        /// <remarks>
        /// Поиск сначала пытается найти точное совпадение по титулу, затем по роли.
        /// Если найдено несколько записей, выбирается запись с наибольшим приоритетом.
        /// </remarks>
        public KonturRoleAccessRecord FindActive(string operatorCode, string titleCode, string senderRole)
        {
            const string sql = @"
SELECT TOP 1
       Id,
       OperatorCode,
       TitleCode,
       SenderRole,
       Inn,
       Kpp,
       DiadocBoxId,
       ApiKey,
       Priority,
       IsActive,
       UpdatedAt
  FROM Perdoc.dbo.TEpdOperatorRoleAccess
 WHERE OperatorCode = @OperatorCode
   AND IsActive = 1
   AND (
        (TitleCode = @TitleCode)
        OR (SenderRole = @SenderRole)
       )
 ORDER BY CASE WHEN TitleCode = @TitleCode THEN 0 ELSE 1 END,
          Priority DESC,
          Id DESC";

            using (var connection = new SqlConnection(ConnectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@OperatorCode", operatorCode ?? string.Empty);
                command.Parameters.AddWithValue("@TitleCode", (titleCode ?? string.Empty).Trim().ToUpperInvariant());
                command.Parameters.AddWithValue("@SenderRole", (senderRole ?? string.Empty).Trim());

                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return null;
                    }

                    return new KonturRoleAccessRecord
                    {
                        Id = Convert.ToInt64(reader["Id"]),
                        OperatorCode = Convert.ToString(reader["OperatorCode"]),
                        TitleCode = Convert.ToString(reader["TitleCode"]),
                        SenderRole = Convert.ToString(reader["SenderRole"]),
                        Inn = Convert.ToString(reader["Inn"]),
                        Kpp = Convert.ToString(reader["Kpp"]),
                        DiadocBoxId = Convert.ToString(reader["DiadocBoxId"]),
                        ApiKey = Convert.ToString(reader["ApiKey"]),
                        Priority = Convert.ToInt32(reader["Priority"]),
                        IsActive = Convert.ToBoolean(reader["IsActive"]),
                        UpdatedAt = Convert.ToDateTime(reader["UpdatedAt"])
                    };
                }
            }
        }
    }
}
