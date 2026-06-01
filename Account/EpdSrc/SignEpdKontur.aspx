<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="SignEpdKontur.aspx.cs" Inherits="tis.Account.EpdSrc.SignEpdKontur" EnableEventValidation="False" %>

<%@ Register assembly="AjaxControlToolkit" namespace="AjaxControlToolkit" tagprefix="cc1" %>

<!DOCTYPE html>

<html lang="ru">
<head>
  <meta http-equiv="Content-Type" content="text/html; charset=utf-8"/>
  <title>Подписание документа</title>
    <script src="../../Scripts/cadesplugin_api.js"></script>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; }
        .container { width: 500px; margin: auto; text-align: center; }
        select, button { padding: 10px; margin-top: 10px; width: 100%; }
    </style>
</head>
<body>

 <form id="form1" runat="server">

 <asp:ScriptManager ID="ScriptManager1" runat="server" EnablePageMethods="True"></asp:ScriptManager>

 <asp:HiddenField ID="hidTimelineId" runat="server" />
 <asp:HiddenField ID="hidReturnToken" runat="server" />
 <asp:HiddenField ID="hidSignerInn" runat="server" />
 <asp:HiddenField ID="hidRequireMChD" runat="server" />
 <asp:HiddenField ID="hidStageCode" runat="server" />
   
 <asp:HiddenField runat="server" ID="signature" Value=""></asp:HiddenField>

 <asp:HiddenField runat="server" ID="base64File" Value=""></asp:HiddenField>

 <asp:HiddenField runat="server" ID="HidCertifs" Value=""></asp:HiddenField>
 <asp:HiddenField runat="server" ID="HidSelSert" Value=""></asp:HiddenField>
 <asp:HiddenField runat="server" ID="HidGetSert" Value="0"></asp:HiddenField>

 <asp:HiddenField runat="server" ID="Hiddenidzay" Value="0"></asp:HiddenField>
 <asp:HiddenField runat="server" ID="HiddenIdTmp" Value="0"></asp:HiddenField>
 <asp:HiddenField runat="server" ID="HiddenIdOtpr" Value="0"></asp:HiddenField>
 <asp:HiddenField runat="server" ID="HiddenIdPolu" Value="0"></asp:HiddenField>
 <asp:HiddenField runat="server" ID="HiddenIdEtcp" Value="0"></asp:HiddenField>

 <asp:HiddenField runat="server" ID="HidSthumbprint" Value=""></asp:HiddenField>
 <asp:HiddenField runat="server" ID="HidSsubjectName" Value=""></asp:HiddenField>
 <asp:HiddenField runat="server" ID="HidSissuerName" Value=""></asp:HiddenField>
 <asp:HiddenField runat="server" ID="HidSvalidFromDate" Value=""></asp:HiddenField>
 <asp:HiddenField runat="server" ID="HidSvalidToDate" Value=""></asp:HiddenField>
 <asp:HiddenField runat="server" ID="HidSserialNumber" Value=""></asp:HiddenField>
 <asp:HiddenField runat="server" ID="HidSpublicKey" Value=""></asp:HiddenField>
 <asp:HiddenField runat="server" ID="HidSsignatureAlgorithm" Value=""></asp:HiddenField>
 <asp:HiddenField runat="server" ID="HidSversion" Value=""></asp:HiddenField>
 <asp:HiddenField runat="server" ID="HidSfriendlyName" Value=""></asp:HiddenField>
 <asp:HiddenField runat="server" ID="HidSkeyUsages" Value=""></asp:HiddenField>

 <asp:HiddenField runat="server" ID="HidMchd" Value="0"></asp:HiddenField>
 <asp:HiddenField runat="server" ID="HidExit1" Value=""></asp:HiddenField>
 <asp:HiddenField runat="server" ID="HidBadCert" Value=""></asp:HiddenField>
 <asp:HiddenField runat="server" ID="Hidtypedocv" Value="r"></asp:HiddenField>
 <asp:HiddenField runat="server" ID="HidEtcpStat" Value="-"></asp:HiddenField>
 <asp:HiddenField runat="server" ID="HidInnKontLogin" Value=""></asp:HiddenField>
 <asp:HiddenField runat="server" ID="HidInnLicoLogin" Value=""></asp:HiddenField>

 <asp:HiddenField runat="server" ID="HidStamp" Value=""></asp:HiddenField>

<span id="debug" runat="server" style="color:red"></span><br/>
<asp:DropDownList runat="server" id="certList" AutoPostBack="True" onchange="setSelectedCertificate();"
 ViewStateMode="Enabled" ClientIDMode="Static"></asp:DropDownList>
<br /><br />
 <asp:Button runat="server" Text="Подписать" ID="ButPodpis"
      style="width:120px;"
      OnClientClick="this.style.visibility='hidden';signData();return false;"
      Visible="false"
     ></asp:Button>

<script type="text/javascript">
  var debtxt=document.getElementById("debug");

  function clearSertHidListElements()     //11.12.2024
  {
      document.getElementById("HidSthumbprint").value="";
      document.getElementById("HidSsubjectName").value="";
      document.getElementById("HidSissuerName").value="";
      document.getElementById("HidSvalidFromDate").value="";
      document.getElementById("HidSvalidToDate").value="";
      document.getElementById("HidSserialNumber").value="";
      document.getElementById("HidSpublicKey").value="";
      document.getElementById("HidSsignatureAlgorithm").value="";
      document.getElementById("HidSversion").value="";
      document.getElementById("HidSfriendlyName").value="";
      document.getElementById("HidSkeyUsages").value="";
  }

</script>

<script type="text/javascript">
 var selectedCertificateThumbprint = "";
 var goden="";

 function setSelectedCertificate()
 {
        const certListBox = document.getElementById("certList");  //DDL
        selectedCertificateThumbprint = certListBox.value;
        document.getElementById("HidSelSert").value=selectedCertificateThumbprint;
        document.getElementById("HidGetSert").value="1";
     // alert(selectedCertificateThumbprint);
        try
        {
            const selind=certListBox.selectedIndex;         //09.01.2025
            const celtext=certListBox.options[selind].text;
            document.getElementById("HidBadCert").value="";
            if((celtext+" ").substr(0,1)=='-') document.getElementById("HidBadCert").value="-";
        } catch(e){ alert(e); }

        document.forms["form1"].submit();
    //  return true;
 }

 function loadCertificatesSync()
  {
        if(document.getElementById("HidGetSert").value=="1") return;

        var certListBox = document.getElementById("certList");
        clearSertHidListElements();

        cadesplugin.then
         (
            function ()
            {
                // Плагин успешно загружен
                cadesplugin.async_spawn(function * ()
                {
                  try {
                        var store = yield cadesplugin.CreateObjectAsync("CAdESCOM.Store");
                        yield store.Open(
                            cadesplugin.CAPICOM_CURRENT_USER_STORE, 
                            cadesplugin.CAPICOM_MY_STORE, 
                            cadesplugin.CAPICOM_STORE_OPEN_MAXIMUM_ALLOWED
                        );
                        
                        var certificates = yield store.Certificates;
                        var count = yield certificates.Count;

                        if (count == 0) { alert("Нет доступных сертификатов"); return; }

                        var option0 = document.createElement("option");  //пустой элемент'
                        option0.value="";
                        option0.text = "Не выбран";
                        certListBox.appendChild(option0);

                        // Перебор сертификатов и добавление их в выпадающий список
                        for (var i=1; i<=count; i++)
                        {
                            var cert = yield certificates.Item(i);
                            var thumbprint = yield cert.Thumbprint;
                            var subjectName = yield cert.SubjectName;
                            var validToDate = yield cert.ValidToDate;
                            var option = document.createElement("option");
                           
                            const curDate=((new Date()).format("yyyy-MM-ddTHH:mm:ss")+"            ").substr(0,16); //28.12.2024
                            const dodate=(validToDate+"                    ").substr(0,16);
                            var horos=dodate>curDate; 
                            var txs="--инн";  //28.12.2024
                            if(horos) txs="+ИНН";
                          //alert(dodate+"=="+curDate); //28.12.2024
                            var innfl=otodratAtr(subjectName,"ИНН");
                            var innyl=otodratAtr(subjectName,"ИНН ЮЛ");
                            var yle="";
                            if(horos) { if(innfl!=document.getElementById("HidInnLicoLogin").value) txs="+?инн";
                                        if(innyl!="") { if(innyl!=document.getElementById("HidInnKontLogin").value) txs="+?инн";
                                                        yle="?"
                                                      }
                                      }
                            if(innyl=="") { innyl="(требуется МЧД)";
                                            if(document.getElementById("HidInnLicoLogin").value==document.getElementById("HidInnKontLogin").value)
                                               innyl="(ИП)";    //20.05.2025
                                          }
                            txs+="="+innfl;
                            txs+=", "+yle+"инн юл="+innyl;
                            txs+=", "+otodratAtr(subjectName,"SN")+" "+otodratAtr(subjectName,"G")+" ";
                            txs+=", до "+(horos?"":"--(")+validToDate.substr(0,10)+(horos?"":")");
                            txs+=", Орг="+otodratAtr(subjectName,"O");       //28.12.2024

                            option.value = thumbprint;
                            option.text  = txs; //28.12.2024 validToDate.substr(0,10)+" "+subjectName;
                            certListBox.appendChild(option);
                            document.getElementById("HidCertifs").value+="§"+thumbprint+"®"+option.text;

                            var issuerName=   yield cert.IssuerName;
                            var validFromDate=yield cert.ValidFromDate;
                            var serialNumber =yield cert.SerialNumber;
                            var publicKey   = yield cert.PublicKey;
                            var version     = yield cert.Version;
                            var friendlyName =yield cert.FriendlyName;
                            var keyUsages   = yield cert.KeyUsages;
                            var signatureAlgorithm = yield cert.SignatureAlgorithm;

                            document.getElementById("HidSthumbprint").value    +="§"+ thumbprint; //11.12.2024
                            document.getElementById("HidSsubjectName").value   +="§"+ subjectName;
                            document.getElementById("HidSissuerName").value    +="§"+ issuerName;
                            document.getElementById("HidSvalidFromDate").value +="§"+ validFromDate;
                            document.getElementById("HidSvalidToDate").value   +="§"+ validToDate;
                            document.getElementById("HidSserialNumber").value  +="§"+ serialNumber;
                            document.getElementById("HidSpublicKey").value     +="§"+ publicKey;
                            document.getElementById("HidSversion").value       +="§"+ version;
                            document.getElementById("HidSfriendlyName").value  +="§"+ friendlyName;
                            document.getElementById("HidSkeyUsages").value     +="§"+ keyUsages;
                            document.getElementById("HidSsignatureAlgorithm").value+="§"+ signatureAlgorithm;
                        }

                        yield store.Close();
                    } catch (e) { alert("Ошибка при загрузке сертификатов: " + e.message); }
                });
               },
            function (error) { alert("Ошибка загрузки плагина CryptoPro: " + error); }  // Плагин не загружен
        );
  }

</script>

<script type="text/javascript">

function signData()
{
    const dataToSign = document.getElementById("base64File").value;
    document.getElementById("debug").innerHTML = "Подписание начато... (" + dataToSign.substr(0, 10) + ") ...";
    selectedCertificateThumbprint=document.getElementById("HidSelSert").value;
    
  //  if(!selectedCertificateThumbprint) { alert("Выберите сертификат для подписи!"); return; }

    if(!dataToSign || dataToSign.trim() === "") { alert("Данные для подписи не указаны или пусты!"); return; }
    if(selectedCertificateThumbprint=="")  { alert("Не выбран сертификат!"); return; }             //28.12.224
    if(document.getElementById("HidBadCert").value!="") { alert(document.getElementById("HidBadCert").value); return; } //28.12.2024

    cadesplugin.async_spawn(function*(args) {
        try {
            const oStore = yield cadesplugin.CreateObjectAsync("CAdESCOM.Store");
            yield oStore.Open(
                cadesplugin.CAPICOM_CURRENT_USER_STORE,
                cadesplugin.CAPICOM_MY_STORE,
                cadesplugin.CAPICOM_STORE_OPEN_MAXIMUM_ALLOWED
            );
   
            const oCertificates = yield oStore.Certificates;
            const foundCertificates = yield oCertificates.Find(
                cadesplugin.CAPICOM_CERTIFICATE_FIND_SHA1_HASH,
                selectedCertificateThumbprint
            );
            const certCount = yield foundCertificates.Count;
            if (certCount === 0) {
                alert("Сертификат не найден: " + selectedCertificateThumbprint);
                yield oStore.Close();
                return;
            }

            const oCertificate = yield foundCertificates.Item(1);
            const oSigner = yield cadesplugin.CreateObjectAsync("CAdESCOM.CPSigner");
            yield oSigner.propset_Certificate(oCertificate);
            yield oSigner.propset_CheckCertificate(true);

            const oSignedData = yield cadesplugin.CreateObjectAsync("CAdESCOM.CadesSignedData");
            yield oSignedData.propset_ContentEncoding(cadesplugin.CADESCOM_BASE64_TO_BINARY);
            yield oSignedData.propset_Content(dataToSign);

            let sSignedMessage;
            try
            { sSignedMessage = yield oSignedData.SignCades( oSigner, cadesplugin.CADESCOM_CADES_BES, true );    //detached подпись
            } catch (err) {
                alert("Ошибка при создании подписи: " + cadesplugin.getLastError(err));
                yield oStore.Close();
                return;
            }

            yield oStore.Close();
            alert("Подпись создана: "+(sSignedMessage+'            ').substr(0,10));
            document.getElementById("signature").value = sSignedMessage;
            document.forms["form1"].submit(); // отправка данных на сервер

        }
        catch (e) { alert("Ошибка в процессе подписи: " + e.message); }
    }, { selectedCertificateThumbprint: selectedCertificateThumbprint });
}
</script>

<script type="text/javascript">
 async function listCertificates()
 {
    const oStore = await cadesplugin.CreateObjectAsync("CAdESCOM.Store");
    await oStore.Open(
        cadesplugin.CAPICOM_CURRENT_USER_STORE,
        cadesplugin.CAPICOM_MY_STORE,
        cadesplugin.CAPICOM_STORE_OPEN_MAXIMUM_ALLOWED
    );

    const oCertificates = await oStore.Certificates;
    const certCount = await oCertificates.Count;

    console.log("Found certificates:", certCount);

    // Перебираем все сертификаты и выводим их имена
    for (let i = 1; i <= certCount; i++) {
        const cert = await oCertificates.Item(i);
        const subjectName = await cert.SubjectName;
        const thumbprint = await cert.Thumbprint;
        const validToDate = await cert.ValidToDate;
        var txs="<br>Certificate " + i + ": " + subjectName+"  SHA1["+thumbprint+"]"+",validTo="+validToDate;
        txs+="<br>Отделить ";
        txs+=",инн="+otodratAtr(subjectName,"ИНН");
        txs+=",инн юл="+otodratAtr(subjectName,"ИНН ЮЛ");
        txs+=",Фам="+otodratAtr(subjectName,"SN")+" "+otodratAtr(subjectName,"G")+" ";
        txs+=",Орг="+otodratAtr(subjectName,"O");

        document.getElementById("slist").innerHTML+=txs+"<br>";
    }

    await oStore.Close();
}
</script>
<script type="text/javascript">
  function otodratAtr(subjName,atrname)
   {
      let atrvalue="";
      const parts=subjName.split(", ");
      for (const part of parts)
      {
         if(part.startsWith(atrname+"=")) { atrvalue=part.split("=")[1]; break; } // Прерываем цикл, когда нашли atrname
      }
     return atrvalue;
   }
</script>
<script type="text/javascript">
async function verifySignature(signat)
{
    const dataToSign=document.getElementById("base64File").value;
    debtxt.innerHTML+="File="+dataToSign.substr(0,10)+"... sign="+signat.substr(0,10);
    try {
        const oSignedData = await cadesplugin.CreateObjectAsync("CAdESCOM.CadesSignedData");
        await oSignedData.propset_ContentEncoding(cadesplugin.CADESCOM_BASE64_TO_BINARY);
        await oSignedData.propset_Content(dataToSign);        // Устанавливаем исходный текст, который подписывался
        await oSignedData.VerifyCades(signat, cadesplugin.CADESCOM_CADES_BES,true);  //отсоед.подпис
       alert("Подпись проверена успешно.");
        document.getElementById('but_exit').click();
    } catch (err) { alert("Ошибка проверки подписи: " + cadesplugin.getLastError(err));  }
}
  function signVerify()
    {
      debtxt.innerHTML+="Verifying ...";
      var signa=document.getElementById("signature").value;
    //alert("DocSign:"+signa);  //25.12.2024-закомм
      verifySignature(signa);
     // downloadSignedFile();
    }
</script>

<script type="text/javascript">
   function base64ToBlob(base64, mimeType)
   {
        const binary = atob(base64);
        const array = [];
        for (let i = 0; i < binary.length; i++) {
            array.push(binary.charCodeAt(i));
        }
        return new Blob([new Uint8Array(array)], { type: mimeType });
    }

    function downloadSignedFile()
    {
        const signedFileBase64=document.getElementById("signature").value;
        const blob = base64ToBlob(signedFileBase64, "application/vnd.ms-excel");
        const url = URL.createObjectURL(blob);

        // Создаем ссылку для скачивания
        const a = document.createElement("a");
        a.href = url;
        a.download = "SignedDocument.xls";
        document.body.appendChild(a);
        a.click();

        // Очистка
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    }
</script>
  &emsp;
 <input type="button" value="Выйти" style="width:120px;"     onclick='exiting()'    id="but_exit"  />
 <input type="button" value="Проверить" style="width:160px;" onclick="signVerify()" id="but_check" runat="server" visible="true" />
<script type="text/javascript">
  function exiting()  //12.12.2024 //20.04.2026 iframe(ЭЗЗ) + window.open(ЭТРН)
  {
      var signa  = document.getElementById("signature").value;
      var stamp  = document.getElementById("HidStamp").value;
      var ismchd = document.getElementById("HidMchd").value;
      if (parent !== window && typeof parent.retPodpisanie === 'function') {
          parent.retPodpisanie(signa, stamp, ismchd);   // iframe (ЭЗЗ)
          return;
      }
      if (window.opener && !window.opener.closed && typeof window.opener.retPodpisanie === 'function') {
          window.opener.retPodpisanie(signa, stamp, ismchd);  // window.open (ЭТРН)
          window.close();
          return;
      }
      if (window.opener && !window.opener.closed) {
          // Для страниц без retPodpisanie (например KonturProbe) обновляем родителя,
          // чтобы статус "Подпись готова" отрисовался сразу после сохранения подписи в БД.
          try { window.opener.location.reload(); } catch (e) { }
          window.close();
          return;
      }
      window.close();
  }
</script>
<br />
<br /><span id="sp_infoSigner" runat="server"></span>
<br />
  <span id="sp_adminSpisok" runat="server" visible="false">
      Список: <span id="slist"></span> <input type="button" style="width:100px" value="список" onclick="listCertificates()" />
  </span>
<br />
<br />
     <input type="hidden" id="InpDebug" value='0' runat="server" />

     <asp:Label ID="litError" runat="server" Text=""></asp:Label>

<script type="text/javascript">
  window.onload = function () { loadCertificatesSync(); };     // Загрузка сертификатов при загрузке страницы
 // alert("Page loaded");
  if(document.getElementById("HidExit1").value=="1")   //25.12.2024
    // signVerify();                                  //25.12.2024 или
     exiting();                                      //25.12.2024 или
    //  document.getElementById("but_check").click(); //25.12.2024 или
</script>
</form>
</body>
</html>

