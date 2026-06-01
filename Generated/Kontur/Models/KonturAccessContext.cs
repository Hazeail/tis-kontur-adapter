/*
  ФАЙЛ: KonturAccessContext.cs
  НАЗНАЧЕНИЕ: Модель разрешенного контекста доступа к Контур API для конкретного титула ЭТрН.
  Хранит вычисленные токен и ящик отправителя после ролевого разрешения настроек ТИС.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  12.05.2026 - Первичное создание модели доступа для ролевой маршрутизации T1/T2/T3/T4.
  14.05.2026 - Добавлено поле AccessToken для OIDC-авторизации вызовов Logistics API.
  26.05.2026 - Добавлено поле SolutionInfo для передачи обязательного заголовка X-Solution-Info.
*/

namespace Tis.KonturIntegration.Models
{
    /// <summary>
    /// Контекст доступа к Контур API для отправки конкретного титула ЭТрН.
    /// </summary>
    public class KonturAccessContext
    {
        /// <summary>
        /// Получает или задает признак готовности контекста к отправке.
        /// </summary>
        public bool IsReady { get; set; }

        /// <summary>
        /// Получает или задает код титула ЭТрН (T1/T2/T3/T4).
        /// </summary>
        public string TitleCode { get; set; }

        /// <summary>
        /// Получает или задает бизнес-роль отправителя (Consignor/Carrier/Consignee).
        /// </summary>
        public string SenderRole { get; set; }

        /// <summary>
        /// Получает или задает URL Logistics API.
        /// </summary>
        public string ApiUrl { get; set; }

        /// <summary>
        /// Получает или задает OIDC access token, разрешенный для выбранной роли.
        /// </summary>
        public string AccessToken { get; set; }

        /// <summary>
        /// Получает или задает DiadocBoxId отправителя для выбранной роли.
        /// </summary>
        public string SenderBoxId { get; set; }

        /// <summary>
        /// Получает или задает значение заголовка X-Solution-Info для вызовов Kontur API.
        /// </summary>
        public string SolutionInfo { get; set; }

        /// <summary>
        /// Получает или задает диагностическое сообщение, объясняющее причину неготовности.
        /// </summary>
        public string Message { get; set; }
    }
}
