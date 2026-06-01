/*
  ФАЙЛ: KonturStageSignerXmlOverrideService.cs
  НАЗНАЧЕНИЕ: Пост-обработка XML титулов Контур для синхронизации узла подписанта с выбранным подписантом этапа.
  Сервис применяется после legacy-builder и не вмешивается в исходную астраловскую логику формирования титула.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  23.05.2026 - Первичное создание сервиса синхронизации подписанта T2/T3/T4 по выбранному подписанту этапа.
  26.05.2026 - Добавлена поддержка T1, чтобы все титулы T1-T4 проходили через единый override подписанта.
  28.05.2026 - В режиме Kontur-only выбор подписанта переведен на отдельный тестовый контекст Соколов/Захаров.
  28.05.2026 - Для T2/T4 добавлена синхронизация представителя перевозчика с выбранным подписантом этапа.
*/

using System;
using System.IO;
using System.Text;
using System.Xml;
using Tis.KonturIntegration.Models;

namespace Tis.KonturIntegration.Integration
{
    /// <summary>
    /// Синхронизирует узел подписанта в XML титула Контур с выбранным подписантом этапа.
    /// </summary>
    /// <remarks>
    /// Сервис нужен как безопасный адаптер поверх legacy-builder, когда менять исходные builder-файлы нельзя,
    /// но итоговый XML должен совпадать с выбором подписанта на продуктовой странице КонтурProbe.
    /// </remarks>
    public class KonturStageSignerXmlOverrideService
    {
        /// <summary>
        /// Инициализирует сервис строкой подключения к БД ТИС и Perdoc.
        /// </summary>
        /// <param name="connectionString">Строка подключения, используемая для чтения выбранного подписанта этапа.</param>
        /// <remarks>Строка подключения нужна только для резолва выбранного подписанта и не используется для записи.</remarks>
        public KonturStageSignerXmlOverrideService(string connectionString)
        {
            ConnectionString = connectionString;
        }

        /// <summary>
        /// Получает строку подключения к БД.
        /// </summary>
        public string ConnectionString { get; private set; }

        /// <summary>
        /// Применяет синхронизацию узла подписанта к XML указанного титула.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="titleCode">Код титула T1/T2/T3/T4.</param>
        /// <param name="xmlBytes">Байты XML титула в кодировке CP1251.</param>
        /// <returns>Обновленные байты XML или исходный массив, если подмена не требуется.</returns>
        /// <remarks>
        /// Метод работает в режиме best-effort: если для этапа нет выбранного подписанта или
        /// узел подписанта в XML не найден, исходный XML возвращается без исключения.
        /// </remarks>
        public byte[] Apply(long timelineId, string titleCode, byte[] xmlBytes)
        {
            if (timelineId <= 0 || xmlBytes == null || xmlBytes.Length == 0)
            {
                return xmlBytes;
            }

            var normalizedTitleCode = NormalizeTitleCode(titleCode);
            var signerNodePaths = GetSignerNodePaths(normalizedTitleCode);
            if (signerNodePaths == null || signerNodePaths.Length == 0)
            {
                return xmlBytes;
            }

            var signer = ResolveSelectedSigner(timelineId, normalizedTitleCode);
            if (signer == null || string.IsNullOrEmpty(signer.SignerFio))
            {
                return xmlBytes;
            }

            var document = new XmlDocument();
            document.PreserveWhitespace = true;
            document.LoadXml(Encoding.GetEncoding(1251).GetString(xmlBytes));

            var signerNode = FindFirstExistingSignerNode(document, signerNodePaths);
            if (signerNode == null)
            {
                return xmlBytes;
            }

            ApplySignerNodeOverride(document, signerNode, signer);
            ApplyCarrierRepresentativeOverride(document, normalizedTitleCode, signer);
            return SaveDocumentToBytes(document);
        }

        /// <summary>
        /// Возвращает выбранного подписанта с учетом тестового режима Kontur-only.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="titleCode">Код титула T1/T2/T3/T4.</param>
        /// <returns>Выбранный подписант или null.</returns>
        /// <remarks>
        /// В test-only режиме XML должен синхронизироваться не со штатным реестром ТИС,
        /// а с отдельным тестовым контекстом подписантов Контур.
        /// </remarks>
        private KonturStageSignerCandidate ResolveSelectedSigner(long timelineId, string titleCode)
        {
            if (new KonturTestModeService(ConnectionString).IsEnabled(timelineId))
            {
                return new KonturTestSigningContextService(ConnectionString).TryResolveSelectedSigner(timelineId, titleCode);
            }

            return new KonturStageSignerService(ConnectionString).TryResolveSelectedSigner(timelineId, titleCode);
        }

        /// <summary>
        /// Возвращает список XPath к узлу подписанта для указанного титула.
        /// </summary>
        /// <param name="titleCode">Код титула T1/T2/T3/T4.</param>
        /// <returns>Массив XPath-путей в порядке приоритета.</returns>
        /// <remarks>
        /// Для T3 оставлены несколько допустимых вариантов пути, потому что в разных поставках
        /// XML схема узла подписанта получателя может называться по-разному.
        /// </remarks>
        private string[] GetSignerNodePaths(string titleCode)
        {
            switch (titleCode)
            {
                case "T1":
                    return new[] { "/Файл/Документ/Подписант" };
                case "T2":
                case "T4":
                    return new[] { "/Файл/Документ/ПодпИнфПрв" };
                case "T3":
                    return new[]
                    {
                        "/Файл/Документ/ПодпИнфГП",
                        "/Файл/Документ/ПодпИнфПол",
                        "/Файл/Документ/ПодпИнфПолуч"
                    };
                default:
                    return null;
            }
        }

        /// <summary>
        /// Ищет первый существующий узел подписанта среди допустимых XPath.
        /// </summary>
        /// <param name="document">XML-документ титула.</param>
        /// <param name="signerNodePaths">Допустимые XPath-пути к узлу подписанта.</param>
        /// <returns>Найденный XML-элемент подписанта или null.</returns>
        /// <remarks>Поиск по нескольким XPath позволяет не привязываться к одной версии XSD для T3.</remarks>
        private XmlElement FindFirstExistingSignerNode(XmlDocument document, string[] signerNodePaths)
        {
            if (document == null || signerNodePaths == null)
            {
                return null;
            }

            for (var index = 0; index < signerNodePaths.Length; index++)
            {
                var signerNode = document.SelectSingleNode(signerNodePaths[index]) as XmlElement;
                if (signerNode != null)
                {
                    return signerNode;
                }
            }

            return null;
        }

        /// <summary>
        /// Подменяет ФИО и должность в найденном узле подписанта.
        /// </summary>
        /// <param name="document">XML-документ титула.</param>
        /// <param name="signerNode">Найденный узел подписанта.</param>
        /// <param name="signer">Выбранный подписант этапа.</param>
        /// <remarks>
        /// Подменяем только те поля, которые должны совпадать с выбором оператора.
        /// Остальные атрибуты узла подписанта, связанные с видом полномочий, сохраняем без изменений.
        /// </remarks>
        private void ApplySignerNodeOverride(XmlDocument document, XmlElement signerNode, KonturStageSignerCandidate signer)
        {
            if (document == null || signerNode == null || signer == null)
            {
                return;
            }

            string lastName;
            string firstName;
            string middleName;
            SplitSignerFio(signer.SignerFio, out lastName, out firstName, out middleName);
            if (string.IsNullOrEmpty(lastName) || string.IsNullOrEmpty(firstName))
            {
                return;
            }

            var fioNode = signerNode.SelectSingleNode("ФИО") as XmlElement;
            if (fioNode == null)
            {
                fioNode = document.CreateElement("ФИО");
                signerNode.AppendChild(fioNode);
            }

            fioNode.SetAttribute("Фамилия", lastName);
            fioNode.SetAttribute("Имя", firstName);

            if (string.IsNullOrEmpty(middleName))
            {
                fioNode.RemoveAttribute("Отчество");
            }
            else
            {
                fioNode.SetAttribute("Отчество", middleName);
            }

            if (!string.IsNullOrEmpty(signer.Position))
            {
                signerNode.SetAttribute("Должн", signer.Position);
            }
        }

        /// <summary>
        /// Синхронизирует представителя перевозчика в содержательной части T2/T4 с выбранным подписантом этапа.
        /// </summary>
        /// <param name="document">XML-документ титула.</param>
        /// <param name="titleCode">Нормализованный код титула.</param>
        /// <param name="signer">Выбранный подписант этапа.</param>
        /// <remarks>
        /// Для Контур важно, чтобы в T2/T4 не расходились блок подписи и блок представителя перевозчика.
        /// Если ПодпИнфПрв уже переключен на тестового подписанта, а СвЛицОргПрвз остался от legacy-сборки,
        /// оператор может вернуть внутреннюю ошибку вместо явной бизнес-валидации.
        /// </remarks>
        private void ApplyCarrierRepresentativeOverride(XmlDocument document, string titleCode, KonturStageSignerCandidate signer)
        {
            if (document == null || signer == null)
            {
                return;
            }

            if (!string.Equals(titleCode, "T2", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(titleCode, "T4", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var representativeNode = document.SelectSingleNode("/Файл/Документ/СодИнфПрв/СвЛицОргПрвз") as XmlElement;
            if (representativeNode == null)
            {
                return;
            }

            string lastName;
            string firstName;
            string middleName;
            SplitSignerFio(signer.SignerFio, out lastName, out firstName, out middleName);
            if (string.IsNullOrEmpty(lastName) || string.IsNullOrEmpty(firstName))
            {
                return;
            }

            var fioNode = representativeNode.SelectSingleNode("ФИО") as XmlElement;
            if (fioNode == null)
            {
                fioNode = document.CreateElement("ФИО");
                representativeNode.AppendChild(fioNode);
            }

            fioNode.SetAttribute("Фамилия", lastName);
            fioNode.SetAttribute("Имя", firstName);

            if (string.IsNullOrEmpty(middleName))
            {
                fioNode.RemoveAttribute("Отчество");
            }
            else
            {
                fioNode.SetAttribute("Отчество", middleName);
            }
        }

        /// <summary>
        /// Сохраняет XML-документ обратно в массив байт CP1251.
        /// </summary>
        /// <param name="document">XML-документ после подмены узла подписанта.</param>
        /// <returns>Байты XML в кодировке CP1251.</returns>
        /// <remarks>
        /// Явная CP1251 нужна для совместимости со всем текущим контуром хранения и ручного просмотра XML в ТИС.
        /// </remarks>
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
        /// Разбивает ФИО на фамилию, имя и отчество для записи в XML.
        /// </summary>
        /// <param name="signerFio">Полное ФИО выбранного подписанта.</param>
        /// <param name="lastName">Фамилия подписанта.</param>
        /// <param name="firstName">Имя подписанта.</param>
        /// <param name="middleName">Отчество подписанта.</param>
        /// <remarks>
        /// Если частей больше трех, остаток склеивается в отчество, чтобы не потерять составное имя.
        /// </remarks>
        private void SplitSignerFio(string signerFio, out string lastName, out string firstName, out string middleName)
        {
            lastName = string.Empty;
            firstName = string.Empty;
            middleName = string.Empty;

            var normalized = (signerFio ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(normalized))
            {
                return;
            }

            var parts = normalized.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
            {
                lastName = parts[0];
            }

            if (parts.Length > 1)
            {
                firstName = parts[1];
            }

            if (parts.Length > 2)
            {
                middleName = string.Join(" ", parts, 2, parts.Length - 2);
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
