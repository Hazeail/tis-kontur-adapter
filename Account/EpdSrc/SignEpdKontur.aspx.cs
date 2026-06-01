//Зингаров, 02.10.2025
//18.11.2025-штамп  (одно и то же поле, передается родителю для сохранения)
//https://e-trust.gosuslugi.ru/check/sign#/portal/sig-check  ручная проверка подписи
//https://m4d.nalog.gov.ru/emchd/check-status  ручная проверка МЧД
//https://m4d.nalog.gov.ru/emchd/check-status?guid=f5dfc6ba-511a-4184-9032-d62c95b6c9b2  проверка сразу на сайте по guid
//05.12.2025-авто получение нужного титула
//29.01.2026-для мчд возврат признака(если найдетн только в табл.мчд)

using System;
/*
  ФАЙЛ: SignEpdKontur.aspx.cs
  НАЗНАЧЕНИЕ: Отдельная страница подписи XML этапов Контур для KonturProbe.
  Изолирует контурный сценарий подписи от legacy SignEpd и сохраняет detached CMS в epd_doc_store и рабочую папку.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  25.05.2026 - Первичное создание отдельной страницы подписи для сценариев Контур.
  28.05.2026 - Загрузка XML переведена на stage-specific источник, чтобы T1 и T2 подписывали именно свой титул.
  28.05.2026 - Добавлена поддержка выбранного xmlFile из KonturProbe с синхронизацией XML в epd_doc_store перед подписью.
  28.05.2026 - Подпись этапа синхронизируется в TEpdTitleArtifact, чтобы отправка использовала тот же XML-артефакт, который был открыт в окне подписи.
  28.05.2026 - Исправлен приоритет источника XML: при ручном выборе файла в KonturProbe подпись строится строго по выбранному xmlFile.
  28.05.2026 - Добавлено чтение флага Kontur-only тестового режима по TimelineId для поэтапного отделения тестовой подписи Контур.
  28.05.2026 - Исправлен конфликт локальных имен в блоке test-only подписанта, чтобы проект стабильно собирался перед деплоем.
*/
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

using System.Web.UI.HtmlControls;
using System.Web.Services;
using System.IO;
using System.Data;
using System.Data.SqlClient;
using tis.Modules;
using Infragistics.Excel;

using System.Text;
using System.Web.Script.Serialization;
using TIS.EPD;      // EpdRepo
using System.Globalization;
using Tis.KonturIntegration.Integration;
using Tis.KonturIntegration.Models;
using Tis.KonturIntegration.Storage;

namespace tis.Account.EpdSrc
{
    /// <summary>
    /// Страница подписи XML этапов Контур в отдельном окне.
    /// </summary>
    /// <remarks>
    /// Страница используется только из KonturProbe и должна подписывать тот же XML,
    /// который затем участвует в локальной проверке и отправке этапа.
    /// </remarks>
    public partial class SignEpdKontur : System.Web.UI.Page
    {
        // Ограниченный обход проверки МЧД/ИНН ЮЛ для тестовых сертификатов в контуре Контур.
        // Важно: применяется только на отдельной странице SignEpdKontur и не затрагивает legacy SignEpd.
        private static readonly HashSet<string> KonturBypassInnList = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "635552018474", // Соколов Лука Тимофеевич
            "081206022988"  // Захаров Петр Русланович
        };

        protected SqlConnection dbCon = new System.Data.SqlClient.SqlConnection(Connection.conStr());

        /// <summary>
        /// Инициализирует страницу подписи, загружает XML текущего этапа и обрабатывает postback сохранения CMS.
        /// </summary>
        /// <param name="sender">Источник события загрузки страницы.</param>
        /// <param name="e">Аргументы события загрузки.</param>
        /// <remarks>
        /// Для исключения рассинхрона T1/T2 страница использует stage-specific XML:
        /// либо выбранный файл из KonturProbe, либо payload соответствующего титула в epd_doc_store.
        /// </remarks>
        protected void Page_Load(object sender, EventArgs e)
        {
             if(dog.admin()==2) { sp_adminSpisok.Visible=true; but_check.Visible=true; } //25.12.2024
             if(signature.Value!="")   debug.InnerHtml+="S ";
             string dbg1=signature.Value;
             if(!IsPostBack)
             {
                try
                {
                    long timelineId = ParseLong(Request["timelineId"]);
                    string returnToken = (Request["returnToken"] ?? "").Trim();
              
                    string xmlSource;
                    byte[] xmlCp1251 = ResolveXmlBytesForSigning(timelineId, out xmlSource);
                    if (xmlCp1251 == null || xmlCp1251.Length == 0)
                        throw new ApplicationException("В БД нет XML для указанного timelineId.");
                    // Логируем SHA-256 XML перед подписью
                    try
                    {
                        string hashHex = EpdRepo.CalcSha256Hex(xmlCp1251);
                        EpdRepo.AppendNote(timelineId, "SHA256(XML текущего титула перед подписью) = " + hashHex + "; source=" + xmlSource);
                        if (IsKonturOnlyTestModeEnabled(timelineId))
                        {
                            EpdRepo.AppendNote(timelineId, "KonturTestMode=1; окно SignEpdKontur открыто в специальном тестовом режиме.");
                        }
                    }
                    catch { /* логирование хеша – вспомогательно, падать из-за него не нужно */ }
              
                    string xmlB64 = Convert.ToBase64String(xmlCp1251);
                    base64File.Value = xmlB64;
              
                    // прокинем назад параметры в скрытые поля (для JS и последующего POST)
                    hidTimelineId.Value = timelineId.ToString();
                    hidReturnToken.Value = returnToken;
                    hidSignerInn.Value = (Request["signerInn"] ?? "").Trim();
                    hidRequireMChD.Value = (Request["requireMChD"] ?? "0").Trim();
                }
                catch (Exception ex)
                {
                    litError.Text = HttpUtility.HtmlEncode("Ошибка инициализации: " + ex.Message);  // Покажем ошибку как есть
                }
               
             }

             if(!IsPostBack) System.Threading.Thread.Sleep(1000);
         //  if(!IsPostBack) HidMchd.Value="#mchd";                   //29.01.2026
                             
               if(Request.IsLocal)
                   InpDebug.Value=transcommon.givescalar(" kod FROM TUnivKod WHERE type='EtcpDebug'",dbCon,"0");  //чтобы можно было подписать любым сертификатом, без разбора ИНН в отладке

             infoSigner();
             setToSertif();
             SaveSignatureIfPosted();
        }

        /// <summary>
        /// Сохраняет подпись из скрытого поля при postback после client-side подписания.
        /// </summary>
        /// <remarks>
        /// Шаг нужен для сценария KonturProbe: после успешного окна подписи статус этапа
        /// должен опираться на фактически сохраненную подпись в БД, а не только на сообщение в popup.
        /// </remarks>
        private void SaveSignatureIfPosted()
        {
            if (!IsPostBack)
            {
                return;
            }

            var signatureBase64 = (signature.Value ?? string.Empty).Trim();
            if (signatureBase64.Length < 16)
            {
                return;
            }

            long timelineId;
            if (!long.TryParse((hidTimelineId.Value ?? string.Empty).Trim(), out timelineId) || timelineId <= 0)
            {
                return;
            }

            try
            {
                var signatureBytes = Convert.FromBase64String(signatureBase64);
                if (signatureBytes == null || signatureBytes.Length == 0)
                {
                    return;
                }

                long idMchd = 0;
                long.TryParse((HidMchd.Value ?? string.Empty).Trim(), out idMchd);

                var who = ResolveSignatureSlotByStageCode();
                var titleCode = ResolveCurrentTitleCode();
                EpdRepo.SaveSignature(timelineId, signatureBytes, who, HidStamp.Value ?? string.Empty, idMchd);
                SyncSignatureToArtifact(timelineId, titleCode, signatureBytes);
                SaveSignatureToServerWorkspace(timelineId, signatureBytes);
                HidExit1.Value = "1";
            }
            catch (Exception ex)
            {
                litError.Text = HttpUtility.HtmlEncode("Ошибка сохранения подписи: " + ex.Message);
            }
        }

        /// <summary>
        /// Определяет номер слота подписи в epd_doc_store по этапу.
        /// </summary>
        /// <returns>"1" для T1/T3 и "2" для T2/T4.</returns>
        private string ResolveSignatureSlotByStageCode()
        {
            var stageCode = (Request["stageCode"] ?? string.Empty).Trim().ToUpperInvariant();
            if (stageCode == "T2" || stageCode == "T4")
            {
                return "2";
            }

            return "1";
        }

        /// <summary>
        /// Возвращает номер слота подписи для текущего кода этапа.
        /// </summary>
        /// <returns>1 для T1/T3 и 2 для T2/T4.</returns>
        /// <remarks>Числовой слот нужен при выборе XML соответствующего титула из epd_doc_store.</remarks>
        private int ResolveSignatureSlotNumberByStageCode()
        {
            return ResolveSignatureSlotByStageCode() == "2" ? 2 : 1;
        }

        /// <summary>
        /// Разрешает XML для подписи по текущему stageCode и выбранному xmlFile.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="xmlSource">Краткое описание фактического источника XML.</param>
        /// <returns>Байты XML в кодировке CP1251.</returns>
        /// <remarks>
        /// Если оператор выбрал XML-файл на странице KonturProbe, подпись должна строиться именно по нему.
        /// Перед возвратом выбранный XML синхронизируется в epd_doc_store, чтобы БД и рабочий файл не расходились.
        /// </remarks>
        private byte[] ResolveXmlBytesForSigning(long timelineId, out string xmlSource)
        {
            xmlSource = "epd_doc_store";
            var titleCode = ResolveCurrentTitleCode();

            var xmlFile = (Request["xmlFile"] ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(xmlFile))
            {
                var requestedFileName = Path.GetFileName(xmlFile);
                if (IsStageServerFile(requestedFileName, titleCode))
                {
                    var requestedPath = ResolveRequestedServerXmlPath(requestedFileName);
                    if (File.Exists(requestedPath))
                    {
                        var xmlBytes = File.ReadAllBytes(requestedPath);
                        SyncXmlToDocStoreForSigning(timelineId, ResolveSignatureSlotNumberByStageCode(), xmlBytes);
                        xmlSource = "server-file:" + Path.GetFileName(requestedPath);
                        return xmlBytes;
                    }
                }
            }

            var artifact = new KonturTitleArtifactRepository(Connection.conStr()).GetLatest(timelineId, titleCode);
            if (artifact != null && artifact.HasXml)
            {
                xmlSource = "artifact:" + artifact.Id;
                return artifact.TitleXml;
            }

            return EpdRepo.GetXmlPayloadTitul(timelineId, ResolveSignatureSlotNumberByStageCode());
        }

        /// <summary>
        /// Возвращает код текущего титула для окна подписи.
        /// </summary>
        /// <returns>Код T1/T2/T3/T4.</returns>
        /// <remarks>SignEpdKontur открывается только из сценария этапов KonturProbe и работает по stageCode текущего этапа.</remarks>
        private string ResolveCurrentTitleCode()
        {
            var stageCode = (Request["stageCode"] ?? string.Empty).Trim().ToUpperInvariant();
            if (stageCode == "T2")
            {
                return "T2";
            }

            if (stageCode == "T3")
            {
                return "T3";
            }

            if (stageCode == "T4")
            {
                return "T4";
            }

            return "T1";
        }

        /// <summary>
        /// Сохраняет detached-подпись в актуальный SQL-артефакт текущего этапа.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="titleCode">Код титула T1/T2/T3/T4.</param>
        /// <param name="signatureBytes">Байты detached-подписи.</param>
        /// <remarks>
        /// Шаг нужен, чтобы дальнейшая отправка этапа могла брать XML и SGN из одного источника,
        /// а не сочетать TEpdTitleArtifact, epd_doc_store и выбранные вручную файлы.
        /// </remarks>
        private void SyncSignatureToArtifact(long timelineId, string titleCode, byte[] signatureBytes)
        {
            var repository = new KonturTitleArtifactRepository(Connection.conStr());
            repository.SaveSignature(
                timelineId,
                titleCode,
                string.Format("{0}_timeline{1}.sgn", titleCode.ToLowerInvariant(), timelineId),
                signatureBytes,
                string.Empty,
                string.Empty,
                DateTime.Now);
        }

        /// <summary>
        /// Синхронизирует выбранный XML в epd_doc_store перед созданием detached-подписи.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="who">Номер слота титула: 1 для T1, 2 для T2.</param>
        /// <param name="xmlBytes">Байты выбранного XML.</param>
        /// <remarks>
        /// Шаг нужен, чтобы после подписи в popup тот же XML оставался источником истины для локальной проверки
        /// и последующей отправки этапа, а не только временным файлом в рабочей папке.
        /// </remarks>
        private void SyncXmlToDocStoreForSigning(long timelineId, int who, byte[] xmlBytes)
        {
            if (xmlBytes == null || xmlBytes.Length == 0)
            {
                return;
            }

            if (who == 2)
            {
                EpdRepo.SaveTitul2Xml(timelineId, xmlBytes);
                return;
            }

            EpdRepo.UpsertDoc(timelineId, xmlBytes, null, null);
        }

        /// <summary>
        /// Преобразует имя выбранного XML-файла в безопасный абсолютный путь внутри рабочей папки KonturEtrn.
        /// </summary>
        /// <param name="xmlFile">Имя XML-файла, переданное из KonturProbe.</param>
        /// <returns>Абсолютный путь к файлу в рабочей папке.</returns>
        /// <remarks>Метод не допускает выход за пределы рабочей папки через относительные сегменты пути.</remarks>
        private string ResolveRequestedServerXmlPath(string xmlFile)
        {
            var safeFileName = Path.GetFileName((xmlFile ?? string.Empty).Trim());
            return Path.Combine(GetKonturServerFilesDirectory(), safeFileName);
        }

        /// <summary>
        /// Возвращает рабочую папку XML/SGN артефактов Контур.
        /// </summary>
        /// <returns>Абсолютный путь к App_Data\Temp\KonturEtrn.</returns>
        private string GetKonturServerFilesDirectory()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "Temp", "KonturEtrn");
        }

        /// <summary>
        /// Проверяет, включен ли специальный тестовый режим Kontur-only для текущего timeline.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <returns>True, если режим включен; иначе false.</returns>
        /// <remarks>
        /// Метод является мягкой точкой входа для дальнейшего перевода окна подписи
        /// на отдельный тестовый контур Контур без влияния на legacy SignEpd.
        /// </remarks>
        private bool IsKonturOnlyTestModeEnabled(long timelineId)
        {
            if (timelineId <= 0)
            {
                return false;
            }

            return new KonturTestModeService(Connection.conStr()).IsEnabled(timelineId);
        }

        /// <summary>
        /// Проверяет, что файл относится к ожидаемому этапу T1/T2.
        /// </summary>
        /// <param name="fileName">Имя файла без пути.</param>
        /// <param name="titleCode">Код титула текущего этапа.</param>
        /// <returns>True, если файл совпадает с ожидаемым этапом по имени.</returns>
        private bool IsStageServerFile(string fileName, string titleCode)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return false;
            }

            var lower = fileName.ToLowerInvariant();
            switch ((titleCode ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "T1":
                    return lower.StartsWith("t1_") || lower.StartsWith("on_trnaclgrot");
                case "T2":
                    return lower.StartsWith("t2_") || lower.StartsWith("on_trnaclpprin");
                default:
                    return false;
            }
        }

        /// <summary>
        /// Сохраняет detached-подпись в серверную рабочую папку KonturEtrn.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="signatureBytes">Байты detached-подписи.</param>
        /// <remarks>
        /// Файл нужен для операторского списка .sgn на странице KonturProbe.
        /// Источником истины остаётся БД, но рабочая копия улучшает ручной контроль и повторные прогоны.
        /// </remarks>
        private void SaveSignatureToServerWorkspace(long timelineId, byte[] signatureBytes)
        {
            if (signatureBytes == null || signatureBytes.Length == 0)
            {
                return;
            }

            var stageCode = (Request["stageCode"] ?? string.Empty).Trim().ToUpperInvariant();
            var prefix = "t1";
            if (stageCode == "T2")
            {
                prefix = "t2";
            }
            else if (stageCode == "T3")
            {
                prefix = "t3";
            }
            else if (stageCode == "T4")
            {
                prefix = "t4";
            }

            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var workspaceDirectory = Path.Combine(baseDirectory, "App_Data", "Temp", "KonturEtrn");
            Directory.CreateDirectory(workspaceDirectory);

            var fileName = string.Format(
                "{0}_timeline{1}_{2}.sgn",
                prefix,
                timelineId,
                DateTime.Now.ToString("yyyyMMdd_HHmmss"));

            var fullPath = Path.Combine(workspaceDirectory, fileName);
            File.WriteAllBytes(fullPath, signatureBytes);
        }
        public void infoSigner()  //25.11.2024
        {
            var testSigner = TryResolveCurrentTestSignerCandidate();
            if (testSigner != null)
            {
                HidInnLicoLogin.Value = testSigner.SignerInnFl ?? string.Empty;
                HidInnKontLogin.Value = testSigner.RequiredKontragentInn ?? string.Empty;
                string testSignerText = "Подписывает: " + (testSigner.SignerFio ?? string.Empty) + ", ИНН " + HidInnLicoLogin.Value;
                string testOrgText = "Контрагент:&emsp; " + (testSigner.RequiredKontragentName ?? string.Empty) + ", ИНН ЮЛ " + HidInnKontLogin.Value;
                sp_infoSigner.InnerHtml = testOrgText + "<br>" + testSignerText + "<br><span style='color:#9b6a00'>Тестовый режим Kontur-only</span>";
                return;
            }

            string sqlm="SET DATEFORMAT DMY;";
            sqlm+=" SELECT CONCAT(RTRIM(FIZ.fam),'§',RTRIM(FIZ.name),'§',RTRIM(FIZ.otch),'§'"
                + ", RTRIM(FIZ.img_contenttype),'§',RTRIM(Kont.inn),'§',RTRIM(Kont.name) )"
                + " FROM TFizLico AS FIZ "
                + " LEFT JOIN TKontragent AS Kont ON Kont.id=FIZ.idvladelec "
                + " WHERE FIZ.id="+getContextSignerId().ToString();
            dbCon.Open();
            string fizdan=(new SqlCommand(sqlm,dbCon)).ExecuteScalar().ToString();
            dbCon.Close();
            string [] lic=fizdan.Split('§');
            bool mchd=HidMchd.Value=="#mchd";
            string innf=lic[3], innk=lic[4];
            HidInnLicoLogin.Value=innf;  //16.05.2025
            HidInnKontLogin.Value=innk;  //16.05.2025
            if(mchd) innf="<b>"+innf+"</b>"; else innk="<b>"+innk+"</b>";
            string s1="Подписывает: "+lic[0]+" "+lic[1]+" "+lic[2]+", ИНН "+innf;
            string s2="Контрагент:&emsp; "+lic[5]+", ИНН ЮЛ "+innk;
            if(mchd) { sp_infoSigner.InnerHtml=s1+", МЧД<br>"+s2; }
               else  { sp_infoSigner.InnerHtml=s2+"<br>"+s1;  }
        }
        public void setToSertif()
        {
             bool hidbad=(HidBadCert.Value+" ")[0]=='-';
             HidBadCert.Value="";
             debug.InnerHtml="";   //09.01.2025
             int selindx=certList.SelectedIndex;
             string selv=certList.SelectedValue;
             selv=HidSelSert.Value;
             ButPodpis.Visible=true;
             bool goodsert=selv!="";               //09.01.2025
             if(certList.SelectedItem!=null)  //09.01.2025
                if(certList.SelectedItem.Text.PadRight(3).Substring(0,3).IndexOf("-")>=0) goodsert=false;
             if(hidbad) goodsert=false;
             if(!goodsert)
             {  HidBadCert.Value="Недействующий сертификат. Выберите другой сертификат";
                //if(certList.SelectedItem!=null)
                    debug.InnerHtml="<span style='color:black'>"+HidBadCert.Value+"</span>";
                ButPodpis.Visible=false;
             }

             if(selv!="") { if(goodsert) putStampikXls(); setDDLsert(selv); }  //09.01.2025
        }
        public void setDDLsert(string selectedval)
        {
            string[] certsarray=("® -Не выбран-"+HidCertifs.Value).Split('§');
            certList.Items.Clear();   //28.12.2024
            for(int jj=0;jj<certsarray.Length;jj++)
               certList.Items.Add(new ListItem(certsarray[jj].Split('®')[1],certsarray[jj].Split('®')[0]));  //25.12.2024
            certList.SelectedValue=selectedval;
        }
        protected string innYLcert="",innFLcert="";    //24.04.2025
        public void putStampikXls()
        {   debug.InnerHtml="";

            HidStamp.Value="";  //18.11.2025
            string stamtxt=beruStampText();  //22.04.2025 включая заполнение  переменных innYLcert,innFLcert
            bool errors=false;  //16.05.2025
            bool bypassForKonturFl = IsKonturBypassSignerByInn(innFLcert) || IsKonturBypassBySelectedCertificateText();
            string innKlogin=HidInnKontLogin.Value;
            string innLlogin=HidInnLicoLogin.Value;  if(innLlogin=="") innLlogin="(отсутствует)";
            if(!bypassForKonturFl && innYLcert!="")
             if(innYLcert!=innKlogin)     //16.05.2025
                { errors=true; debug.InnerHtml+="<b>Не совпадает ИНН предприятия "+innKlogin+" с сертификатным "+innYLcert+"</b>"; }
            if(!bypassForKonturFl && innFLcert!=innLlogin)      //16.05.2025
                { errors=true; debug.InnerHtml+="<br><b>Не совпадает ИНН физлица "+innLlogin+" с сертификатным "+innFLcert+"</b>"; }

            HidMchd.Value="0";        //29.01.2026  id Mchd
            if(!bypassForKonturFl && innLlogin!=innKlogin)      //16.04.2025 если не ИП
            if(innYLcert=="")                        //  if(HidMchd.Value=="#mchd")
              {
                string rett=beruMchd();
                if(rett!="") stamtxt+=rett;
                 else { errors=true; debug.InnerHtml+="<br><b>МЧД не найдена. Загрузите МЧД в ТИС или выберите другой сертификат.</b>";
                      }
              } //14.01.2025 //22.04.2025 //24.04.2025
             if(InpDebug.Value!="1")  //16.05.2025
              if(errors) { ButPodpis.Visible=false; return; }           //16.05.2025
             HidStamp.Value=stamtxt;  //18.11.2025
          //    wsh.Rows[rowcou+3].Cells[0].Value="https://e-trust.gosuslugi.ru/check/sign#/portal/sig-check"; подсказка url для проверки
         }

        /// <summary>
        /// Определяет, разрешен ли обход проверки МЧД/ИНН ЮЛ для выбранного сертификата.
        /// </summary>
        /// <param name="signerInnFl">ИНН физлица из сертификата.</param>
        /// <returns>Истина, если ИНН включен в ограниченный список обхода.</returns>
        private bool IsKonturBypassSignerByInn(string signerInnFl)
        {
            var normalizedInn = NormalizeInnDigits(signerInnFl);
            if (string.IsNullOrEmpty(normalizedInn))
            {
                return false;
            }

            return KonturBypassInnList.Contains(normalizedInn);
        }

        /// <summary>
        /// Возвращает признак обхода по тексту выбранного сертификата в выпадающем списке.
        /// </summary>
        /// <returns>Истина, если в тексте выбранного сертификата найден ИНН из списка обхода.</returns>
        private bool IsKonturBypassBySelectedCertificateText()
        {
            if (certList == null || certList.SelectedItem == null)
            {
                return false;
            }

            var selectedText = certList.SelectedItem.Text ?? string.Empty;
            foreach (var allowedInn in KonturBypassInnList)
            {
                if (selectedText.IndexOf(allowedInn, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Нормализует ИНН: оставляет только цифры.
        /// </summary>
        /// <param name="value">Сырой текст ИНН из subject сертификата.</param>
        /// <returns>Строка цифр ИНН или пустая строка.</returns>
        private string NormalizeInnDigits(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var source = value.Trim();
            var builder = new StringBuilder(source.Length);
            for (int i = 0; i < source.Length; i++)
            {
                var ch = source[i];
                if (ch >= '0' && ch <= '9')
                {
                    builder.Append(ch);
                }
            }

            return builder.ToString();
        }

       public string beruStampText()  //22.04.2024  для нового проекта только малая часть нужно. Но оставим для отладки или показа полностью
        {   string stamtxt="";
            string[] certsarray=HidCertifs.Value.Split('§');
            int ii=0;
            string selv=HidSelSert.Value;
            for(;ii<certsarray.Length;ii++) if(certsarray[ii].IndexOf(selv)>=0) break;
            string selsertdata=certsarray[ii];

          //stamtxt+=+HidSelSert.Value+"\n"+selsertdata;
            stamtxt+="Электронно-цифровая подпись";     //27.05.2025 было Отправитель
            stamtxt+="\nДата подписи (ЭЦП) "+DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");  //11.12.2024
            string subj=HidSsubjectName.Value.Split('§')[ii];
            stamtxt+="\nФИО: "+getFromSubject(subj,"SN")+" "+getFromSubject(subj,"G");
            innFLcert=getFromSubject(subj,"ИНН");
            stamtxt+="\nИНН: "+innFLcert;     //24.04.2025
            innYLcert=getFromSubject(subj,"ИНН ЮЛ");
            stamtxt+=" ИНН ЮЛ="+innYLcert;    //24.04.2025
            stamtxt+="\nДолжность: "+getFromSubject(subj,"T");
            string organi=getFromSubject(subj,"O");  //16.05.2025
            { if(organi.StartsWith("\"")) organi = organi.Substring(1);
              if(organi.EndsWith("\"")  ) organi = organi.Substring(0,organi.Length-1);
              organi=organi.Replace("\"\"", "'");
            }
            stamtxt+="\nОрганизация: "+organi;
            string nacha=HidSvalidFromDate.Value.Split('§')[ii];
            string konec=HidSvalidToDate.Value.Split('§')[ii];
            DateTime dtmpn;
            DateTime dtmpk;
            bool okNacha = DateTime.TryParse(nacha, new CultureInfo("ru-RU"), DateTimeStyles.AllowWhiteSpaces, out dtmpn)
                        || DateTime.TryParse(nacha, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out dtmpn);
            bool okKonec = DateTime.TryParse(konec, new CultureInfo("ru-RU"), DateTimeStyles.AllowWhiteSpaces, out dtmpk)
                        || DateTime.TryParse(konec, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out dtmpk);
            if(okNacha && okKonec)
                stamtxt+="\nСертификат действителен с "+dtmpn.ToString("dd.MM.yyyy HH:mm")+" по "+dtmpk.ToString("dd.MM.yyyy HH:mm");
            else
                // Формат даты в некоторых контейнерах CryptoPro может отличаться от локали процесса.
                // В этом случае не блокируем подписание и выводим исходные значения из сертификата.
                stamtxt+="\nСертификат действителен с "+nacha+" по "+konec;
            stamtxt+="\nСерийный № ЭП: "+HidSserialNumber.Value.Split('§')[ii];
            stamtxt+="\nОтпечаток: "+HidSthumbprint.Value.Split('§')[ii];
            string utcsert=HidSissuerName.Value.Split('§')[ii];
            stamtxt+="\nУЦ выдавший сертификат: "+getFromSubject(utcsert,"CN");
         // stamtxt+="\nВерсия:"+HidSversion.Value.Split('§')[ii];
         // stamtxt+="\n Публик ключ:"+HidSpublicKey.Value.Split('§')[ii];
         // stamtxt+="\n Алгоритм:"+HidSsignatureAlgorithm.Value.Split('§')[ii];
         // stamtxt+="\n Человекочитаемое имя сертификата:"+HidSfriendlyName.Value.Split('§')[ii];
         // stamtxt+="\n СписокПрименения(8 перечислений(не сделано)):"+HidSkeyUsages.Value;
         // stamtxt+="\n\n Субъект для отладки:"+HidSsubjectName.Value.Split('§')[ii];
            return stamtxt;
        }
        public string beruMchd()  //14.01.2024 //22.04.2025
        {  string rett="";
           if(innYLcert!="") return rett; //24.04.2025 не мчд
           string sqlm="SET DATEFORMAT DMY;";
           sqlm+="SELECT TOP 1 * FROM TMchdK WHERE TMchdK.del=0 AND"
               + " TMchdK.innFl='"+innFLcert+"' AND TMchdK.innYL='"+getContextSignerInnUl()+"'"  //25.05.2026
               + " AND validDateTo>=CAST(getDate() AS DATE) AND (dateOtzyv IS NULL OR dateOtzyv>CAST(getDate() AS DATE))"
               + " ORDER BY ID DESC";
           SqlDataAdapter sda=new SqlDataAdapter(sqlm,dbCon);
           DataSet dsm=new DataSet();
           sda.Fill(dsm,"mchdk");
           DataTable dtm=dsm.Tables["mchdk"];
           if(dtm.Rows.Count==0) return rett;  //24.04.2025
           DataRow drm=dtm.Rows[0];
           rett+="\n                       МЧД:";
           rett+="\nДоверитель:       "+drm["Organiz"].ToString()+", ИНН ЮЛ "+drm["innYL"];
           rett+="\nДоверенное лицо:  "+drm["FIO"].ToString()+", ИНН "+drm["innFL"];
           rett+="\nНомер:            "+drm["NumDoverUUID"].ToString();
           rett+="\nПериод действия:  "+((DateTime)drm["dovDate"]).ToString("dd.MM.yyyy")+" по "
                                       +((DateTime)drm["validDateTo"]).ToString("dd.MM.yyyy");
           HidMchd.Value=drm["id"].ToString(); //24.04.2025 //29.01.2026
           return rett;
        }
        public string getFromSubject(string subj,string elem)  //11.12.2024
        {  string otvet="";
           string[] spart=subj.Split(new[] {", "},StringSplitOptions.None);
           for(int jj=0;jj<spart.Length;jj++)
               if(spart[jj].IndexOf(elem+"=")==0)
                   { otvet=spart[jj].Split('=')[1]; break; }
           return otvet;
        }
        /// <summary>
        /// Возвращает подписанта для окна SignEpdKontur.
        /// </summary>
        /// <returns>idPodpisant из query-параметра или id из текущей сессии пользователя.</returns>
        /// <remarks>
        /// Метод нужен, чтобы окно подписи работало в контексте выбранного подписанта этапа,
        /// не затрагивая legacy-страницу SignEpd.
        /// </remarks>
        private long getContextSignerId()
        {
            long signerId;
            if(long.TryParse((Request["idPodpisant"]??"").Trim(),out signerId))
                if(signerId!=0) return signerId;
            if(Session["UserId"]!=null)
                if(long.TryParse(Session["UserId"].ToString(),out signerId))
                    if(signerId>0) return signerId;
            return 0;
        }

        /// <summary>
        /// Возвращает ИНН ЮЛ организации выбранного подписанта этапа.
        /// </summary>
        /// <returns>ИНН организации подписанта или Session["my_INN"] как fallback.</returns>
        private string getContextSignerInnUl()
        {
            long signerId=getContextSignerId();
            if(signerId<0)
            {
                var testSigner = TryResolveCurrentTestSignerCandidate();
                if (testSigner != null)
                {
                    return (testSigner.RequiredKontragentInn ?? string.Empty).Trim();
                }
            }

            if(signerId>0)
            {
                string sqlm="SET DATEFORMAT DMY;";
                sqlm+=" SELECT RTRIM(ISNULL(Kont.inn,''))"
                    + " FROM TFizLico AS FIZ "
                    + " LEFT JOIN TKontragent AS Kont ON Kont.id=FIZ.idvladelec "
                    + " WHERE FIZ.id="+signerId.ToString();
                dbCon.Open();
                object innObj=(new SqlCommand(sqlm,dbCon)).ExecuteScalar();
                dbCon.Close();
                if(innObj!=null) return innObj.ToString().Trim();
            }
            return Session["my_INN"]==null ? "" : Session["my_INN"].ToString();
        }

        /// <summary>
        /// Пытается вернуть тестового подписанта Kontur-only для текущего окна подписи.
        /// </summary>
        /// <returns>Тестовый подписант или null, если окно открыто в обычном режиме.</returns>
        /// <remarks>
        /// Метод нужен, чтобы окно подписи не требовало существование TFizLico для тестового сертификата
        /// и могло показывать корректный субъект подписи в отдельном тестовом сценарии Контур.
        /// </remarks>
        private KonturStageSignerCandidate TryResolveCurrentTestSignerCandidate()
        {
            long timelineId;
            if (!long.TryParse((Request["timelineId"] ?? string.Empty).Trim(), out timelineId) || timelineId <= 0)
            {
                return null;
            }

            if (!IsKonturOnlyTestModeEnabled(timelineId))
            {
                return null;
            }

            long signerId;
            if (!long.TryParse((Request["idPodpisant"] ?? string.Empty).Trim(), out signerId))
            {
                signerId = 0;
            }

            var stageCode = (Request["stageCode"] ?? string.Empty).Trim();
            var service = new KonturTestSigningContextService(Connection.conStr());
            if (signerId < 0)
            {
                return service.TryResolveSignerById(timelineId, stageCode, signerId);
            }

            return service.TryResolveSelectedSigner(timelineId, stageCode);
        }
     private static long ParseLong(string s)
       {
           long v;
           if (!long.TryParse((s ?? "").Trim(), out v) || v <= 0)
               throw new ApplicationException("Некорректный идентификатор.");
           return v;
       }
//------------------------
    }
}

