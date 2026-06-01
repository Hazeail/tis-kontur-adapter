/*
  ФАЙЛ: KonturSignatureResult.cs
  НАЗНАЧЕНИЕ: Модель результата получения и проверки открепленной подписи титула ЭТрН.
  Используется stage-runner для явного разделения ошибок подписи и ошибок отправки оператору.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  13.05.2026 - Первичное создание результата сервиса подписи для конвейера Контур.
*/

namespace Tis.KonturIntegration.Models
{
    /// <summary>
    /// Описывает результат получения и локальной проверки detached CMS-подписи.
    /// Используется между сервисом подписи, хранилищем артефактов и отправкой в Контур.
    /// </summary>
    public class KonturSignatureResult
    {
        /// <summary>
        /// Получает или задает признак успешного получения и проверки подписи.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Получает или задает байты открепленной подписи.
        /// </summary>
        public byte[] SignatureBytes { get; set; }

        /// <summary>
        /// Получает или задает имя файла подписи для multipart-отправки.
        /// </summary>
        public string SignatureFileName { get; set; }

        /// <summary>
        /// Получает или задает отпечаток сертификата подписанта, если он известен.
        /// </summary>
        public string Thumbprint { get; set; }

        /// <summary>
        /// Получает или задает роль подписанта внутри процесса ЭТрН.
        /// </summary>
        public string SignerRole { get; set; }

        /// <summary>
        /// Получает или задает диагностическое сообщение результата.
        /// </summary>
        public string Message { get; set; }
    }
}
