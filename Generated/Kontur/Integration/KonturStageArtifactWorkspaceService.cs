/*
  ФАЙЛ: KonturStageArtifactWorkspaceService.cs
  НАЗНАЧЕНИЕ: Сервис рабочего файлового слоя артефактов этапов ЭТрН Контур.
  Поддерживает один актуальный XML-файл на этап и очищает устаревшие служебные копии в папке App_Data\Temp\KonturEtrn.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  29.05.2026 - Добавлено сохранение актуальной detached-подписи этапа рядом с актуальным XML-артефактом.
  29.05.2026 - Добавлен поиск актуальной detached-подписи этапа для bridge-проверок UI.
  23.05.2026 - Первичное создание сервиса актуальных XML-артефактов этапов.
*/

using System;
using System.Collections.Generic;
using System.IO;

namespace Tis.KonturIntegration.Integration
{
    /// <summary>
    /// Сервис управления рабочими XML-артефактами этапов ЭТрН в серверной папке.
    /// </summary>
    public class KonturStageArtifactWorkspaceService
    {
        /// <summary>
        /// Инициализирует сервис путем к рабочей папке артефактов.
        /// </summary>
        /// <param name="rootDirectory">Абсолютный путь к App_Data\Temp\KonturEtrn.</param>
        /// <remarks>Сервис не хранит внешние зависимости и работает только в рамках одной папки артефактов.</remarks>
        public KonturStageArtifactWorkspaceService(string rootDirectory)
        {
            RootDirectory = rootDirectory ?? string.Empty;
        }

        /// <summary>
        /// Получает корневую папку рабочего слоя артефактов.
        /// </summary>
        public string RootDirectory { get; private set; }

        /// <summary>
        /// Сохраняет один актуальный XML-файл этапа и очищает устаревшие копии того же этапа.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="titleCode">Код титула T1/T2/T3/T4.</param>
        /// <param name="xmlBytes">XML титула в байтах.</param>
        /// <returns>Абсолютный путь к сохраненному актуальному XML-файлу.</returns>
        /// <remarks>
        /// Рабочая папка не является источником истины, поэтому сервис оставляет только один актуальный файл этапа,
        /// а предыдущие служебные копии очищает, чтобы не накапливать лишние артефакты между прогонами.
        /// </remarks>
        public string SaveCurrentXml(long timelineId, string titleCode, byte[] xmlBytes)
        {
            if (timelineId <= 0)
            {
                throw new ArgumentOutOfRangeException("timelineId");
            }

            if (xmlBytes == null || xmlBytes.Length == 0)
            {
                throw new ArgumentNullException("xmlBytes");
            }

            var normalizedTitleCode = NormalizeTitleCode(titleCode);
            if (string.IsNullOrEmpty(normalizedTitleCode))
            {
                throw new ArgumentException("Код титула не задан.", "titleCode");
            }

            Directory.CreateDirectory(RootDirectory);

            var fullPath = Path.Combine(RootDirectory, GetCurrentXmlFileName(timelineId, normalizedTitleCode));

            // Перед записью очищаем старые служебные копии этапа, чтобы в папке не накапливался хлам между прогонами.
            CleanupLegacyXmlFiles(timelineId, normalizedTitleCode, fullPath);
            File.WriteAllBytes(fullPath, xmlBytes);
            return fullPath;
        }

        /// <summary>
        /// Возвращает имя актуального XML-файла этапа без пути.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="titleCode">Код титула T1/T2/T3/T4.</param>
        /// <returns>Имя актуального XML-файла этапа.</returns>
        /// <remarks>Именование детерминировано, чтобы UI и SQL-хранилище ссылались на один и тот же текущий артефакт.</remarks>
        public string GetCurrentXmlFileName(long timelineId, string titleCode)
        {
            var normalizedTitleCode = NormalizeTitleCode(titleCode);
            switch (normalizedTitleCode)
            {
                case "T1":
                    return string.Format("t1_override_timeline{0}.xml", timelineId);
                case "T2":
                    return string.Format("t2_timeline{0}.xml", timelineId);
                case "T3":
                    return string.Format("t3_timeline{0}.xml", timelineId);
                case "T4":
                    return string.Format("t4_timeline{0}.xml", timelineId);
                default:
                    return string.Format("{0}_timeline{1}.xml", normalizedTitleCode.ToLowerInvariant(), timelineId);
            }
        }

        /// <summary>
        /// Возвращает абсолютный путь к актуальному XML-файлу этапа, если он уже существует.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="titleCode">Код титула T1/T2/T3/T4.</param>
        /// <returns>Путь к актуальному XML-файлу или пустую строку.</returns>
        public string FindCurrentXmlPath(long timelineId, string titleCode)
        {
            var path = Path.Combine(RootDirectory, GetCurrentXmlFileName(timelineId, titleCode));
            return File.Exists(path) ? path : string.Empty;
        }

        /// <summary>
        /// Сохраняет одну актуальную detached-подпись этапа и очищает устаревшие копии того же этапа.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="titleCode">Код титула T1/T2/T3/T4.</param>
        /// <param name="signatureBytes">Байты detached-подписи титула.</param>
        /// <returns>Абсолютный путь к сохраненной актуальной подписи этапа.</returns>
        /// <remarks>Рабочая подпись синхронизируется с актуальным XML и не используется как постоянное хранилище.</remarks>
        public string SaveCurrentSignature(long timelineId, string titleCode, byte[] signatureBytes)
        {
            if (timelineId <= 0)
            {
                throw new ArgumentOutOfRangeException("timelineId");
            }

            if (signatureBytes == null || signatureBytes.Length == 0)
            {
                throw new ArgumentNullException("signatureBytes");
            }

            var normalizedTitleCode = NormalizeTitleCode(titleCode);
            if (string.IsNullOrEmpty(normalizedTitleCode))
            {
                throw new ArgumentException("Код титула не задан.", "titleCode");
            }

            Directory.CreateDirectory(RootDirectory);

            var fullPath = Path.Combine(RootDirectory, GetCurrentSignatureFileName(timelineId, normalizedTitleCode));

            // Подпись хранится как одна актуальная копия на этап, чтобы отправка не схватывала старый SGN.
            CleanupLegacySignatureFiles(timelineId, normalizedTitleCode, fullPath);
            File.WriteAllBytes(fullPath, signatureBytes);
            return fullPath;
        }

        /// <summary>
        /// Возвращает имя актуального файла detached-подписи этапа без пути.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="titleCode">Код титула T1/T2/T3/T4.</param>
        /// <returns>Имя актуального файла detached-подписи этапа.</returns>
        /// <remarks>Именование детерминировано, чтобы XML и SGN одного этапа были связаны на файловом слое.</remarks>
        public string GetCurrentSignatureFileName(long timelineId, string titleCode)
        {
            var normalizedTitleCode = NormalizeTitleCode(titleCode);
            return string.Format("{0}_timeline{1}.sig", normalizedTitleCode.ToLowerInvariant(), timelineId);
        }

        /// <summary>
        /// Возвращает абсолютный путь к актуальной detached-подписи этапа, если она уже существует.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="titleCode">Код титула T1/T2/T3/T4.</param>
        /// <returns>Путь к актуальной подписи этапа или пустую строку.</returns>
        /// <remarks>
        /// Метод нужен UI-мосту реконструкционного слоя, чтобы проверять готовность подписи
        /// без знания соглашения об именовании рабочих файлов.
        /// </remarks>
        public string FindCurrentSignaturePath(long timelineId, string titleCode)
        {
            var path = Path.Combine(RootDirectory, GetCurrentSignatureFileName(timelineId, titleCode));
            return File.Exists(path) ? path : string.Empty;
        }

        /// <summary>
        /// Очищает устаревшие XML-копии этапа, оставляя только целевой актуальный файл.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="titleCode">Код титула T1/T2/T3/T4.</param>
        /// <param name="currentFullPath">Путь к актуальному XML, который должен сохраниться после очистки.</param>
        /// <remarks>
        /// Очистка затрагивает только служебные XML-файлы известного шаблона и не трогает подписи.
        /// Это сохраняет совместимость с ручным сценарием подписи и будущей выгрузкой документооборота.
        /// </remarks>
        private void CleanupLegacyXmlFiles(long timelineId, string titleCode, string currentFullPath)
        {
            foreach (var mask in GetLegacyXmlMasks(timelineId, titleCode))
            {
                var files = Directory.GetFiles(RootDirectory, mask, SearchOption.TopDirectoryOnly);
                for (var i = 0; i < files.Length; i++)
                {
                    if (string.Equals(files[i], currentFullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    File.Delete(files[i]);
                }
            }
        }

        /// <summary>
        /// Очищает устаревшие detached-подписи этапа, оставляя только целевой актуальный файл.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="titleCode">Код титула T1/T2/T3/T4.</param>
        /// <param name="currentFullPath">Путь к актуальной подписи, который должен сохраниться после очистки.</param>
        /// <remarks>Очистка затрагивает только служебные SGN-файлы известного шаблона и не трогает XML.</remarks>
        private void CleanupLegacySignatureFiles(long timelineId, string titleCode, string currentFullPath)
        {
            foreach (var mask in GetLegacySignatureMasks(timelineId, titleCode))
            {
                var files = Directory.GetFiles(RootDirectory, mask, SearchOption.TopDirectoryOnly);
                for (var i = 0; i < files.Length; i++)
                {
                    if (string.Equals(files[i], currentFullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    File.Delete(files[i]);
                }
            }
        }

        /// <summary>
        /// Возвращает маски старых XML-файлов этапа, которые можно очищать при сохранении нового актуального артефакта.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="titleCode">Код титула T1/T2/T3/T4.</param>
        /// <returns>Набор файловых масок для очистки.</returns>
        private IEnumerable<string> GetLegacyXmlMasks(long timelineId, string titleCode)
        {
            switch (NormalizeTitleCode(titleCode))
            {
                case "T1":
                    yield return string.Format("t1_override_timeline{0}.xml", timelineId);
                    yield return string.Format("t1_override_{0}_*.xml", timelineId);
                    yield return string.Format("t1_timeline{0}_*.xml", timelineId);
                    yield return string.Format("t1_source_timeline{0}.xml", timelineId);
                    break;
                case "T2":
                    yield return string.Format("t2_timeline{0}.xml", timelineId);
                    yield return string.Format("t2_timeline{0}_*.xml", timelineId);
                    break;
                case "T3":
                    yield return string.Format("t3_timeline{0}.xml", timelineId);
                    yield return string.Format("t3_timeline{0}_*.xml", timelineId);
                    break;
                case "T4":
                    yield return string.Format("t4_timeline{0}.xml", timelineId);
                    yield return string.Format("t4_timeline{0}_*.xml", timelineId);
                    break;
            }
        }

        /// <summary>
        /// Возвращает маски старых SGN-файлов этапа, которые можно очищать при сохранении новой актуальной подписи.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="titleCode">Код титула T1/T2/T3/T4.</param>
        /// <returns>Набор файловых масок для очистки detached-подписей.</returns>
        private IEnumerable<string> GetLegacySignatureMasks(long timelineId, string titleCode)
        {
            yield return string.Format("{0}_timeline{1}.sig", NormalizeTitleCode(titleCode).ToLowerInvariant(), timelineId);
            yield return string.Format("{0}_timeline{1}_*.sig", NormalizeTitleCode(titleCode).ToLowerInvariant(), timelineId);
        }

        /// <summary>
        /// Нормализует код титула к верхнему регистру.
        /// </summary>
        /// <param name="titleCode">Исходный код титула.</param>
        /// <returns>Нормализованный код титула.</returns>
        private string NormalizeTitleCode(string titleCode)
        {
            return string.IsNullOrEmpty(titleCode) ? string.Empty : titleCode.Trim().ToUpperInvariant();
        }
    }
}
