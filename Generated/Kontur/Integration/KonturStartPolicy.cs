/*
  ФАЙЛ: KonturStartPolicy.cs
  НАЗНАЧЕНИЕ: Политика проверок стартовых условий перед отправкой титулов в Контур.
  Используется адаптером, чтобы заранее отсеивать вызовы в неподходящей стадии документа.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  08.05.2026 - Первичное создание файла.
  08.05.2026 - Добавлена проверка допустимости запуска T2 по внутреннему статусу.
  13.05.2026 - Добавлена проверка допустимости запуска T3 по внутреннему статусу timeline.
  13.05.2026 - Добавлена проверка допустимости запуска T4 по внутреннему статусу timeline.
*/

using System;

namespace Tis.KonturIntegration.Integration
{
    /// <summary>
    /// Политика проверок стартовых условий для интеграционных операций Контур.
    /// </summary>
    public static class KonturStartPolicy
    {
        /// <summary>
        /// Проверяет, допускается ли запуск T1 в режиме draft для текущего статуса timeline.
        /// </summary>
        /// <param name="lastStatus">Текущий внутренний статус документа в EPD timeline.</param>
        /// <returns>Истина, если режим draft разрешен на указанном статусе.</returns>
        /// <remarks>
        /// На текущем этапе draft-ветка разрешается только на статусе printform_ready,
        /// что синхронизировано с фактическим поведением Контур в runtime.
        /// </remarks>
        public static bool IsDraftStartAllowed(string lastStatus)
        {
            return string.Equals(lastStatus, "printform_ready", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Проверяет, допускается ли запуск T2 для текущего статуса timeline.
        /// </summary>
        /// <param name="lastStatus">Текущий внутренний статус документа в EPD timeline.</param>
        /// <returns>Истина, если отправка T2 не блокируется по внутреннему статусу.</returns>
        /// <remarks>
        /// На текущем этапе блокируются только явно завершенные/ошибочные состояния.
        /// Детализация правил может быть расширена после накопления runtime-данных.
        /// </remarks>
        public static bool IsT2StartAllowed(string lastStatus)
        {
            if (string.IsNullOrEmpty(lastStatus))
            {
                return true;
            }

            return !string.Equals(lastStatus, "completed", StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(lastStatus, "doc_error", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Проверяет, допускается ли запуск T3 для текущего статуса timeline.
        /// </summary>
        /// <param name="lastStatus">Текущий внутренний статус документа в EPD timeline.</param>
        /// <returns>Истина, если отправка T3 не блокируется по внутреннему статусу.</returns>
        /// <remarks>Правило синхронизировано с T2: явно завершенные и ошибочные документы не отправляются повторно.</remarks>
        public static bool IsT3StartAllowed(string lastStatus)
        {
            if (string.IsNullOrEmpty(lastStatus))
            {
                return true;
            }

            return !string.Equals(lastStatus, "completed", StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(lastStatus, "doc_error", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Проверяет, допускается ли запуск T4 для текущего статуса timeline.
        /// </summary>
        /// <param name="lastStatus">Текущий внутренний статус документа в EPD timeline.</param>
        /// <returns>Истина, если отправка T4 не блокируется по внутреннему статусу.</returns>
        /// <remarks>Правило синхронизировано с T2/T3: явно завершенные и ошибочные документы не отправляются повторно.</remarks>
        public static bool IsT4StartAllowed(string lastStatus)
        {
            if (string.IsNullOrEmpty(lastStatus))
            {
                return true;
            }

            return !string.Equals(lastStatus, "completed", StringComparison.OrdinalIgnoreCase)
                   && !string.Equals(lastStatus, "doc_error", StringComparison.OrdinalIgnoreCase);
        }
    }
}
