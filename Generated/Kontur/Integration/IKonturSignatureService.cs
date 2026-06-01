/*
  ФАЙЛ: IKonturSignatureService.cs
  НАЗНАЧЕНИЕ: Порт получения и проверки подписи титула ЭТрН для конвейера Контур.
  Позволяет отдельно развивать клиентскую и серверную подпись без изменения отправки оператору.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  13.05.2026 - Первичное создание порта сервиса подписи.
*/

using Tis.KonturIntegration.Models;

namespace Tis.KonturIntegration.Integration
{
    /// <summary>
    /// Описывает контракт получения detached CMS-подписи для титула ЭТрН.
    /// Используется stage-runner между построением XML и отправкой в Контур.
    /// </summary>
    public interface IKonturSignatureService
    {
        /// <summary>
        /// Получает подпись титула и проверяет ее относительно XML.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="titleCode">Код титула, для которого нужна подпись.</param>
        /// <param name="titleXml">Байты XML титула, который должен быть подписан.</param>
        /// <param name="signaturePath">Необязательный путь к подписи для совместимости с ручным запуском.</param>
        /// <returns>Результат получения и локальной проверки подписи.</returns>
        /// <remarks>Пустой signaturePath означает, что подпись нужно искать во внутреннем хранилище.</remarks>
        KonturSignatureResult Resolve(long timelineId, string titleCode, byte[] titleXml, string signaturePath);
    }
}
