/*
  ФАЙЛ: KonturSignatureService.cs
  НАЗНАЧЕНИЕ: Сервис получения и проверки detached CMS-подписи для титулов ЭТрН Контур.
  Поддерживает совместимость с ручным .sgn и внутреннее чтение подписи из SQL-хранилищ.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  13.05.2026 - Первичное создание сервиса подписи для stage-runner Контур.
  13.05.2026 - Добавлен приоритет подписи sig2_detached для T2/T4 и исправлена роль подписанта T4.
*/

using System;
using System.IO;
using TIS.EPD;
using Tis.KonturIntegration.Models;
using Tis.KonturIntegration.Storage;

namespace Tis.KonturIntegration.Integration
{
    /// <summary>
    /// Получает и проверяет открепленную подпись титула ЭТрН перед отправкой в Контур.
    /// Используется как порт подписи между XML-builder и операторским адаптером.
    /// </summary>
    public class KonturSignatureService : IKonturSignatureService
    {
        /// <summary>
        /// Инициализирует сервис подписи репозиторием артефактов.
        /// </summary>
        /// <param name="artifactRepository">Репозиторий внутренних XML/SGN артефактов.</param>
        /// <remarks>Репозиторий используется первым источником при запуске без пользовательского файла подписи.</remarks>
        public KonturSignatureService(KonturTitleArtifactRepository artifactRepository)
        {
            ArtifactRepository = artifactRepository;
        }

        /// <summary>
        /// Получает репозиторий артефактов титулов.
        /// </summary>
        public KonturTitleArtifactRepository ArtifactRepository { get; private set; }

        /// <summary>
        /// Получает подпись титула и проверяет ее относительно XML.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="titleCode">Код титула, для которого нужна подпись.</param>
        /// <param name="titleXml">Байты XML титула.</param>
        /// <param name="signaturePath">Необязательный путь к .sgn для совместимости с ручным запуском.</param>
        /// <returns>Результат получения и проверки подписи.</returns>
        /// <remarks>Если путь не задан, сервис сначала смотрит TEpdTitleArtifact, затем legacy epd_doc_store.</remarks>
        public KonturSignatureResult Resolve(long timelineId, string titleCode, byte[] titleXml, string signaturePath)
        {
            if (titleXml == null || titleXml.Length == 0)
            {
                return Fail("TitleXmlEmpty");
            }

            var normalizedTitle = NormalizeTitleCode(titleCode);
            var signatureFileName = BuildSignatureFileName(normalizedTitle);
            byte[] signatureBytes = null;
            string thumbprint = string.Empty;
            string signerRole = TitleToSignerRole(normalizedTitle);

            if (!string.IsNullOrEmpty(signaturePath))
            {
                if (!File.Exists(signaturePath))
                {
                    return Fail("SignatureFileNotFound: " + signaturePath);
                }

                signatureBytes = File.ReadAllBytes(signaturePath);
                signatureFileName = Path.GetFileName(signaturePath);
            }
            else
            {
                var stored = ArtifactRepository == null ? null : ArtifactRepository.GetLatest(timelineId, normalizedTitle);
                if (stored != null && stored.HasSignature)
                {
                    signatureBytes = stored.TitleSgn;
                    signatureFileName = string.IsNullOrEmpty(stored.SignatureFileName) ? signatureFileName : stored.SignatureFileName;
                    thumbprint = stored.Thumbprint;
                    signerRole = string.IsNullOrEmpty(stored.SignerRole) ? signerRole : stored.SignerRole;
                }
                else
                {
                    signatureBytes = GetLegacySignatureBytes(timelineId, normalizedTitle);
                }
            }

            if (signatureBytes == null || signatureBytes.Length == 0)
            {
                return Fail("SignatureMissingFor" + normalizedTitle);
            }

            // Локальная проверка нужна до API-вызова, чтобы не отправлять заведомо чужую подпись к XML титула.
            string verifyInfo;
            var verifyOk = EpdRepo.VerifyDetachedCms(titleXml, signatureBytes, out verifyInfo);
            if (!verifyOk)
            {
                return Fail("SignatureVerifyFailed: " + (string.IsNullOrEmpty(verifyInfo) ? "unknown" : verifyInfo));
            }

            return new KonturSignatureResult
            {
                IsSuccess = true,
                SignatureBytes = signatureBytes,
                SignatureFileName = signatureFileName,
                Thumbprint = thumbprint,
                SignerRole = signerRole,
                Message = string.IsNullOrEmpty(verifyInfo) ? "SignatureVerified" : verifyInfo
            };
        }

        /// <summary>
        /// Формирует неуспешный результат сервиса подписи.
        /// </summary>
        /// <param name="message">Причина остановки сценария.</param>
        /// <returns>Неуспешный результат подписи.</returns>
        /// <remarks>Единый формат ошибки позволяет stage-runner не ловить штатные исключения подписи.</remarks>
        private KonturSignatureResult Fail(string message)
        {
            return new KonturSignatureResult
            {
                IsSuccess = false,
                Message = message
            };
        }

        /// <summary>
        /// Формирует имя файла подписи для multipart-отправки.
        /// </summary>
        /// <param name="titleCode">Код титула.</param>
        /// <returns>Имя файла с расширением sig.</returns>
        /// <remarks>Имя используется только в HTTP-части запроса и не требует файла на диске.</remarks>
        private string BuildSignatureFileName(string titleCode)
        {
            return NormalizeTitleCode(titleCode).ToLowerInvariant() + ".sig";
        }

        /// <summary>
        /// Возвращает базовую роль подписанта по коду титула.
        /// </summary>
        /// <param name="titleCode">Код титула.</param>
        /// <returns>Роль подписанта для диагностики и хранения.</returns>
        /// <remarks>Роль уточняется сохраненным артефактом, если подпись уже была получена ранее.</remarks>
        private string TitleToSignerRole(string titleCode)
        {
            if (titleCode == "T2")
            {
                return "Carrier";
            }

            if (titleCode == "T3")
            {
                return "Consignee";
            }

            if (titleCode == "T4")
            {
                return "Carrier";
            }

            return "Unknown";
        }

        /// <summary>
        /// Нормализует код титула.
        /// </summary>
        /// <param name="titleCode">Исходный код титула.</param>
        /// <returns>Код титула в верхнем регистре.</returns>
        /// <remarks>Нормализация исключает расхождение ключей при чтении артефактов и диагностике.</remarks>
        private string NormalizeTitleCode(string titleCode)
        {
            return string.IsNullOrEmpty(titleCode) ? "UNKNOWN" : titleCode.Trim().ToUpperInvariant();
        }

        /// <summary>
        /// Получает подпись из legacy-хранилища EPD с учетом роли титула.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="titleCode">Код титула.</param>
        /// <returns>Байты подписи или пустое значение.</returns>
        /// <remarks>
        /// Для T2/T4 приоритетно используется вторая подпись перевозчика (sig2_detached).
        /// Для остальных титулов сохраняется текущая fallback-логика через GetLatestSigBytes.
        /// </remarks>
        private byte[] GetLegacySignatureBytes(long timelineId, string titleCode)
        {
            if (titleCode == "T2" || titleCode == "T4")
            {
                var sig2 = EpdRepo.GetSig2Bytes(timelineId);
                if (sig2 != null && sig2.Length > 0)
                {
                    return sig2;
                }
            }

            return EpdRepo.GetLatestSigBytes(timelineId);
        }
    }
}
