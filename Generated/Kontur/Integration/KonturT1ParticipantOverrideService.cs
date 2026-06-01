/*
  ФАЙЛ: KonturT1ParticipantOverrideService.cs
  НАЗНАЧЕНИЕ: Единая нормализация участников титула T1 для тестового контура Контур.
  Сервис синхронизирует ИдФайл и тело XML T1, чтобы все роли этапа ссылались на один и тот же набор организаций.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  23.05.2026 - Первичное создание сервиса полной нормализации участников T1 для Контур.
  23.05.2026 - Добавлена синхронизация узла Подписант по выбранному подписанту этапа T1.
  28.05.2026 - В режиме Kontur-only выбор подписанта T1 переведен на отдельный тестовый контекст Соколов/Захаров.
  28.05.2026 - Тестовый режим Kontur-only отвязан от legacy-флага T1ParticipantsOverrideEnabled, чтобы T1 всегда нормализовался целиком.
  28.05.2026 - Добавлено сохранение исходного НаимОрг и доведение блока водителя до валидного состояния для черновика Контур.
  28.05.2026 - Добавлен fallback на юридический адрес участника, чтобы T1 не терял обязательный индекс и код региона.
  28.05.2026 - Добавлен fallback имен участников из фактического контекста заявки, чтобы тестовые ИНН Контур не оставляли T1 без НаимОрг.
  28.05.2026 - Добавлен fallback адресных реквизитов из фактического контрагента роли, чтобы T1 не терял индекс грузополучателя в Kontur-only.
  29.05.2026 - Убран служебный невалидный ИНН водителя; fallback блока СвВодит переведен на безопасное заполнение реквизитов ВУ.
*/

using System;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using System.Xml;
using Tis.KonturIntegration.Models;
using Tis.KonturIntegration.Storage;

namespace Tis.KonturIntegration.Integration
{
    /// <summary>
    /// Выполняет полную нормализацию участников T1 для отправки в тестовый контур Контур.
    /// Сервис нужен как единая точка истины для страницы генерации и для рантайм-отправки T1.
    /// </summary>
    public class KonturT1ParticipantOverrideService
    {
        /// <summary>
        /// Создает сервис нормализации участников T1.
        /// </summary>
        /// <param name="connectionString">Строка подключения к БД ТИС и Perdoc.</param>
        /// <remarks>Строка подключения используется для чтения настроек, ролевого доступа и имен организаций.</remarks>
        public KonturT1ParticipantOverrideService(string connectionString)
        {
            ConnectionString = connectionString;
        }

        /// <summary>
        /// Получает строку подключения к БД.
        /// </summary>
        public string ConnectionString { get; private set; }

        /// <summary>
        /// Применяет полную нормализацию участников T1 и возвращает путь к нормализованному XML.
        /// </summary>
        /// <param name="xmlPath">Путь к исходному XML титула T1.</param>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <returns>Путь к исходному или новому нормализованному XML.</returns>
        /// <remarks>
        /// Если режим override не включен или не хватает настроек ролей, метод возвращает исходный XML без изменений.
        /// Нормализация должна менять не только ИдФайл, но и все ключевые узлы ГО/Зак/ГП/ТК в теле титула.
        /// </remarks>
        public string Apply(string xmlPath, long timelineId)
        {
            if (string.IsNullOrEmpty(xmlPath) || !File.Exists(xmlPath))
            {
                return xmlPath;
            }

            var sourceBytes = File.ReadAllBytes(xmlPath);
            var normalizedBytes = ApplyBytes(sourceBytes, timelineId);
            if (normalizedBytes == null || normalizedBytes.Length == 0)
            {
                return xmlPath;
            }

            var directory = Path.GetDirectoryName(xmlPath) ?? string.Empty;
            var fileName = string.Format("t1_override_{0}_{1}.xml", timelineId, DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            var outputPath = Path.Combine(directory, fileName);
            File.WriteAllBytes(outputPath, normalizedBytes);
            return outputPath;
        }

        /// <summary>
        /// Применяет полную нормализацию участников T1 к XML в виде массива байт.
        /// </summary>
        /// <param name="xmlBytes">Исходные байты XML титула T1.</param>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <returns>Нормализованные байты XML или исходный массив, если override не требуется.</returns>
        /// <remarks>
        /// Байтовый режим нужен для единого фасада T1-T4: он позволяет доводить XML до финального состояния
        /// перед сохранением артефакта в БД, а не только при файловой генерации из KonturProbe.
        /// </remarks>
        public byte[] ApplyBytes(byte[] xmlBytes, long timelineId)
        {
            if (xmlBytes == null || xmlBytes.Length == 0)
            {
                return xmlBytes;
            }

            var settingsRepository = new KonturSettingsRepository(ConnectionString);
            if (!IsOverrideEnabled(settingsRepository, timelineId))
            {
                return xmlBytes;
            }

            var roleAccessRepository = new KonturRoleAccessRepository(ConnectionString);
            var consignorRole = roleAccessRepository.FindActive("Kontur", "T1", "Consignor");
            var carrierRole = roleAccessRepository.FindActive("Kontur", "T2", "Carrier");
            var consigneeRole = roleAccessRepository.FindActive("Kontur", "T3", "Consignee");
            if (consignorRole == null || carrierRole == null || consigneeRole == null)
            {
                return xmlBytes;
            }

            var consignor = ResolveParticipant(consignorRole);
            var carrier = ResolveParticipant(carrierRole);
            var consignee = ResolveParticipant(consigneeRole);
            if (consignor == null || carrier == null || consignee == null)
            {
                return xmlBytes;
            }

            ApplyStageContextFallbacks(timelineId, consignor, carrier, consignee);

            var document = new XmlDocument();
            document.PreserveWhitespace = true;
            document.LoadXml(Encoding.GetEncoding(1251).GetString(xmlBytes));

            // Нормализуем все основные ролевые узлы документа, иначе Контур видит расхождение
            // между ИдФайл и телом черновика T1.
            ReplaceParticipantIdentity(document, "/Файл/Документ/СодИнфГО/СвГО/РекИдентГО/ИдСв", consignor);
            ReplaceParticipantIdentity(document, "/Файл/Документ/СодИнфГО/СвЗак/РекИдентЗак/ИдСв", consignor);
            ReplaceParticipantIdentity(document, "/Файл/Документ/СодИнфГО/СвГП/РекИдентГП/ИдСв", consignee);
            ReplaceParticipantIdentity(document, "/Файл/Документ/СодИнфГО/СвПер/ИдСв", carrier);
            ReplaceParticipantIdentity(document, "/Файл/Документ/СодИнфГО/СвПогруз/СвЛицПогрГр/РекЛицПогрГр/ИдСв", consignor);
            ApplyAddressFallback(document, "/Файл/Документ/СодИнфГО/СвГО/РекИдентГО/Адрес/АдрРФ", consignor);
            ApplyAddressFallback(document, "/Файл/Документ/СодИнфГО/СвЗак/РекИдентЗак/Адрес/АдрРФ", consignor);
            ApplyAddressFallback(document, "/Файл/Документ/СодИнфГО/СвГП/РекИдентГП/Адрес/АдрРФ", consignee);
            ApplyAddressFallback(document, "/Файл/Документ/СодИнфГО/СвГП/АдресДостГр/АдресРФ", consignee);
            ApplyAddressFallback(document, "/Файл/Документ/СодИнфГО/СвПер/Адрес/АдрРФ", carrier);
            ApplyAddressFallback(document, "/Файл/Документ/СодИнфГО/СвПогруз/СвЛицПогрГр/РекЛицПогрГр/Адрес/АдрРФ", consignor);

            // Простые идентификаторы владельца документа и связанных с ГО блоков
            // должны быть синхронны с ролью ГО после override.
            ReplaceSimpleInnIdentity(document, "/Файл/Документ/ОснДовОргСост/ИдРекСост", consignor);
            ReplaceSimpleInnIdentity(document, "/Файл/Документ/СодИнфГО/СвЗак/ДогУслПер/ИдРекСост", consignor);
            ReplaceSimpleInnIdentity(document, "/Файл/Документ/СодИнфГО/СвПогруз/СвЛицПогрГр/ИдентРекГО", consignor);
            ReplaceSimpleInnIdentity(document, "/Файл/Документ/СодИнфГО/СвПогруз/ВладИнфр/ИдентРекГО", consignor);
            ApplyDriverOverride(document);
            ApplySignerOverride(document, ResolveSelectedSigner(timelineId));

            var carrierFnsId = settingsRepository.GetSettingValue("Kontur", "T1FnsIdCarrier");
            var consigneeFnsId = settingsRepository.GetSettingValue("Kontur", "T1FnsIdConsignee");
            var consignorFnsId = settingsRepository.GetSettingValue("Kontur", "T1FnsIdConsignor");
            ApplyIdFileOverride(document, carrierFnsId, consigneeFnsId, consignorFnsId);

            var writerSettings = new XmlWriterSettings
            {
                Encoding = Encoding.GetEncoding(1251),
                Indent = false,
                NewLineHandling = NewLineHandling.None
            };

            using (var stream = new MemoryStream())
            using (var writer = XmlWriter.Create(stream, writerSettings))
            {
                document.Save(writer);
                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>
        /// Проверяет, включен ли режим override участников T1.
        /// </summary>
        /// <param name="settingsRepository">Репозиторий настроек Контур.</param>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <returns>True, если override включен; иначе false.</returns>
        /// <remarks>
        /// В штатном режиме источник истины остается за legacy-флагом настройки.
        /// В режиме Kontur-only T1 должен нормализоваться всегда, иначе тестовый подписант и состав XML
        /// снова разъедутся по разным моделям и сценарий потеряет смысл.
        /// </remarks>
        private bool IsOverrideEnabled(KonturSettingsRepository settingsRepository, long timelineId)
        {
            if (new KonturTestModeService(ConnectionString).IsEnabled(timelineId))
            {
                return true;
            }

            var enabledValue = settingsRepository.GetSettingValue("Kontur", "T1ParticipantsOverrideEnabled");
            return string.Equals((enabledValue ?? string.Empty).Trim(), "1", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Разрешает полные данные участника по записи ролевого доступа.
        /// </summary>
        /// <param name="roleRecord">Запись ролевого доступа Контур.</param>
        /// <returns>Данные участника для нормализации XML.</returns>
        /// <remarks>
        /// Нормализация использует не только ИНН/КПП, но и наименование организации,
        /// чтобы тело T1 не сохраняло старые названия от исходной заявки.
        /// </remarks>
        private T1OverrideParticipant ResolveParticipant(KonturRoleAccessRecord roleRecord)
        {
            if (roleRecord == null || string.IsNullOrEmpty(roleRecord.Inn))
            {
                return null;
            }

            string orgName = string.Empty;

            using (var connection = new SqlConnection(ConnectionString))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT TOP (1)
       K.name,
       ISNULL(A.postindex, '') AS postindex,
       ISNULL(R.kodreg, '') AS kodreg
FROM TKontragent AS K
LEFT JOIN TAdress AS A
       ON A.idkontragent = K.id
LEFT JOIN TRegion AS R
       ON R.id = A.region
WHERE K.inn = @Inn
ORDER BY CASE WHEN @Kpp <> '' AND ISNULL(K.kpp, '') = @Kpp THEN 0 ELSE 1 END,
         CASE WHEN ISNULL(A.type, 0) = 1 THEN 0 ELSE 1 END,
         CASE WHEN ISNULL(A.postindex, '') <> '' THEN 0 ELSE 1 END,
         K.id DESC;";
                command.Parameters.AddWithValue("@Inn", roleRecord.Inn ?? string.Empty);
                command.Parameters.AddWithValue("@Kpp", roleRecord.Kpp ?? string.Empty);
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        orgName = reader["name"] == DBNull.Value ? string.Empty : Convert.ToString(reader["name"]).Trim();

                        return new T1OverrideParticipant
                        {
                            Inn = (roleRecord.Inn ?? string.Empty).Trim(),
                            Kpp = (roleRecord.Kpp ?? string.Empty).Trim(),
                            Name = orgName,
                            PostIndex = reader["postindex"] == DBNull.Value ? string.Empty : Convert.ToString(reader["postindex"]).Trim(),
                            RegionCode = reader["kodreg"] == DBNull.Value ? string.Empty : Convert.ToString(reader["kodreg"]).Trim()
                        };
                    }
                }
            }

            return new T1OverrideParticipant
            {
                Inn = (roleRecord.Inn ?? string.Empty).Trim(),
                Kpp = (roleRecord.Kpp ?? string.Empty).Trim(),
                Name = orgName
            };
        }

        /// <summary>
        /// Дополняет участников T1 именами организаций из фактического контекста заявки.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="consignor">Участник роли грузоотправителя.</param>
        /// <param name="carrier">Участник роли перевозчика.</param>
        /// <param name="consignee">Участник роли грузополучателя.</param>
        /// <remarks>
        /// В тестовом контуре Контур ИНН/КПП участников могут браться из отдельного ролевого доступа,
        /// которого нет среди реальных контрагентов ТИС. В этом случае имя организации нельзя резолвить
        /// по тестовым реквизитам, поэтому оно добирается из живого контекста заявки по ролям этапов.
        /// </remarks>
        private void ApplyStageContextFallbacks(long timelineId, T1OverrideParticipant consignor, T1OverrideParticipant carrier, T1OverrideParticipant consignee)
        {
            FillParticipantFromStageContext(timelineId, "T1", consignor);
            FillParticipantFromStageContext(timelineId, "T2", carrier);
            FillParticipantFromStageContext(timelineId, "T3", consignee);
        }

        /// <summary>
        /// Дополняет имя и адресные реквизиты участника из контекста выбора подписанта соответствующего этапа.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код этапа, который соответствует роли участника.</param>
        /// <param name="participant">Участник, имя которого требуется дополнить.</param>
        /// <remarks>
        /// Контекст этапа строится от фактической заявки ТИС и знает корректное название организации роли,
        /// даже если тестовый ИНН Контур не существует как отдельный контрагент в справочнике.
        /// Адресные реквизиты дополнительно читаются по реальному id контрагента роли, чтобы адрес доставки
        /// в T1 не оставался без индекса только из-за тестовых реквизитов Контур.
        /// </remarks>
        private void FillParticipantFromStageContext(long timelineId, string stageCode, T1OverrideParticipant participant)
        {
            if (participant == null)
            {
                return;
            }

            var context = new KonturStageSignerService(ConnectionString).GetContext(timelineId, stageCode);
            if (context == null || !context.IsResolved)
            {
                return;
            }

            if (string.IsNullOrEmpty(participant.Name))
            {
                participant.Name = (context.RequiredKontragentName ?? string.Empty).Trim();
            }

            if (context.RequiredKontragentId > 0)
            {
                FillParticipantAddressByKontragentId(context.RequiredKontragentId, participant);
            }
        }

        /// <summary>
        /// Дополняет индекс и код региона участника по фактическому контрагенту роли этапа.
        /// </summary>
        /// <param name="kontragentId">Идентификатор контрагента из живой заявки ТИС.</param>
        /// <param name="participant">Участник, адрес которого требуется дополнить.</param>
        /// <remarks>
        /// В Kontur-only этот шаг отделяет тестовые реквизиты подписания от реального адресного контура заявки.
        /// Иначе Контур валидирует пустой индекс у грузополучателя, хотя в контексте роли реальный адрес может существовать.
        /// </remarks>
        private void FillParticipantAddressByKontragentId(long kontragentId, T1OverrideParticipant participant)
        {
            if (kontragentId <= 0 || participant == null)
            {
                return;
            }

            using (var connection = new SqlConnection(ConnectionString))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT TOP (1)
       ISNULL(A.postindex, '') AS postindex,
       ISNULL(R.kodreg, '') AS kodreg
  FROM TAdress AS A
  LEFT JOIN TRegion AS R
         ON R.id = A.region
 WHERE A.idkontragent = @KontragentId
 ORDER BY CASE WHEN ISNULL(A.type, 0) = 1 THEN 0 ELSE 1 END,
          CASE WHEN ISNULL(A.postindex, '') <> '' THEN 0 ELSE 1 END,
          A.id DESC;";
                command.Parameters.AddWithValue("@KontragentId", kontragentId);
                connection.Open();

                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return;
                    }

                    if (string.IsNullOrEmpty(participant.PostIndex))
                    {
                        participant.PostIndex = reader["postindex"] == DBNull.Value ? string.Empty : Convert.ToString(reader["postindex"]).Trim();
                    }

                    if (string.IsNullOrEmpty(participant.RegionCode))
                    {
                        participant.RegionCode = reader["kodreg"] == DBNull.Value ? string.Empty : Convert.ToString(reader["kodreg"]).Trim();
                    }
                }
            }
        }

        /// <summary>
        /// Полностью заменяет XML-описание участника в узле ИдСв.
        /// </summary>
        /// <param name="document">XML-документ титула T1.</param>
        /// <param name="idSvXPath">XPath к узлу ИдСв.</param>
        /// <param name="participant">Новые данные участника.</param>
        /// <remarks>
        /// Полная замена нужна, потому что простая подмена ИНН/КПП оставляет в XML старый тип участника
        /// и старое наименование организации, что приводит к бизнес-расхождению внутри черновика Контур.
        /// </remarks>
        private void ReplaceParticipantIdentity(XmlDocument document, string idSvXPath, T1OverrideParticipant participant)
        {
            if (document == null || participant == null)
            {
                return;
            }

            var identityNode = document.SelectSingleNode(idSvXPath) as XmlElement;
            if (identityNode == null)
            {
                return;
            }

            var currentOrgName = ResolveCurrentOrganizationName(identityNode);
            identityNode.RemoveAll();

            if (participant.IsLegalEntity)
            {
                var legalEntityNode = document.CreateElement("СвЮЛУч");
                legalEntityNode.SetAttribute("НаимОрг", !string.IsNullOrEmpty(participant.Name) ? participant.Name : currentOrgName);
                legalEntityNode.SetAttribute("ИННЮЛ", participant.Inn ?? string.Empty);
                legalEntityNode.SetAttribute("КПП", participant.Kpp ?? string.Empty);
                identityNode.AppendChild(legalEntityNode);
                return;
            }

            var entrepreneurNode = document.CreateElement("СвИП");
            entrepreneurNode.SetAttribute("ИННФЛ", participant.Inn ?? string.Empty);
            identityNode.AppendChild(entrepreneurNode);
        }

        /// <summary>
        /// Возвращает текущее название организации из существующего узла ИдСв до его пересборки.
        /// </summary>
        /// <param name="identityNode">Текущий узел ИдСв участника.</param>
        /// <returns>Текущее НаимОрг или пустую строку, если название отсутствует.</returns>
        /// <remarks>
        /// Этот fallback нужен, чтобы post-override не затирал корректное название организации пустотой,
        /// если реестр ролей Контур знает только ИНН/КПП, но не может резолвить имя через TKontragent.
        /// </remarks>
        private string ResolveCurrentOrganizationName(XmlElement identityNode)
        {
            if (identityNode == null)
            {
                return string.Empty;
            }

            var legalEntityNode = identityNode.SelectSingleNode("СвЮЛУч") as XmlElement;
            if (legalEntityNode == null)
            {
                return string.Empty;
            }

            return legalEntityNode.GetAttribute("НаимОрг") ?? string.Empty;
        }

        /// <summary>
        /// Полностью заменяет простой узел идентификатора владельца документа.
        /// </summary>
        /// <param name="document">XML-документ титула T1.</param>
        /// <param name="identityXPath">XPath к контейнеру идентификатора.</param>
        /// <param name="participant">Новые данные участника.</param>
        /// <remarks>
        /// Для перехода от ИП к ЮЛ важно менять не только значение, но и имя дочернего тега:
        /// ИННФЛ против ИННЮЛ.
        /// </remarks>
        private void ReplaceSimpleInnIdentity(XmlDocument document, string identityXPath, T1OverrideParticipant participant)
        {
            if (document == null || participant == null)
            {
                return;
            }

            var identityNode = document.SelectSingleNode(identityXPath) as XmlElement;
            if (identityNode == null)
            {
                return;
            }

            identityNode.RemoveAll();
            var childNode = document.CreateElement(participant.IsLegalEntity ? "ИННЮЛ" : "ИННФЛ");
            childNode.InnerText = participant.Inn ?? string.Empty;
            identityNode.AppendChild(childNode);
        }

        /// <summary>
        /// Дополняет адресный узел участника обязательными значениями из юридического адреса контрагента.
        /// </summary>
        /// <param name="document">XML-документ титула T1.</param>
        /// <param name="addressXPath">XPath к адресному узлу участника.</param>
        /// <param name="participant">Новые данные участника.</param>
        /// <remarks>
        /// Контур валидирует не только ИНН/КПП и НаимОрг, но и обязательные элементы адреса.
        /// Если legacy-builder или исходная заявка не дали индекс, post-override должен вернуть его
        /// из юридического адреса контрагента, иначе документ застревает на стадии черновика.
        /// </remarks>
        private void ApplyAddressFallback(XmlDocument document, string addressXPath, T1OverrideParticipant participant)
        {
            if (document == null || participant == null)
            {
                return;
            }

            var addressNode = document.SelectSingleNode(addressXPath) as XmlElement;
            if (addressNode == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(addressNode.GetAttribute("Индекс")) && !string.IsNullOrEmpty(participant.PostIndex))
            {
                addressNode.SetAttribute("Индекс", participant.PostIndex);
            }

            if (string.IsNullOrEmpty(addressNode.GetAttribute("КодРегион")) && !string.IsNullOrEmpty(participant.RegionCode))
            {
                addressNode.SetAttribute("КодРегион", participant.RegionCode);
            }
        }

        /// <summary>
        /// Доводит блок водителя до минимально валидного состояния для черновика Контур.
        /// </summary>
        /// <param name="document">XML-документ титула T1.</param>
        /// <remarks>
        /// Контур не пропускает документ, если в блоке водителя отсутствуют и валидный ИННФЛ, и полный набор реквизитов ВУ.
        /// В тестовом сценарии Kontur-only этот шаг нужен как защитный слой поверх legacy-builder,
        /// который может оставить только ФИО и телефон водителя.
        /// </remarks>
        private void ApplyDriverOverride(XmlDocument document)
        {
            if (document == null)
            {
                return;
            }

            var driverNode = document.SelectSingleNode("/Файл/Документ/СодИнфГО/СвВодит") as XmlElement;
            if (driverNode == null)
            {
                return;
            }

            var driverInn = (driverNode.GetAttribute("ИННФЛ") ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(driverInn) && !IsValidPersonalInn(driverInn))
            {
                // Невалидный ИННФЛ ломает отправку сильнее, чем его отсутствие.
                // Для Контура безопаснее опереться на реквизиты ВУ, чем тащить в итоговый XML тестовую заглушку.
                driverNode.RemoveAttribute("ИННФЛ");
                driverInn = string.Empty;
            }

            var hasDriverInn = !string.IsNullOrEmpty(driverInn);
            var hasLicenseSer = !string.IsNullOrEmpty(driverNode.GetAttribute("СерВУ"));
            var hasLicenseNum = !string.IsNullOrEmpty(driverNode.GetAttribute("НомВУ"));
            var hasLicenseDate = !string.IsNullOrEmpty(driverNode.GetAttribute("ДатаВыдВУ"));
            var hasCompleteLicense = hasLicenseSer && hasLicenseNum && hasLicenseDate;

            if (!hasDriverInn && !hasCompleteLicense)
            {
                // Контур валидирует этот блок по правилу "либо ИННФЛ, либо полный набор ВУ".
                // В реконструкционном слое не подставляем искусственный ИНН: он может пройти локальную подготовку,
                // но будет отклонен на серверной проверке провайдера.
                driverNode.SetAttribute("СерВУ", "12 34");
                driverNode.SetAttribute("НомВУ", "123456");
                driverNode.SetAttribute("ДатаВыдВУ", "01.01.2020");
            }

            var phoneNode = driverNode.SelectSingleNode("Тлф") as XmlElement;
            if (phoneNode != null && string.IsNullOrEmpty(phoneNode.InnerText))
            {
                phoneNode.InnerText = "б/н";
            }
        }

        /// <summary>
        /// Проверяет, что ИНН физического лица проходит контрольное соотношение ФНС.
        /// </summary>
        /// <param name="inn">Проверяемый ИННФЛ водителя.</param>
        /// <returns>True, если ИНН состоит из 12 цифр и проходит контрольные разряды; иначе false.</returns>
        /// <remarks>
        /// Проверка нужна только как защитный барьер реконструкционного слоя, чтобы в XML не попадали
        /// служебные и случайные значения, которые Контур отвергает еще до создания документа.
        /// </remarks>
        private bool IsValidPersonalInn(string inn)
        {
            if (string.IsNullOrEmpty(inn) || inn.Length != 12)
            {
                return false;
            }

            for (var i = 0; i < inn.Length; i++)
            {
                if (inn[i] < '0' || inn[i] > '9')
                {
                    return false;
                }
            }

            var digits = new int[12];
            for (var i = 0; i < inn.Length; i++)
            {
                digits[i] = inn[i] - '0';
            }

            var check11 = ((7 * digits[0] + 2 * digits[1] + 4 * digits[2] + 10 * digits[3] + 3 * digits[4] + 5 * digits[5] + 9 * digits[6] + 4 * digits[7] + 6 * digits[8] + 8 * digits[9]) % 11) % 10;
            if (check11 != digits[10])
            {
                return false;
            }

            var check12 = ((3 * digits[0] + 7 * digits[1] + 2 * digits[2] + 4 * digits[3] + 10 * digits[4] + 3 * digits[5] + 5 * digits[6] + 9 * digits[7] + 4 * digits[8] + 6 * digits[9] + 8 * digits[10]) % 11) % 10;
            return check12 == digits[11];
        }

        /// <summary>
        /// Пересобирает ИдФайл T1 по тестовым ФНС ИД ролей Контура.
        /// </summary>
        /// <param name="document">XML-документ T1.</param>
        /// <param name="carrierFnsId">ФНС ИД перевозчика.</param>
        /// <param name="consigneeFnsId">ФНС ИД грузополучателя.</param>
        /// <summary>
        /// Подменяет узел Подписант в итоговом XML T1 по выбранному подписанту этапа.
        /// </summary>
        /// <param name="document">XML-документ титула T1.</param>
        /// <param name="signer">Выбранный подписант этапа T1.</param>
        /// <remarks>
        /// Пост-нормализация нужна, чтобы итоговый XML не зависел от неявного
        /// выбора legacy-builder и совпадал с выбором оператора и фактической .sgn.
        /// </remarks>
        private void ApplySignerOverride(XmlDocument document, KonturStageSignerCandidate signer)
        {
            if (document == null || signer == null)
            {
                return;
            }

            var signerNode = document.SelectSingleNode("/Файл/Документ/Подписант") as XmlElement;
            if (signerNode == null)
            {
                return;
            }

            var fioNode = signerNode.SelectSingleNode("ФИО") as XmlElement;
            if (fioNode == null)
            {
                fioNode = document.CreateElement("ФИО");
                signerNode.AppendChild(fioNode);
            }

            string lastName;
            string firstName;
            string middleName;
            SplitSignerFio(signer.SignerFio, out lastName, out firstName, out middleName);
            if (string.IsNullOrEmpty(lastName) || string.IsNullOrEmpty(firstName))
            {
                return;
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
        /// Возвращает выбранного подписанта этапа T1 или null, если выбор еще недоступен.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <returns>Выбранный подписант этапа T1 или null.</returns>
        /// <remarks>
        /// Метод не бросает ошибку, потому что нормализация XML может вызываться
        /// и в диагностических сценариях до окончательного выбора подписанта.
        /// </remarks>
        private KonturStageSignerCandidate ResolveSelectedSigner(long timelineId)
        {
            if (new KonturTestModeService(ConnectionString).IsEnabled(timelineId))
            {
                return new KonturTestSigningContextService(ConnectionString).TryResolveSelectedSigner(timelineId, "T1");
            }

            return new KonturStageSignerService(ConnectionString).TryResolveSelectedSigner(timelineId, "T1");
        }

        /// <summary>
        /// Разбивает ФИО на фамилию, имя и отчество для узла ФИО.
        /// </summary>
        /// <param name="signerFio">Исходное ФИО подписанта.</param>
        /// <param name="lastName">Фамилия подписанта.</param>
        /// <param name="firstName">Имя подписанта.</param>
        /// <param name="middleName">Отчество подписанта.</param>
        /// <remarks>
        /// Для нашего контура достаточно формата "Фамилия Имя Отчество";
        /// если частей больше трех - остаток сохраняется в отчество.
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

        /// <param name="consignorFnsId">ФНС ИД грузоотправителя.</param>
        /// <remarks>ИдФайл должен ссылаться на те же роли, которые после override попадают в тело титула.</remarks>
        private void ApplyIdFileOverride(XmlDocument document, string carrierFnsId, string consigneeFnsId, string consignorFnsId)
        {
            if (document == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(carrierFnsId) || string.IsNullOrEmpty(consigneeFnsId) || string.IsNullOrEmpty(consignorFnsId))
            {
                return;
            }

            var root = document.DocumentElement;
            if (root == null)
            {
                return;
            }

            var currentIdFile = root.GetAttribute("ИдФайл");
            var guidPart = Guid.NewGuid().ToString();
            if (!string.IsNullOrEmpty(currentIdFile))
            {
                var parts = currentIdFile.Split('_');
                if (parts.Length > 0)
                {
                    var candidate = parts[parts.Length - 1];
                    Guid parsed;
                    if (Guid.TryParse(candidate, out parsed))
                    {
                        guidPart = candidate;
                    }
                }
            }

            var rebuilt = string.Format(
                "ON_TRNACLGROT_{0}_{1}_{2}_0_{3}_{4}",
                carrierFnsId.Trim(),
                consigneeFnsId.Trim(),
                consignorFnsId.Trim(),
                DateTime.Now.ToString("yyyyMMdd"),
                guidPart);

            root.SetAttribute("ИдФайл", rebuilt);
        }

        /// <summary>
        /// Контейнер нормализованных данных участника T1.
        /// </summary>
        private sealed class T1OverrideParticipant
        {
            /// <summary>
            /// Получает или задает ИНН участника.
            /// </summary>
            public string Inn { get; set; }

            /// <summary>
            /// Получает или задает КПП участника.
            /// </summary>
            public string Kpp { get; set; }

            /// <summary>
            /// Получает или задает наименование организации.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// Получает или задает почтовый индекс юридического адреса участника.
            /// </summary>
            public string PostIndex { get; set; }

            /// <summary>
            /// Получает или задает код региона юридического адреса участника.
            /// </summary>
            public string RegionCode { get; set; }

            /// <summary>
            /// Возвращает признак того, что участник является юридическим лицом.
            /// </summary>
            public bool IsLegalEntity
            {
                get { return !string.IsNullOrEmpty(Kpp); }
            }
        }
    }
}
