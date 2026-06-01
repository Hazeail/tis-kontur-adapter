// ETRNtituls.cs  v27.04.2026  .NET Framework 4.0.
// 02.04.2026 Зингаров — начальная версия
// 15.04.2026 Зингаров — EtrnPageHeader, GetPageHeader, CanSign
// 27.04.2026 Зингаров — EtrnHeader: CarrierPostIndex/CarrierKodRegion (юр. адрес ТК);
//                       SQLzay: JOIN TAdress/TRegion для TK (type=1);
//                       LoadHeader: заполняем CarrierPostIndex/CarrierKodRegion;
//                       Titul_1: принимает prebuiltIdFile как параметр (строит ETRN2026a.aspx.cs).
//                       GetAstralUserId убран: Connection.conStr() не имеет доступа к transinfoservice.
//
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using System.Web;
using System.Xml;
using System.Xml.Serialization;
using tis.Modules;
using TIS.EPD.ETRN.Mappers;

// =====================================================================
// ЭТРН — Единая внутренняя модель ТИС
// Оператор-независимая. Не под Астрал, не под Докробот.
// =====================================================================

/// <summary>
/// Корневая модель ЭТРН — передаётся в XML-построитель любого оператора.
/// </summary>
public class EtrnSourceModel
{
    public EtrnHeader            Header      { get; set; }
    public List<EtrnCargoRow>    CargoRows   { get; set; }
    public List<EtrnSignatory>   Signatories { get; set; }

    public EtrnSourceModel()
    {
        CargoRows   = new List<EtrnCargoRow>();
        Signatories = new List<EtrnSignatory>();
    }
}

// =====================================================================
// A. ШАПКА
// =====================================================================
/// <summary>
/// Шапка ЭТРН. Один экземпляр на документ.
/// Ключ привязки к ТИС: IdZayav (заявка).
/// </summary>
public class EtrnHeader
{
    // --- Идентификаторы в ТИС ---
    public int      IdReis          { get; set; }   // id рейса (Reis2)
    public int      IdZayav         { get; set; }   // id заявки
    public string   DocNumber       { get; set; }   // номер ТрН (TtnGruzNumber)
    public DateTime DocDate         { get; set; }   // дата документа

    // --- Участники перевозки ---
    // Грузоотправитель
    public string   ShipperInn      { get; set; }
    public string   ShipperKpp      { get; set; }
    public string   ShipperName     { get; set; }
    public string   ShipperPhone    { get; set; }

    // Грузополучатель
    public string   ConsigneeInn    { get; set; }
    public string   ConsigneeKpp    { get; set; }
    public string   ConsigneeName   { get; set; }
    public string   ConsigneePhone  { get; set; }

    // Заказчик перевозки
    public string   CustomerInn     { get; set; }
    public string   CustomerKpp     { get; set; }
    public string   CustomerName    { get; set; }
    public string   CustomerPhone   { get; set; }

    // Перевозчик
    public string   CarrierInn      { get; set; }
    public string   CarrierKpp      { get; set; }
    public string   CarrierName     { get; set; }
    public string   CarrierPhone    { get; set; }

    // Экспедитор (если есть)
    public string   ForwarderInn    { get; set; }
    public string   ForwarderKpp    { get; set; }
    public string   ForwarderName   { get; set; }

    // --- Транспорт ---
    public string   VehicleRegNum   { get; set; }   // гос. номер ТС
    public string   VehicleMarka    { get; set; }   // марка
    public string   TrailerRegNum   { get; set; }   // прицеп

    // --- Водитель ---
    public string   DriverFam       { get; set; }
    public string   DriverName      { get; set; }
    public string   DriverOtch      { get; set; }
    public string   DriverLicSer    { get; set; }   // серия ВУ
    public string   DriverLicNum    { get; set; }   // номер ВУ
    public string   DriverLicDate   { get; set; }   // дата выдачи ВУ
    public string   DriverPhone     { get; set; }
    public string   DriverInn       { get; set; }   // ИНН (legacy: img_contenttype)

    // --- Адрес погрузки ---
    public string   LoadPostIndex   { get; set; }
    public string   LoadKodRegion   { get; set; }
    public string   LoadGorodType   { get; set; }
    public string   LoadGorodName   { get; set; }
    public string   LoadUlica       { get; set; }
    public string   LoadDom         { get; set; }
    public string   LoadRaion       { get; set; }

    // --- Адрес разгрузки ---
    public string   UnloadPostIndex { get; set; }
    public string   UnloadKodRegion { get; set; }
    public string   UnloadGorodType { get; set; }
    public string   UnloadGorodName { get; set; }
    public string   UnloadUlica     { get; set; }
    public string   UnloadDom       { get; set; }
    public string   UnloadRaion     { get; set; }

    // --- Юридический адрес ГО ---
    public string   ShipperPostIndex { get; set; }
    public string   ShipperKodRegion { get; set; }
    public string   ShipperGorodType { get; set; }
    public string   ShipperGorodName { get; set; }
    public string   ShipperUlica     { get; set; }
    public string   ShipperDom       { get; set; }
    public string   ShipperKorpus    { get; set; }
    public string   ShipperKvartira  { get; set; }

    // 27.04.2026 Зингаров — юр. адрес ТК (перевозчика); ранее для СвПер ошибочно
    //            использовался LoadPostIndex/LoadKodRegion (адрес склада погрузки).
    public string   CarrierPostIndex { get; set; }
    public string   CarrierKodRegion { get; set; }

    // --- Даты / время ---
    public DateTime? LoadingDatePlan    { get; set; }
    public DateTime? UnloadingDatePlan  { get; set; }
    public DateTime? LoadingDateFact    { get; set; }
    public DateTime? UnloadingDateFact  { get; set; }

    // --- Основание перевозки ---
    public string   TransportConditions { get; set; }
    public decimal  GruzBrutto          { get; set; }
    public int      GruzMesta           { get; set; }

    // --- Номер и дата заявки ---
    public string   ZayNumber       { get; set; }
    public DateTime? ZayDate        { get; set; }

    // --- Внутренние ID для загрузки подписантов ---
    public int ShipperOrgId  { get; set; }  // TKontragent.id ГО
    public int CarrierOrgId  { get; set; }  // TKontragent.id перевозчика
    public int IdReis2       { get; set; }  // id главного рейса
}

// =====================================================================
// B. ГРУЗЫ
// =====================================================================
/// <summary>
/// Строка груза в ЭТРН. Одна строка = один вид груза.
/// </summary>
public class EtrnCargoRow
{
    public int      RowNum      { get; set; }   // порядковый номер
    public string   CargoName   { get; set; }   // наименование
    public string   CargoCode   { get; set; }   // GFR.ide — единица измерения (id)
    public decimal? PlacesCount { get; set; }   // GFR.kol
    public string   PlacesText  { get; set; }   // GFR.kolpropis (прописью)
    public decimal? WeightTon   { get; set; }   // GFR.vestonna (в тоннах)
    public decimal? FaktKol     { get; set; }   // GFR.faktkol
    public decimal? FaktVes     { get; set; }   // GFR.faktves
    public string   Dimensions  { get; set; }   // GFR.gabarit
    // Поля для будущего расширения
    public string   PackageType { get; set; }
    public string   Marking     { get; set; }
    public string   HazardClass { get; set; }
    public string   Comment     { get; set; }
}

// =====================================================================
// C. ПОДПИСАНТЫ / УПОЛНОМОЧЕННЫЕ ЛИЦА
// =====================================================================
/// <summary>
/// Уполномоченное лицо / подписант.
/// </summary>
public class EtrnSignatory
{
    public string    Role        { get; set; }  // см. EtrnSignatoryRole
    public int       OrgId       { get; set; }  // TKontragent.id
    public string    OrgInn      { get; set; }
    public string    OrgName     { get; set; }
    public string    Fam         { get; set; }
    public string    FirstName   { get; set; }
    public string    Otch        { get; set; }
    public string    Position    { get; set; }  // должность (TDlg.name)
    public string    AuthDocType { get; set; }  // тип основания (TRukAndUL.tip)
    public DateTime? AuthDocDate { get; set; }
    public string    Phone       { get; set; }
    public string    Email       { get; set; }
}

// =====================================================================
// КОНСТАНТЫ РОЛЕЙ
// =====================================================================
public static class EtrnSignatoryRole
{
    public const string Shipper   = "Shipper";
    public const string Consignee = "Consignee";
    public const string Carrier   = "Carrier";
    public const string Customer  = "Customer";
    public const string Forwarder = "Forwarder";
    public const string Driver    = "Driver";
}

// =====================================================================
// D. ШАПКА СТРАНИЦЫ (минимальный контекст, отдельный от полного T1)
// =====================================================================
/// <summary>
/// Минимальный контекст для отображения в шапке страницы ЭТРН.
/// Загружается отдельным лёгким SQL, не смешивается с загрузкой T1.
/// </summary>
public class EtrnPageHeader
{
    public int       IdZay     { get; set; }   // id заявки
    public string    ZayNumber { get; set; }   // номер заявки (TZayavka.number)
    public DateTime? ZayDate   { get; set; }   // дата отправки заказчику (dateOtprZakom)
    public int       IdReis    { get; set; }   // id главного рейса (Reis2.id)
    public int       GoOrgId   { get; set; }   // TKontragent.id грузоотправителя
}

// =====================================================================
// ОСНОВНОЙ КЛАСС — SQL, загрузка, фабрика модели
// =====================================================================
public static class ETRNtituls
{
    private static readonly string ConnString = Connection.conStr();
    const char parag = '§';  // разделитель

    // -----------------------------------------------------------------
    // ФАБРИКА: собрать полную модель по idZay
    // -----------------------------------------------------------------
    public static EtrnSourceModel GetEtrnData(string idZay, out string message)
    {
        message = "";
        var model = new EtrnSourceModel();
        try
        {
            model.Header      = LoadHeader(idZay);
            model.CargoRows   = LoadCargoRows(idZay);
            model.Signatories = LoadSignatories(
                model.Header.ShipperOrgId,
                model.Header.CarrierOrgId
            );
        }
        catch (Exception ex)
        {
            message = ex.Message;
        }
        return model;
    }

    // -----------------------------------------------------------------
    // GetPageHeader — минимальный контекст для шапки страницы.
    // Отдельный лёгкий SQL, не связан с загрузкой T1.
    // -----------------------------------------------------------------
    public static EtrnPageHeader GetPageHeader(string idZay, out string message)
    {
        message = "";
        if (string.IsNullOrWhiteSpace(idZay)) { message = "Не передан id заявки."; return null; }

        string sql = @"SET DATEFORMAT DMY;
SELECT
    Zay.id                AS idZay,
    Zay.number            AS zayNumber,
    Zay.dateOtprZakom     AS zayDate,
    Reis2.id              AS idReis,
    TZR.idOtpravitel      AS goOrgId
FROM TZayavka AS Zay
    LEFT JOIN TZRekviz AS TZR  ON TZR.idn  = Zay.id
    LEFT JOIN TZayavka AS Reis ON Reis.id   = Zay.idzakaz
    LEFT JOIN TZayavka AS Reis2 ON Reis2.id =
        CASE WHEN Reis.idzakaz = 0 OR Reis.idzakaz IS NULL
             THEN Reis.id ELSE Reis.idzakaz END
WHERE Zay.id = " + idZay;

        DataTable dt = new DataTable();
        try
        {
            using (SqlConnection con = new SqlConnection(ConnString))
            using (SqlDataAdapter da = new SqlDataAdapter(sql, con))
                da.Fill(dt);
        }
        catch (Exception ex) { message = ex.Message; return null; }

        if (dt.Rows.Count == 0) { message = "Заявка id=" + idZay + " не найдена."; return null; }

        DataRow r = dt.Rows[0];
        return new EtrnPageHeader
        {
            IdZay     = ToInt(r, "idZay"),
            ZayNumber = Str(r, "zayNumber"),
            ZayDate   = ToDateNull(r, "zayDate"),
            IdReis    = ToInt(r, "idReis"),
            GoOrgId   = ToInt(r, "goOrgId"),
        };
    }

    // -----------------------------------------------------------------
    // CanSign — есть ли у текущего пользователя право подписывать.
    //
    // userId       = Session["UserId"]          (TFizLico.id)
    // kontragentId = Session["LoginKontragent"] (TKontragent.id — орг. логина)
    //
    // Годятся роли: 0=Руководитель, 1=Бухгалтер, 2=Зам.руководителя,
    //               3=Зам.бухгалтера, 7=Ответственный за ТН, 14=ЭЦП заказа,
    //               15=МЧД ЭЦП заявки
    // -----------------------------------------------------------------
    public static bool CanSign(string userId, string kontragentId,
                               out string positionName, out string message)
    {
        positionName = "";
        message      = "";

        if (string.IsNullOrEmpty(userId) || userId == "0") return false;
        if (string.IsNullOrEmpty(kontragentId) || kontragentId == "0") return false;

        string sql = @"SET DATEFORMAT DMY;
SELECT TOP 1
    TDlg.name AS doljnost
FROM TRukAndUL AS RL
    LEFT JOIN TFizLico AS FL ON FL.id  = RL.idFizL
    LEFT JOIN TDlg          ON TDlg.id = FL.iddlg
WHERE RL.idFizL      = " + userId + @"
  AND RL.idvladelec  = " + kontragentId + @"
  AND RL.zdolg IN (0, 1, 2, 3, 7, 14, 15)
  AND RL.del      = 0
  AND RL.used     = 1
  AND RL.databegin <= GETDATE()
  AND RL.dataend   >= CAST(GETDATE() AS date)
ORDER BY RL.zdolg";

        DataTable dt = new DataTable();
        try
        {
            using (SqlConnection con = new SqlConnection(ConnString))
            using (SqlDataAdapter da = new SqlDataAdapter(sql, con))
                da.Fill(dt);
        }
        catch (Exception ex) { message = ex.Message; return false; }

        if (dt.Rows.Count == 0) return false;

        object v = dt.Rows[0]["doljnost"];
        positionName = (v == null || v == DBNull.Value) ? "" : v.ToString().Trim();
        return true;
    }

    // -----------------------------------------------------------------
    // Titul_1 — публичная точка входа: idzay -> модель -> T1 XML
    // -----------------------------------------------------------------
    public static string Titul_1(string idZay, out string errorstr, string prebuiltIdFile = null)
    {
        errorstr = "";

        if (string.IsNullOrWhiteSpace(idZay))
        {
            errorstr = "Не передан id заявки.";
            return "";
        }

        EtrnSourceModel model = GetEtrnData(idZay, out errorstr);
        if (!string.IsNullOrEmpty(errorstr))
            return "";

        if (model == null || model.Header == null)
        {
            errorstr = "Не удалось собрать исходную модель ЭТРН.";
            return "";
        }

        // prebuiltIdFile строится в ETRN2026a.aspx.cs через AstralKontRepo и передаётся сюда.
        // Connection.conStr() не имеет доступа к transinfoservice.dbo.AstralKont,
        // поэтому логика вынесена туда где AstralKontRepo доступен.
        var file = ET1Mapper.MapToT1(model, out errorstr, prebuiltIdFile);
        if (!string.IsNullOrEmpty(errorstr) || file == null)
        {
            if (string.IsNullOrEmpty(errorstr))
                errorstr = "ET1Mapper не вернул объект Т1.";
            return "";
        }

        try
        {
            // 27.04.2026 Зингаров — кодировка CP1251 вместо UTF-8.
            // EpdRepo.GetLatestXmlBytes конвертирует строку→байты через CP1251.
            // Декларация encoding="windows-1251" должна совпадать с реальными байтами,
            // иначе Астрал-парсер падает с "unable to determine document type".
            // Контур также использует WINDOWS-1251 для ЭТРН XML.
            var cp1251 = Encoding.GetEncoding(1251);
            XmlSerializer serializer = new XmlSerializer(file.GetType());
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Encoding           = cp1251;
            settings.Indent             = true;
            settings.OmitXmlDeclaration = false;

            using (var ms = new MemoryStream())
            using (XmlWriter xw = XmlWriter.Create(ms, settings))
            {
                serializer.Serialize(xw, file);
                xw.Flush();
                return cp1251.GetString(ms.ToArray());
            }
        }
        catch (Exception ex)
        {
            errorstr = "Ошибка сериализации T1 XML: " + ex.Message;
            return "";
        }
    }

    // -----------------------------------------------------------------
    // LoadHeader — шапка ЭТРН
    // -----------------------------------------------------------------
    private static EtrnHeader LoadHeader(string idZay)
    {
        string sqlm = SQLzay(idZay);
        DataTable dt = new DataTable();
        using (SqlConnection con = new SqlConnection(ConnString))
        using (SqlDataAdapter da = new SqlDataAdapter(sqlm, con))
            da.Fill(dt);

        if (dt.Rows.Count == 0)
            throw new Exception("Заявка не найдена: idZay=" + idZay);

        DataRow r = dt.Rows[0];

        EtrnHeader h = new EtrnHeader();

        h.IdZayav   = ToInt(r, "idZay");
        h.IdReis    = ToInt(r, "idReis");
        h.IdReis2   = ToInt(r, "idReis");   // idReis уже = Reis2.id после логики в SQL
        // DocNumber: если номер ТрН не заполнен — берём номер заявки как постоянный суррогат
        string ttnNum = Str(r, "ttnNum");
        h.ZayNumber = Str(r, "zayNumber");
        h.DocNumber = !string.IsNullOrEmpty(ttnNum) ? ttnNum : h.ZayNumber;
        h.DocDate   = ToDateOrMin(r, "ttnDate");
        h.ZayDate   = ToDateNull(r, "zayDate");

        // Участники
        h.ShipperInn   = Str(r, "goInn");
        h.ShipperKpp   = Str(r, "goKpp");
        h.ShipperName  = Str(r, "goName");
        h.ShipperPhone = Str(r, "goPhoneKod") + Str(r, "goPhone");

        h.ConsigneeInn   = Str(r, "gpInn");
        h.ConsigneeKpp   = Str(r, "gpKpp");
        h.ConsigneeName  = Str(r, "gpName");
        h.ConsigneePhone = Str(r, "gpPhoneKod") + Str(r, "gpPhone");

        h.CarrierInn   = Str(r, "tkInn");
        h.CarrierKpp   = Str(r, "tkKpp");
        h.CarrierName  = Str(r, "tkName");
        h.CarrierPhone = Str(r, "tkPhoneKod") + Str(r, "tkPhone");

        h.CustomerInn   = Str(r, "zakInn");
        h.CustomerKpp   = Str(r, "zakKpp");
        h.CustomerName  = Str(r, "zakName");
        h.CustomerPhone = Str(r, "zakPhoneKod") + Str(r, "zakPhone");

        // Водитель
        h.DriverFam     = Str(r, "vodFam");
        h.DriverName    = Str(r, "vodName");
        h.DriverOtch    = Str(r, "vodOtch");
        h.DriverLicSer  = Str(r, "vodLicSer");
        h.DriverLicNum  = Str(r, "vodLicNum");
        h.DriverLicDate = Str(r, "vodLicDate");
        h.DriverPhone   = Str(r, "vodPhoneKod") + Str(r, "vodPhone");
        string vodInnRaw = Str(r, "vodInn");
        h.DriverInn = vodInnRaw.Length >= 20
            ? vodInnRaw.Substring(0, 20).Trim()
            : vodInnRaw.Trim();

        // Транспорт
        h.VehicleRegNum = Str(r, "amGosNomer");
        h.VehicleMarka  = (Str(r, "amProizvod") + " " + Str(r, "amMarka")).Trim();
        h.TrailerRegNum = Str(r, "pricepGosNomer");

        // Адрес погрузки
        h.LoadPostIndex  = Str(r, "postindexsPog");
        h.LoadKodRegion  = Str(r, "kodregsPog");
        h.LoadGorodType  = Str(r, "typegorodsPog");
        h.LoadGorodName  = Str(r, "gorodnamesPog");
        h.LoadUlica      = Str(r, "ulicasPog");
        h.LoadDom        = Str(r, "domsPog");
        h.LoadRaion      = Str(r, "raionsPog");

        // Адрес разгрузки
        h.UnloadPostIndex = Str(r, "postindexsRaz");
        h.UnloadKodRegion = Str(r, "kodregsRaz");
        h.UnloadGorodType = Str(r, "typegorodsRaz");
        h.UnloadGorodName = Str(r, "gorodnamesRaz");
        h.UnloadUlica     = Str(r, "ulicasRaz");
        h.UnloadDom       = Str(r, "domsRaz");
        h.UnloadRaion     = Str(r, "raionsRaz");

        // Юр. адрес ГО
        h.ShipperPostIndex = Str(r, "postindexsGO");
        h.ShipperKodRegion = Str(r, "kodregsGO");
        h.ShipperGorodType = Str(r, "typegorodsGO");
        h.ShipperGorodName = Str(r, "gorodnamesGO");
        h.ShipperUlica     = Str(r, "ulicasGO");
        h.ShipperDom       = Str(r, "domsGO");
        h.ShipperKorpus    = Str(r, "korpussGO");
        h.ShipperKvartira  = Str(r, "kvartirasGO");

        // 27.04.2026 Зингаров — юр. адрес ТК (перевозчика)
        h.CarrierPostIndex = Str(r, "postindexsTK");
        h.CarrierKodRegion = Str(r, "kodregsTK");

        // Даты
        h.LoadingDatePlan   = ToDateNull(r, "dateLoadPlan");
        h.UnloadingDatePlan = ToDateNull(r, "dateUnloadPlan");
        h.LoadingDateFact   = ToDateNull(r, "dateLoadFact");
        h.UnloadingDateFact = ToDateNull(r, "dateUnloadFact");

        // Прочее
        h.TransportConditions = Str(r, "transportConditions");
        h.GruzBrutto = ToDecimal(r, "gruzBrutto");
        h.GruzMesta  = ToInt(r, "gruzMesta");

        // ID для подписантов — берём прямо из SQL (goOrgId, tkOrgId уже в SELECT)
        h.ShipperOrgId = ToInt(r, "goOrgId");
        h.CarrierOrgId = ToInt(r, "tkOrgId");

        return h;
    }

    // -----------------------------------------------------------------
    // LoadCargoRows — грузы
    // -----------------------------------------------------------------
    private static List<EtrnCargoRow> LoadCargoRows(string idZay)
    {
        string sqlm = SQLcargo(idZay);
        DataTable dt = new DataTable();
        using (SqlConnection con = new SqlConnection(ConnString))
        using (SqlDataAdapter da = new SqlDataAdapter(sqlm, con))
            da.Fill(dt);

        var list = new List<EtrnCargoRow>();
        foreach (DataRow r in dt.Rows)
        {
            list.Add(new EtrnCargoRow
            {
                RowNum     = ToInt(r, "rowNum"),
                CargoName  = Str(r, "cargoName"),
                CargoCode  = Str(r, "cargoCode"),
                PlacesCount = ToDecimalNull(r, "placesCount"),
                PlacesText  = Str(r, "placesText"),
                WeightTon   = ToDecimalNull(r, "weightTon"),
                FaktKol     = ToDecimalNull(r, "faktKol"),
                FaktVes     = ToDecimalNull(r, "faktVes"),
                Dimensions  = Str(r, "dimensions"),
            });
        }
        return list;
    }

    // -----------------------------------------------------------------
    // LoadSignatories — уполномоченные лица ГО и Перевозчика
    // -----------------------------------------------------------------
    private static List<EtrnSignatory> LoadSignatories(int shipperOrgId, int carrierOrgId)
    {
        var list = new List<EtrnSignatory>();
        if (shipperOrgId == 0 && carrierOrgId == 0) return list;

        string sqlm = SQLsignatories(shipperOrgId, carrierOrgId);
        DataTable dt = new DataTable();
        using (SqlConnection con = new SqlConnection(ConnString))
        using (SqlDataAdapter da = new SqlDataAdapter(sqlm, con))
            da.Fill(dt);

        // Берём первого подписанта для каждой организации (ORDER уже в SQL).
        // Если ГО и перевозчик — одна организация (ИНН совпадают),
        // запись будет одна; роль Shipper приоритетнее.
        bool shipperDone = false;
        bool carrierDone = false;
        foreach (DataRow r in dt.Rows)
        {
            int orgId = ToInt(r, "orgId");
            string role = null;
            if (orgId == shipperOrgId && !shipperDone)
            {
                role = EtrnSignatoryRole.Shipper;
                shipperDone = true;
            }
            else if (orgId == carrierOrgId && !carrierDone)
            {
                role = EtrnSignatoryRole.Carrier;
                carrierDone = true;
            }
            if (role == null) continue;

            list.Add(new EtrnSignatory
            {
                Role        = role,
                OrgId       = orgId,
                Fam         = Str(r, "fam"),
                FirstName   = Str(r, "firstName"),
                Otch        = Str(r, "otch"),
                Position    = Str(r, "position"),
                AuthDocType = Str(r, "authDocType"),
                AuthDocDate = ToDateNull(r, "authDocDate"),
            });
        }
        return list;
    }

    // -----------------------------------------------------------------
    // Уполномоченные (из ЭЗЗ — для текущего пользователя)
    // -----------------------------------------------------------------
    public static string[] getUpolnom(out string message)
    {
        message = "";
        DataTable dtupol = new DataTable();
        string[] upoln = { "", "", "", "", "", "" };
        string sqlm = "SET DATEFORMAT DMY;"
            + "SELECT TRukAndUl.*,TFizLico.fam,TFizLico.name as fizname,TFizLico.otch,TDlg.name AS doljnost"
            + ", telef=CONCAT(RTRIM(TFizLico.phonekod),RTRIM(TFizLico.phone))"
            + " FROM TRukAndUL"
            + " LEFT JOIN TFizLico ON TFizLico.id=TRukAndUL.idFizL AND TFizLico.del='false'"
            + " LEFT JOIN TDlg ON TDlg.id=TFizLico.iddlg"
            + " WHERE TRukAndUL.idvladelec='" + HttpContext.Current.Session["LoginKontragent"].ToString() + "'"
            + "  AND TRukAndUL.idFizL='"      + HttpContext.Current.Session["UserId"].ToString()          + "'"
            + "  AND TRukAndUL.del='false' AND used='true'"
            + "  AND databegin<='" + DateTime.Now.ToString("dd.MM.yyyy") + "'"
            + "  AND dataend>='"   + DateTime.Now.ToString("dd.MM.yyyy") + "'"
            + " ORDER BY zdolg";

        using (SqlConnection dbCon = new SqlConnection(ConnString))
        using (SqlDataAdapter dar = new SqlDataAdapter(sqlm, dbCon))
        {
        try { dar.Fill(dtupol); }
        catch (Exception sqlerr) { message = sqlerr.ToString(); return null; }
        }

        if (dtupol.Rows.Count >= 1)
        {
            DataRow drup = dtupol.Rows[0];
            Int64 zdolg = (Int64)drup["zdolg"];
            if (zdolg < 4 || zdolg == 7 || zdolg == 14)
            {
                upoln[0] = drup["doljnost"].ToString().Trim();
                upoln[1] = drup["fam"].ToString().Trim();
                upoln[2] = drup["fizname"].ToString().Trim();
                upoln[3] = drup["otch"].ToString().Trim();
                upoln[4] = drup["telef"].ToString();
            }
        }
        message = "";
        return upoln;
    }

    // -----------------------------------------------------------------
    // getZayData — публичный метод возврата DataTable (для обратной
    //              совместимости / отладки на экране)
    // -----------------------------------------------------------------
    public static DataTable getZayData(string idzak, out string message)
    {
        DataTable dtzay = new DataTable();
        message = "";
        string sqlm = SQLzay(idzak);
        using (SqlConnection dbCon = new SqlConnection(ConnString))
        using (SqlDataAdapter dar = new SqlDataAdapter(sqlm, dbCon))
        {
        try { dar.Fill(dtzay); }
        catch (Exception sqlerr) { message = sqlerr.ToString(); return null; }
        }
        return dtzay;
    }

    // =================================================================
    // SQL-ТЕКСТЫ
    // =================================================================

    public static string SQLcargo(string idzay)
    {
        return @"SET DATEFORMAT DMY;
SELECT
    ROW_NUMBER() OVER (ORDER BY GFR.id) AS rowNum,
    GFR.id,
    GFR.name         AS cargoName,
    GFR.ide          AS cargoCode,
    GFR.kol          AS placesCount,
    GFR.kolpropis    AS placesText,
    GFR.vestonna     AS weightTon,
    GFR.faktkol      AS faktKol,
    GFR.faktves      AS faktVes,
    GFR.gabarit      AS dimensions
FROM TGruzFromZayavka AS GFR
WHERE GFR.idz = " + idzay + @"
ORDER BY GFR.id";
    }

    public static string SQLzay(string idzak)
    {
        return @"SET DATEFORMAT DMY;
SELECT
    Zay.id            AS idZay,
    Reis2.id          AS idReis,
    TZR.TtnGruzNumber  AS ttnNum,
    TZR.TtnGruzData    AS ttnDate,
    TZR.dap62Pogruzgruz  AS dateLoadPlan,
    TZR.dap95Razgruzgruz AS dateUnloadPlan,
    TZR.daF62Pogruzgruz  AS dateLoadFact,
    TZR.daF95razgruzgruz AS dateUnloadFact,
    TZR.gruzUslPerevozki AS transportConditions,
    TZR.gruzBrutto,
    TZR.gruzMesta,
    GO.id    AS goOrgId,
    GO.inn   AS goInn,
    GO.kpp   AS goKpp,
    GO.name  AS goName,
    GO.phone AS goPhone, GO.phonekod AS goPhoneKod,
    GP.inn   AS gpInn,
    GP.kpp   AS gpKpp,
    GP.name  AS gpName,
    GP.phone AS gpPhone, GP.phonekod AS gpPhoneKod,
    TK.id    AS tkOrgId,
    TK.inn   AS tkInn,
    TK.kpp   AS tkKpp,
    TK.name  AS tkName,
    TK.phone AS tkPhone, TK.phonekod AS tkPhoneKod,
    Zak.inn      AS zakInn,
    Zak.kpp      AS zakKpp,
    Zak.bigname  AS zakName,
    Zak.phone    AS zakPhone, Zak.phonekod AS zakPhoneKod,
    Vod.fam          AS vodFam,
    Vod.name         AS vodName,
    Vod.otch         AS vodOtch,
    Vod.vod_ser      AS vodLicSer,
    Vod.vod_nom      AS vodLicNum,
    Vod.vod_data     AS vodLicDate,
    Vod.phonekod     AS vodPhoneKod,
    Vod.phone        AS vodPhone,
    Vod.img_contenttype AS vodInn,
    AM.gosnomer       AS amGosNomer,
    TMark.name        AS amMarka,
    TProiz.name       AS amProizvod,
    Pricep.gosnomer   AS pricepGosNomer,
    AdrPog.postindex     AS postindexsPog,
    RegPog.kodreg        AS kodregsPog,
    GorPog.name          AS gorodnamesPog,
    TGorPog.shortname    AS typegorodsPog,
    AdrPog.ulica         AS ulicasPog,
    AdrPog.dom           AS domsPog,
    AdrPog.raion         AS raionsPog,
    AdrRaz.postindex     AS postindexsRaz,
    RegRaz.kodreg        AS kodregsRaz,
    GorRaz.name          AS gorodnamesRaz,
    TGorRaz.shortname    AS typegorodsRaz,
    AdrRaz.ulica         AS ulicasRaz,
    AdrRaz.dom           AS domsRaz,
    AdrRaz.raion         AS raionsRaz,
    AdrGO.postindex      AS postindexsGO,
    RegGO.kodreg         AS kodregsGO,
    GorGO.name           AS gorodnamesGO,
    TGorGO.shortname     AS typegorodsGO,
    AdrGO.ulica          AS ulicasGO,
    AdrGO.dom            AS domsGO,
    AdrGO.korpus         AS korpussGO,
    AdrGO.kvartira       AS kvartirasGO,
    -- 27.04.2026 Зингаров — юр. адрес ТК (тип=1); нужен для СвПер в T1
    AdrTK.postindex      AS postindexsTK,
    RegTK.kodreg         AS kodregsTK,
    Zay.number        AS zayNumber,
    Zay.dateOtprZakom AS zayDate
FROM TZayavka AS Zay
    LEFT JOIN TZRekviz AS TZR     ON TZR.idn = Zay.id
    LEFT JOIN TZayavka AS Reis    ON Reis.id  = Zay.idzakaz
    LEFT JOIN TZayavka AS Reis2   ON Reis2.id =
        CASE WHEN Reis.idzakaz = 0 OR Reis.idzakaz IS NULL
             THEN Reis.id
             ELSE Reis.idzakaz
        END
    LEFT JOIN TKontragent AS GO   ON GO.id  = TZR.idOtpravitel
    LEFT JOIN TKontragent AS GP   ON GP.id  = TZR.idPoluchatel
    LEFT JOIN TKontragent AS TK   ON TK.id  = Reis2.idIspolnitel
    LEFT JOIN TKontragent AS Zak  ON Zak.id = Zay.idZakazchik
    LEFT JOIN TKontragent AS Sclot    ON Sclot.id   = TZR.idPogrSclad
    LEFT JOIN TKontragent AS Scltuda  ON Scltuda.id = TZR.idPoluSclad
    LEFT JOIN TReisRekv           ON TReisRekv.idn  = Reis2.id
    LEFT JOIN TFizLico AS Vod     ON Vod.id = TReisRekv.idVoditel
    LEFT JOIN TTransport AS AM    ON AM.id  = TReisRekv.idAM
    LEFT JOIN TTransport AS Pricep ON Pricep.id = TReisRekv.idPricep
    LEFT JOIN TTransportMarka  AS TMark  ON TMark.id  = AM.idTransportMarka
    LEFT JOIN TTransportProizvod AS TProiz ON TProiz.id = TMark.idTransportProizvod
    LEFT JOIN TAdress AS AdrPog   ON AdrPog.idkontragent = Sclot.id   AND AdrPog.type = 2
    LEFT JOIN TRegion AS RegPog   ON RegPog.id = AdrPog.region
    LEFT JOIN TGorod  AS GorPog   ON GorPog.id = AdrPog.gorod
    LEFT JOIN TTypeGorod AS TGorPog ON TGorPog.id = AdrPog.typegorod
    LEFT JOIN TAdress AS AdrRaz   ON AdrRaz.idkontragent = Scltuda.id AND AdrRaz.type = 2
    LEFT JOIN TRegion AS RegRaz   ON RegRaz.id = AdrRaz.region
    LEFT JOIN TGorod  AS GorRaz   ON GorRaz.id = AdrRaz.gorod
    LEFT JOIN TTypeGorod AS TGorRaz ON TGorRaz.id = AdrRaz.gorod
    LEFT JOIN TAdress AS AdrGO    ON AdrGO.idkontragent = GO.id AND AdrGO.type = 1
    LEFT JOIN TRegion AS RegGO    ON RegGO.id = AdrGO.region
    LEFT JOIN TGorod  AS GorGO    ON GorGO.id = AdrGO.gorod
    LEFT JOIN TTypeGorod AS TGorGO ON TGorGO.id = AdrGO.typegorod
    -- 27.04.2026 Зингаров — юр. адрес ТК (тип=1)
    LEFT JOIN TAdress AS AdrTK    ON AdrTK.idkontragent = TK.id AND AdrTK.type = 1
    LEFT JOIN TRegion AS RegTK    ON RegTK.id = AdrTK.region
WHERE Zay.id = " + idzak;
    }

    private static string SQLsignatories(int shipperOrgId, int carrierOrgId)
    {
        return @"SET DATEFORMAT DMY;
SELECT
    RL.idvladelec    AS orgId,
    TDlg.name        AS position,
    FL.fam           AS fam,
    FL.name          AS firstName,
    FL.otch          AS otch,
    RL.tip           AS authDocType,
    RL.datadoc       AS authDocDate,
    RL.zdolg         AS roleCode
FROM TRukAndUL AS RL
    LEFT JOIN TFizLico AS FL ON FL.id = RL.idFizL
    LEFT JOIN TDlg          ON TDlg.id = FL.iddlg
WHERE RL.del = 0
    AND RL.idvladelec IN (" + shipperOrgId + ", " + carrierOrgId + @")
    AND RL.used = 1
    AND RL.databegin <= GETDATE()
    AND RL.dataend   >= CAST(GETDATE() AS date)
    AND RL.zdolg IN (7, 0, 2)
ORDER BY RL.idvladelec, RL.zdolg % 7, RL.datadoc DESC";
    }

    // =================================================================
    // ХЕЛПЕРЫ — безопасное чтение из DataRow
    // =================================================================

    private static string Str(DataRow r, string col)
    {
        if (!r.Table.Columns.Contains(col)) return "";
        object v = r[col];
        if (v == null || v == DBNull.Value) return "";
        return v.ToString().Trim();
    }

    private static int ToInt(DataRow r, string col)
    {
        if (!r.Table.Columns.Contains(col)) return 0;
        object v = r[col];
        if (v == null || v == DBNull.Value) return 0;
        int result;
        return int.TryParse(v.ToString(), out result) ? result : 0;
    }

    private static decimal ToDecimal(DataRow r, string col)
    {
        if (!r.Table.Columns.Contains(col)) return 0m;
        object v = r[col];
        if (v == null || v == DBNull.Value) return 0m;
        try { return Convert.ToDecimal(v); } catch { return 0m; }
    }

    private static decimal? ToDecimalNull(DataRow r, string col)
    {
        if (!r.Table.Columns.Contains(col)) return null;
        object v = r[col];
        if (v == null || v == DBNull.Value) return null;
        try { return Convert.ToDecimal(v); } catch { return null; }
    }

    private static DateTime ToDateOrMin(DataRow r, string col)
    {
        DateTime? d = ToDateNull(r, col);
        return d.HasValue ? d.Value : DateTime.MinValue;
    }

    private static DateTime? ToDateNull(DataRow r, string col)
    {
        if (!r.Table.Columns.Contains(col)) return null;
        object v = r[col];
        if (v == null || v == DBNull.Value) return null;
        DateTime d;
        return DateTime.TryParse(v.ToString(), out d) ? d : (DateTime?)null;
    }

    private sealed class StringWriterUtf8 : StringWriter
    {
        public override Encoding Encoding
        {
            get { return Encoding.UTF8; }
        }
    }
}
