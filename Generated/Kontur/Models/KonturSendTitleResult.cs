/*
  ФАЙЛ: KonturSendTitleResult.cs
  НАЗНАЧЕНИЕ: Модель результата отправки титула в Контур.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  05.05.2026 - Первичное создание модели результата отправки.
*/

namespace Tis.KonturIntegration.Models
{
    /// <summary>
    /// Результат отправки титула или черновика в Kontur Logistics API.
    /// </summary>
    public class KonturSendTitleResult
    {
        /// <summary>
        /// Получает или задает признак успешного выполнения операторного запроса.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Получает или задает идентификатор перевозки в Контуре.
        /// </summary>
        public string TransportationId { get; set; }

        /// <summary>
        /// Получает или задает идентификатор титула в Контуре.
        /// </summary>
        public string TitleId { get; set; }

        /// <summary>
        /// Получает или задает HTTP-статус операторного вызова.
        /// </summary>
        public int HttpStatus { get; set; }

        /// <summary>
        /// Получает или задает очищенный payload ответа для диагностического хранения.
        /// </summary>
        public string SanitizedResponsePayload { get; set; }

        /// <summary>
        /// Получает или задает текст ошибки, если операторный вызов завершился неуспешно.
        /// </summary>
        public string ErrorMessage { get; set; }
    }
}
