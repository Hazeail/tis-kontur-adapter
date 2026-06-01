/*
  ФАЙЛ: KonturEtrnT1234XmlService.cs
  НАЗНАЧЕНИЕ: Единый фасад нормализации XML титулов T1-T4 для контура Контур внутри ТИС.
  Сервис сводит в одну точку stage-specific правила пост-обработки XML, чтобы страница, runner и сервисы этапов не расходились по логике.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  23.05.2026 - Первичное создание единого фасада нормализации XML для T1-T4.
  26.05.2026 - Добавлена единая финальная нормализация ВерсПрог и подписи для всех XML T1-T4.
*/

using System;
using System.IO;
using System.Text;
using System.Xml;
using tis.Modules;

namespace Tis.KonturIntegration.Integration
{
    /// <summary>
    /// Выполняет единую нормализацию XML титулов T1-T4 перед сохранением артефакта или отправкой оператору.
    /// </summary>
    /// <remarks>
    /// Сервис нужен как единая точка stage-specific логики, чтобы:
    /// - T1 синхронизировал участников, ИдФайл и подписанта;
    /// - T2/T3/T4 синхронизировали узел подписанта;
    /// - остальной код вызывал один и тот же фасад, а не набор разрозненных helper-методов.
    /// </remarks>
    public class KonturEtrnT1234XmlService
    {
        /// <summary>
        /// Инициализирует фасад строкой подключения к БД ТИС и Perdoc.
        /// </summary>
        /// <param name="connectionString">Строка подключения, необходимая для чтения выбранного подписанта и stage-настроек.</param>
        /// <remarks>Фасад не пишет в БД, а только читает данные, нужные для нормализации XML.</remarks>
        public KonturEtrnT1234XmlService(string connectionString)
        {
            ConnectionString = connectionString;
        }

        /// <summary>
        /// Получает строку подключения к БД.
        /// </summary>
        public string ConnectionString { get; private set; }

        /// <summary>
        /// Нормализует XML титула, сохраненный в файле рабочего слоя.
        /// </summary>
        /// <param name="xmlPath">Путь к XML-файлу титула.</param>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="titleCode">Код титула T1/T2/T3/T4.</param>
        /// <returns>Путь к исходному или нормализованному XML-файлу.</returns>
        /// <remarks>
        /// Для T1 используется сервис полной нормализации, который может породить новый override-файл.
        /// Для T2/T3/T4 XML обновляется на месте, чтобы не плодить дополнительные файловые версии.
        /// </remarks>
        public string NormalizeFile(string xmlPath, long timelineId, string titleCode)
        {
            if (string.IsNullOrEmpty(xmlPath) || !File.Exists(xmlPath))
            {
                return xmlPath;
            }

            var normalizedTitleCode = NormalizeTitleCode(titleCode);
            string targetPath = xmlPath;
            if (string.Equals(normalizedTitleCode, "T1", StringComparison.OrdinalIgnoreCase))
            {
                targetPath = new KonturT1ParticipantOverrideService(ConnectionString).Apply(xmlPath, timelineId);
            }

            var fileBytes = File.ReadAllBytes(targetPath);
            var normalizedBytes = NormalizeBytes(timelineId, normalizedTitleCode, fileBytes);
            if (normalizedBytes == null || normalizedBytes.Length == 0)
            {
                return targetPath;
            }

            File.WriteAllBytes(targetPath, normalizedBytes);
            return targetPath;
        }

        /// <summary>
        /// Нормализует XML титула в виде массива байт перед сохранением артефакта.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="titleCode">Код титула T1/T2/T3/T4.</param>
        /// <param name="xmlBytes">Исходные байты XML.</param>
        /// <returns>Нормализованные байты XML.</returns>
        /// <remarks>
        /// Для T1 байтовый путь не используется, потому что T1-нормализация строит override-файл.
        /// Для T2/T3/T4 применяется синхронизация подписанта XML с выбранным подписантом этапа.
        /// </remarks>
        public byte[] NormalizeBytes(long timelineId, string titleCode, byte[] xmlBytes)
        {
            if (xmlBytes == null || xmlBytes.Length == 0)
            {
                return xmlBytes;
            }

            var normalizedTitleCode = NormalizeTitleCode(titleCode);
            if (string.IsNullOrEmpty(normalizedTitleCode))
            {
                return xmlBytes;
            }

            var resultBytes = xmlBytes;
            if (string.Equals(normalizedTitleCode, "T1", StringComparison.OrdinalIgnoreCase))
            {
                resultBytes = new KonturT1ParticipantOverrideService(ConnectionString).ApplyBytes(resultBytes, timelineId);
            }

            resultBytes = new KonturStageSignerXmlOverrideService(ConnectionString).Apply(timelineId, normalizedTitleCode, resultBytes);
            return ApplyProgramVersionOverride(resultBytes);
        }

        /// <summary>
        /// Подменяет корневой атрибут ВерсПрог на единую версию ТИС для всех XML T1-T4.
        /// </summary>
        /// <param name="xmlBytes">Исходные байты XML.</param>
        /// <returns>XML с актуализированным ВерсПрог или исходный массив, если подмена не требуется.</returns>
        /// <remarks>
        /// Общий ВерсПрог нужен как единый “штамп сборки”, чтобы T1-T4 не расходились между собой
        /// и всегда указывали на одну и ту же версию ТИС независимо от legacy-builder.
        /// </remarks>
        private byte[] ApplyProgramVersionOverride(byte[] xmlBytes)
        {
            if (xmlBytes == null || xmlBytes.Length == 0)
            {
                return xmlBytes;
            }

            var programVersion = BuildProgramVersion();
            if (string.IsNullOrEmpty(programVersion))
            {
                return xmlBytes;
            }

            try
            {
                var document = new XmlDocument();
                document.PreserveWhitespace = true;
                document.LoadXml(Encoding.GetEncoding(1251).GetString(xmlBytes));

                if (document.DocumentElement == null)
                {
                    return xmlBytes;
                }

                document.DocumentElement.SetAttribute("ВерсПрог", programVersion);
                return SaveDocumentToBytes(document);
            }
            catch
            {
                return xmlBytes;
            }
        }

        /// <summary>
        /// Собирает строку ВерсПрог из текущей версии ТИС.
        /// </summary>
        /// <returns>Текст версии без HTML-разметки и лишних пробелов.</returns>
        /// <remarks>
        /// Источником истины служит zaya.Version, потому что эту строку команда уже использует
        /// как рабочее обозначение релиза в админке и при выпуске обновлений.
        /// </remarks>
        private string BuildProgramVersion()
        {
            var rawVersion = zaya.Version ?? string.Empty;
            if (string.IsNullOrEmpty(rawVersion))
            {
                return string.Empty;
            }

            var normalized = rawVersion.Replace("&nbsp;", " ").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();
            while (normalized.IndexOf("  ", StringComparison.Ordinal) >= 0)
            {
                normalized = normalized.Replace("  ", " ");
            }

            return normalized;
        }

        /// <summary>
        /// Сохраняет XML-документ обратно в байтовый массив CP1251.
        /// </summary>
        /// <param name="document">XML-документ после общей нормализации.</param>
        /// <returns>Байты XML в кодировке CP1251.</returns>
        private byte[] SaveDocumentToBytes(XmlDocument document)
        {
            var settings = new XmlWriterSettings
            {
                Encoding = Encoding.GetEncoding(1251),
                Indent = false,
                NewLineHandling = NewLineHandling.None
            };

            using (var stream = new MemoryStream())
            using (var writer = XmlWriter.Create(stream, settings))
            {
                document.Save(writer);
                writer.Flush();
                return stream.ToArray();
            }
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
