/*
  ФАЙЛ: KonturSettingsRepository.cs
  НАЗНАЧЕНИЕ: Чтение операторных настроек Контур из Perdoc.dbo.TEpdOperatorSettings.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  05.05.2026 - Первичное создание репозитория настроек оператора.
  14.05.2026 - Добавлен upsert-метод записи настроек для обновления OIDC токенов.
*/

using System;
using System.Data.SqlClient;

namespace Tis.KonturIntegration.Storage
{
    /// <summary>
    /// Репозиторий операторных настроек Контур.
    /// </summary>
    public class KonturSettingsRepository
    {
        /// <summary>
        /// Инициализирует репозиторий строкой подключения к SQL.
        /// </summary>
        /// <param name="connectionString">Строка подключения к базе, где доступна схема Perdoc.</param>
        /// <remarks>Репозиторий не хранит состояние кроме строки подключения.</remarks>
        public KonturSettingsRepository(string connectionString)
        {
            ConnectionString = connectionString;
        }

        /// <summary>
        /// Получает строку подключения для SQL-операций.
        /// </summary>
        public string ConnectionString { get; private set; }

        /// <summary>
        /// Читает значение настройки по коду оператора и ключу.
        /// </summary>
        /// <param name="operatorCode">Код оператора, например Kontur.</param>
        /// <param name="settingKey">Ключ настройки, например ApiKey или LogisticsApiUrl.</param>
        /// <returns>Найденное значение настройки или пустая строка, если запись отсутствует.</returns>
        /// <remarks>Метод не выбрасывает исключение при отсутствии данных, чтобы адаптер мог обработать это как неготовность конфигурации.</remarks>
        public string GetSettingValue(string operatorCode, string settingKey)
        {
            const string sql = @"
SELECT TOP 1 SettingValue
FROM Perdoc.dbo.TEpdOperatorSettings
WHERE OperatorCode = @OperatorCode
  AND SettingKey = @SettingKey";

            using (var connection = new SqlConnection(ConnectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@OperatorCode", operatorCode);
                command.Parameters.AddWithValue("@SettingKey", settingKey);
                connection.Open();

                var scalar = command.ExecuteScalar();
                if (scalar == null || scalar == DBNull.Value)
                {
                    return string.Empty;
                }

                return Convert.ToString(scalar);
            }
        }

        /// <summary>
        /// Создает или обновляет значение настройки по коду оператора и ключу.
        /// </summary>
        /// <param name="operatorCode">Код оператора, например Kontur.</param>
        /// <param name="settingKey">Ключ настройки.</param>
        /// <param name="settingValue">Новое значение настройки.</param>
        /// <remarks>Метод используется для сохранения обновленных OIDC токенов после refresh.</remarks>
        public void UpsertSettingValue(string operatorCode, string settingKey, string settingValue)
        {
            const string sql = @"
IF EXISTS (
    SELECT 1
    FROM Perdoc.dbo.TEpdOperatorSettings
    WHERE OperatorCode = @OperatorCode
      AND SettingKey = @SettingKey
)
BEGIN
    UPDATE Perdoc.dbo.TEpdOperatorSettings
    SET SettingValue = @SettingValue
    WHERE OperatorCode = @OperatorCode
      AND SettingKey = @SettingKey
END
ELSE
BEGIN
    INSERT INTO Perdoc.dbo.TEpdOperatorSettings (OperatorCode, SettingKey, SettingValue)
    VALUES (@OperatorCode, @SettingKey, @SettingValue)
END";

            using (var connection = new SqlConnection(ConnectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@OperatorCode", operatorCode);
                command.Parameters.AddWithValue("@SettingKey", settingKey);
                command.Parameters.AddWithValue("@SettingValue", settingValue ?? string.Empty);
                connection.Open();
                command.ExecuteNonQuery();
            }
        }
    }
}
