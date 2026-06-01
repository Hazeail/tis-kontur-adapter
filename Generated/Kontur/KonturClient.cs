/*
  ФАЙЛ: KonturClient.cs
  НАЗНАЧЕНИЕ: Клиент транспортного слоя для вызовов Kontur Logistics API.
  Используется адаптером, чтобы изолировать HTTP-взаимодействие от бизнес-логики ТИС.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  05.05.2026 - Первичное создание каркаса клиента для ветки Контур + Danaflex.
  07.05.2026 - Реализованы сценарии отправки T1 (initial и draft).
  08.05.2026 - Добавлен сценарий отправки T2 через endpoint /v1/transportations/documents.
  13.05.2026 - Добавлена отправка T3 по байтам XML и подписи для запуска без ручных файлов.
  13.05.2026 - Добавлена отправка T4 по байтам XML и подписи для полного цикла T1-T4.
  14.05.2026 - Переключена авторизация вызовов Logistics API на OIDC Bearer token.
  18.05.2026 - Добавлены retry/backoff для 429/5xx и сетевых сбоев.
  19.05.2026 - Добавлена совместимость авторизации: OIDC Bearer и x-kontur-apikey.
  19.05.2026 - Добавлены явные префиксы bearer:/apiKey: для детерминированного выбора схемы авторизации.
  26.05.2026 - Добавлена передача X-Solution-Info и выделены общие заголовки API-вызова.
*/

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using Tis.KonturIntegration.Models;

namespace Tis.KonturIntegration.KonturClient
{
    /// <summary>
    /// Клиент вызовов Kontur Logistics API.
    /// </summary>
    public class KonturClient
    {
        /// <summary>
        /// Создает экземпляр клиента с базовыми настройками оператора.
        /// </summary>
        /// <param name="apiUrl">Базовый URL Kontur Logistics API.</param>
        /// <param name="accessToken">OIDC access token для авторизации вызовов.</param>
        /// <remarks>Метод только сохраняет конфигурацию; сетевые вызовы выполняются отдельными методами.</remarks>
        public KonturClient(string apiUrl, string accessToken)
        {
            ApiUrl = apiUrl;
            AccessToken = accessToken;
        }

        /// <summary>
        /// Создает экземпляр клиента с дополнительной меткой решения для заголовка X-Solution-Info.
        /// </summary>
        /// <param name="apiUrl">Базовый URL Kontur Logistics API.</param>
        /// <param name="accessToken">OIDC access token или API key для авторизации вызовов.</param>
        /// <param name="solutionInfo">Значение заголовка X-Solution-Info.</param>
        /// <remarks>Перегрузка нужна для единообразной передачи идентификатора решения во все вызовы API.</remarks>
        public KonturClient(string apiUrl, string accessToken, string solutionInfo)
            : this(apiUrl, accessToken)
        {
            SolutionInfo = solutionInfo;
        }

        /// <summary>
        /// Получает базовый URL оператора.
        /// </summary>
        public string ApiUrl { get; private set; }

        /// <summary>
        /// Получает OIDC access token оператора.
        /// </summary>
        public string AccessToken { get; private set; }

        /// <summary>
        /// Получает значение заголовка X-Solution-Info для идентификации клиентского решения.
        /// </summary>
        public string SolutionInfo { get; private set; }

        /// <summary>
        /// Выполняет проверку обязательных настроек клиента.
        /// </summary>
        /// <returns>Истина, если API URL и access token заполнены.</returns>
        /// <remarks>Проверка нужна, чтобы не запускать сетевые операции с пустой конфигурацией.</remarks>
        public bool IsConfigured()
        {
            return !string.IsNullOrEmpty(ApiUrl) && !string.IsNullOrEmpty(AccessToken);
        }

        /// <summary>
        /// Выполняет первичную отправку T1 в Контур.
        /// </summary>
        /// <param name="xmlPath">Путь к XML-файлу T1.</param>
        /// <param name="diadocBoxId">Идентификатор ящика отправителя в Диадоке.</param>
        /// <returns>Нормализованный результат отправки титула.</returns>
        /// <remarks>Метод используется для старта нового документооборота в Контуре.</remarks>
        public KonturSendTitleResult SendT1Initial(string xmlPath, string diadocBoxId)
        {
            return SendTitleMultipart(xmlPath, string.Empty, false, BuildInitialEndpoint(diadocBoxId), "formFiles", ParseInitialResponse);
        }

        /// <summary>
        /// Выполняет отправку T1 через draft-ветку.
        /// </summary>
        /// <param name="xmlPath">Путь к XML-файлу T1.</param>
        /// <param name="diadocBoxId">Идентификатор ящика инициатора в Диадоке.</param>
        /// <returns>Нормализованный результат отправки титула.</returns>
        /// <remarks>Метод применяется только для допустимых статусов перевозки на стороне Контур.</remarks>
        public KonturSendTitleResult SendT1Draft(string xmlPath, string diadocBoxId)
        {
            return SendTitleMultipart(xmlPath, string.Empty, false, BuildDraftEndpoint(diadocBoxId), "draft", ParseDraftResponse);
        }

        /// <summary>
        /// Выполняет отправку T2 как ответного титула.
        /// </summary>
        /// <param name="xmlPath">Путь к XML-файлу T2.</param>
        /// <param name="diadocBoxId">Идентификатор ящика отправителя в Диадоке.</param>
        /// <returns>Нормализованный результат отправки титула.</returns>
        /// <remarks>
        /// На текущем этапе используется endpoint /v1/transportations/documents;
        /// тип документа определяется содержимым XML.
        /// </remarks>
        public KonturSendTitleResult SendT2Response(string xmlPath, string signaturePath, string diadocBoxId, string transportationId)
        {
            if (string.IsNullOrEmpty(xmlPath) || !File.Exists(xmlPath))
            {
                return new KonturSendTitleResult
                {
                    IsSuccess = false,
                    HttpStatus = 0,
                    ErrorMessage = "XmlFileNotFound: " + xmlPath,
                    SanitizedResponsePayload = string.Empty
                };
            }

            if (string.IsNullOrEmpty(signaturePath) || !File.Exists(signaturePath))
            {
                return new KonturSendTitleResult
                {
                    IsSuccess = false,
                    HttpStatus = 0,
                    ErrorMessage = "SignatureFileNotFound: " + signaturePath,
                    SanitizedResponsePayload = string.Empty
                };
            }

            var xmlBytes = File.ReadAllBytes(xmlPath);
            var signatureBytes = File.ReadAllBytes(signaturePath);
            return SendT2ResponseBytes(xmlBytes, Path.GetFileName(xmlPath), signatureBytes, Path.GetFileName(signaturePath), diadocBoxId, transportationId);
        }

        /// <summary>
        /// Выполняет отправку T2 как ответного титула по байтам XML и подписи.
        /// </summary>
        /// <param name="xmlBytes">Содержимое XML-файла T2.</param>
        /// <param name="xmlFileName">Имя XML-файла для multipart-части.</param>
        /// <param name="signatureBytes">Содержимое detached-подписи T2.</param>
        /// <param name="signatureFileName">Имя файла подписи для multipart-части.</param>
        /// <param name="diadocBoxId">Идентификатор ящика отправителя в Диадоке.</param>
        /// <returns>Нормализованный результат отправки титула.</returns>
        /// <remarks>Метод используется серверным контуром ТИС, когда подпись получена из EpdRepo, а не из файлового пути.</remarks>
        public KonturSendTitleResult SendT2ResponseBytes(byte[] xmlBytes, string xmlFileName, byte[] signatureBytes, string signatureFileName, string diadocBoxId, string transportationId)
        {
            if (!IsConfigured())
            {
                return new KonturSendTitleResult
                {
                    IsSuccess = false,
                    HttpStatus = 0,
                    ErrorMessage = "ClientNotConfigured",
                    SanitizedResponsePayload = string.Empty
                };
            }

            if (xmlBytes == null || xmlBytes.Length == 0)
            {
                return new KonturSendTitleResult
                {
                    IsSuccess = false,
                    HttpStatus = 0,
                    ErrorMessage = "XmlBytesEmpty",
                    SanitizedResponsePayload = string.Empty
                };
            }

            if (signatureBytes == null || signatureBytes.Length == 0)
            {
                return new KonturSendTitleResult
                {
                    IsSuccess = false,
                    HttpStatus = 0,
                    ErrorMessage = "SignatureBytesEmpty",
                    SanitizedResponsePayload = string.Empty
                };
            }

            var endpoint = BuildInitialEndpoint(diadocBoxId, transportationId);
            var boundary = "---------------------------" + DateTime.UtcNow.Ticks.ToString("x");
            var requestBody = BuildMultipartBodyBytes(
                xmlBytes,
                string.IsNullOrEmpty(xmlFileName) ? "t2.xml" : xmlFileName,
                signatureBytes,
                string.IsNullOrEmpty(signatureFileName) ? "t2.sig" : signatureFileName,
                boundary,
                "formFiles");

            return SendPreparedMultipart(endpoint, boundary, requestBody, ParseInitialResponse);
        }

        /// <summary>
        /// Выполняет отправку T3 как ответного титула по байтам XML и подписи.
        /// </summary>
        /// <param name="xmlBytes">Содержимое XML-файла T3.</param>
        /// <param name="xmlFileName">Имя XML-файла для multipart-части.</param>
        /// <param name="signatureBytes">Содержимое detached-подписи T3.</param>
        /// <param name="signatureFileName">Имя файла подписи для multipart-части.</param>
        /// <param name="diadocBoxId">Идентификатор ящика отправителя в Диадоке.</param>
        /// <returns>Нормализованный результат отправки титула.</returns>
        /// <remarks>Метод использует тот же endpoint, что и T2; тип титула определяется содержимым XML.</remarks>
        public KonturSendTitleResult SendT3ResponseBytes(byte[] xmlBytes, string xmlFileName, byte[] signatureBytes, string signatureFileName, string diadocBoxId)
        {
            if (!IsConfigured())
            {
                return new KonturSendTitleResult
                {
                    IsSuccess = false,
                    HttpStatus = 0,
                    ErrorMessage = "ClientNotConfigured",
                    SanitizedResponsePayload = string.Empty
                };
            }

            if (xmlBytes == null || xmlBytes.Length == 0)
            {
                return new KonturSendTitleResult
                {
                    IsSuccess = false,
                    HttpStatus = 0,
                    ErrorMessage = "XmlBytesEmpty",
                    SanitizedResponsePayload = string.Empty
                };
            }

            if (signatureBytes == null || signatureBytes.Length == 0)
            {
                return new KonturSendTitleResult
                {
                    IsSuccess = false,
                    HttpStatus = 0,
                    ErrorMessage = "SignatureBytesEmpty",
                    SanitizedResponsePayload = string.Empty
                };
            }

            var endpoint = BuildInitialEndpoint(diadocBoxId);
            var boundary = "---------------------------" + DateTime.UtcNow.Ticks.ToString("x");
            var requestBody = BuildMultipartBodyBytes(
                xmlBytes,
                string.IsNullOrEmpty(xmlFileName) ? "t3.xml" : xmlFileName,
                signatureBytes,
                string.IsNullOrEmpty(signatureFileName) ? "t3.sig" : signatureFileName,
                boundary,
                "formFiles");

            return SendPreparedMultipart(endpoint, boundary, requestBody, ParseInitialResponse);
        }

        /// <summary>
        /// Выполняет отправку T4 как ответного титула по байтам XML и подписи.
        /// </summary>
        /// <param name="xmlBytes">Содержимое XML-файла T4.</param>
        /// <param name="xmlFileName">Имя XML-файла для multipart-части.</param>
        /// <param name="signatureBytes">Содержимое detached-подписи T4.</param>
        /// <param name="signatureFileName">Имя файла подписи для multipart-части.</param>
        /// <param name="diadocBoxId">Идентификатор ящика отправителя в Диадоке.</param>
        /// <returns>Нормализованный результат отправки титула.</returns>
        /// <remarks>Метод использует тот же endpoint, что и T2/T3; тип титула определяется содержимым XML.</remarks>
        public KonturSendTitleResult SendT4ResponseBytes(byte[] xmlBytes, string xmlFileName, byte[] signatureBytes, string signatureFileName, string diadocBoxId)
        {
            if (!IsConfigured())
            {
                return new KonturSendTitleResult
                {
                    IsSuccess = false,
                    HttpStatus = 0,
                    ErrorMessage = "ClientNotConfigured",
                    SanitizedResponsePayload = string.Empty
                };
            }

            if (xmlBytes == null || xmlBytes.Length == 0)
            {
                return new KonturSendTitleResult
                {
                    IsSuccess = false,
                    HttpStatus = 0,
                    ErrorMessage = "XmlBytesEmpty",
                    SanitizedResponsePayload = string.Empty
                };
            }

            if (signatureBytes == null || signatureBytes.Length == 0)
            {
                return new KonturSendTitleResult
                {
                    IsSuccess = false,
                    HttpStatus = 0,
                    ErrorMessage = "SignatureBytesEmpty",
                    SanitizedResponsePayload = string.Empty
                };
            }

            var endpoint = BuildInitialEndpoint(diadocBoxId);
            var boundary = "---------------------------" + DateTime.UtcNow.Ticks.ToString("x");
            var requestBody = BuildMultipartBodyBytes(
                xmlBytes,
                string.IsNullOrEmpty(xmlFileName) ? "t4.xml" : xmlFileName,
                signatureBytes,
                string.IsNullOrEmpty(signatureFileName) ? "t4.sig" : signatureFileName,
                boundary,
                "formFiles");

            return SendPreparedMultipart(endpoint, boundary, requestBody, ParseInitialResponse);
        }

        /// <summary>
        /// Выполняет универсальную отправку XML-файла титула в формате multipart/form-data.
        /// </summary>
        /// <param name="xmlPath">Путь к XML-файлу титула.</param>
        /// <param name="endpoint">Полный URL endpoint для отправки.</param>
        /// <param name="formFieldName">Имя multipart-поля, требуемое endpoint.</param>
        /// <param name="responseParser">Функция разбора JSON-ответа.</param>
        /// <returns>Нормализованный результат вызова API.</returns>
        /// <remarks>Метод унифицирует транспортный код и исключает дублирование между T1/T2.</remarks>
        private KonturSendTitleResult SendTitleMultipart(
            string xmlPath,
            string signaturePath,
            bool signatureRequired,
            string endpoint,
            string formFieldName,
            Func<int, string, KonturSendTitleResult> responseParser)
        {
            if (!IsConfigured())
            {
                return new KonturSendTitleResult
                {
                    IsSuccess = false,
                    HttpStatus = 0,
                    ErrorMessage = "ClientNotConfigured",
                    SanitizedResponsePayload = string.Empty
                };
            }

            if (string.IsNullOrEmpty(xmlPath) || !File.Exists(xmlPath))
            {
                return new KonturSendTitleResult
                {
                    IsSuccess = false,
                    HttpStatus = 0,
                    ErrorMessage = "XmlFileNotFound: " + xmlPath,
                    SanitizedResponsePayload = string.Empty
                };
            }

            if (signatureRequired && (string.IsNullOrEmpty(signaturePath) || !File.Exists(signaturePath)))
            {
                return new KonturSendTitleResult
                {
                    IsSuccess = false,
                    HttpStatus = 0,
                    ErrorMessage = "SignatureFileNotFound: " + signaturePath,
                    SanitizedResponsePayload = string.Empty
                };
            }

            var boundary = "---------------------------" + DateTime.UtcNow.Ticks.ToString("x");
            var requestBody = BuildMultipartBody(xmlPath, signaturePath, signatureRequired, boundary, formFieldName);

            return SendPreparedMultipart(endpoint, boundary, requestBody, responseParser);
        }

        /// <summary>
        /// Формирует endpoint первичной отправки титула.
        /// </summary>
        /// <param name="diadocBoxId">Идентификатор ящика отправителя в Диадоке.</param>
        /// <returns>Полный URL endpoint для первичной отправки.</returns>
        /// <remarks>Используется для T1 initial и T2 response на текущем этапе.</remarks>
        private string BuildInitialEndpoint(string diadocBoxId, string transportationId = "")
        {
            var baseUrl = ApiUrl.EndsWith("/") ? ApiUrl.Substring(0, ApiUrl.Length - 1) : ApiUrl;
            var url = baseUrl + "/v1/transportations/documents";
            var hasQuery = false;
            if (!string.IsNullOrEmpty(diadocBoxId))
            {
                url += "?diadocBoxId=" + Uri.EscapeDataString(diadocBoxId);
                hasQuery = true;
            }

            if (!string.IsNullOrEmpty(transportationId))
            {
                url += hasQuery ? "&" : "?";
                url += "transportationId=" + Uri.EscapeDataString(transportationId);
            }

            return url;
        }

        /// <summary>
        /// Формирует endpoint отправки черновика.
        /// </summary>
        /// <param name="diadocBoxId">Идентификатор ящика инициатора в Диадоке.</param>
        /// <returns>Полный URL endpoint для отправки draft.</returns>
        /// <remarks>Используется для сценария T1 draft.</remarks>
        private string BuildDraftEndpoint(string diadocBoxId)
        {
            var baseUrl = ApiUrl.EndsWith("/") ? ApiUrl.Substring(0, ApiUrl.Length - 1) : ApiUrl;
            var url = baseUrl + "/v1/transportations/documents/draft?draftAction=SavedDraft";
            if (!string.IsNullOrEmpty(diadocBoxId))
            {
                url += "&initiatorBoxId=" + Uri.EscapeDataString(diadocBoxId);
            }

            return url;
        }

        /// <summary>
        /// Формирует multipart/form-data тело для отправки XML-файла.
        /// </summary>
        /// <param name="xmlPath">Путь к XML-файлу.</param>
        /// <param name="boundary">Граница multipart-пакета.</param>
        /// <param name="formFieldName">Имя multipart-поля.</param>
        /// <returns>Сериализованное тело HTTP-запроса.</returns>
        /// <remarks>Единая функция сборки тела для всех титулов.</remarks>
        private byte[] BuildMultipartBody(string xmlPath, string boundary, string formFieldName)
        {
            return BuildMultipartBody(xmlPath, string.Empty, false, boundary, formFieldName);
        }

        /// <summary>
        /// Формирует multipart/form-data тело для отправки XML и подписи.
        /// </summary>
        /// <param name="xmlPath">Путь к XML-файлу.</param>
        /// <param name="signaturePath">Путь к файлу подписи.</param>
        /// <param name="signatureRequired">Признак обязательности подписи.</param>
        /// <param name="boundary">Граница multipart-пакета.</param>
        /// <param name="formFieldName">Имя multipart-поля.</param>
        /// <returns>Сериализованное тело HTTP-запроса.</returns>
        /// <remarks>
        /// Для T2 подпись отправляется отдельной частью multipart.
        /// Имя multipart-поля для совместимости оставляется таким же, как и для XML.
        /// </remarks>
        private byte[] BuildMultipartBody(string xmlPath, string signaturePath, bool signatureRequired, string boundary, string formFieldName)
        {
            var bodyStream = new MemoryStream();

            WriteMultipartFilePart(bodyStream, boundary, formFieldName, xmlPath, "application/xml");

            if (signatureRequired)
            {
                WriteMultipartFilePart(bodyStream, boundary, formFieldName, signaturePath, "application/octet-stream");
            }

            var footerBytes = Encoding.UTF8.GetBytes("--" + boundary + "--\r\n");
            bodyStream.Write(footerBytes, 0, footerBytes.Length);
            return bodyStream.ToArray();
        }

        /// <summary>
        /// Записывает одну файловую часть в multipart-поток.
        /// </summary>
        /// <param name="stream">Поток тела multipart-запроса.</param>
        /// <param name="boundary">Граница multipart-пакета.</param>
        /// <param name="formFieldName">Имя multipart-поля.</param>
        /// <param name="filePath">Путь к файлу.</param>
        /// <param name="contentType">MIME-тип файла.</param>
        /// <remarks>Метод выделен отдельно, чтобы исключить дублирование и упростить развитие сценариев T3/T4.</remarks>
        private void WriteMultipartFilePart(Stream stream, string boundary, string formFieldName, string filePath, string contentType)
        {
            var headerBuilder = new StringBuilder();
            headerBuilder.Append("--").Append(boundary).Append("\r\n");
            headerBuilder.Append("Content-Disposition: form-data; name=\"")
                .Append(formFieldName)
                .Append("\"; filename=\"")
                .Append(Path.GetFileName(filePath))
                .Append("\"\r\n");
            headerBuilder.Append("Content-Type: ").Append(contentType).Append("\r\n\r\n");

            var headerBytes = Encoding.UTF8.GetBytes(headerBuilder.ToString());
            stream.Write(headerBytes, 0, headerBytes.Length);

            var fileBytes = File.ReadAllBytes(filePath);
            stream.Write(fileBytes, 0, fileBytes.Length);

            var lineBreakBytes = Encoding.UTF8.GetBytes("\r\n");
            stream.Write(lineBreakBytes, 0, lineBreakBytes.Length);
        }

        /// <summary>
        /// Формирует multipart/form-data тело из байтов XML и подписи.
        /// </summary>
        /// <param name="xmlBytes">Байты XML-файла.</param>
        /// <param name="xmlFileName">Имя XML-файла.</param>
        /// <param name="signatureBytes">Байты detached-подписи.</param>
        /// <param name="signatureFileName">Имя файла подписи.</param>
        /// <param name="boundary">Граница multipart-пакета.</param>
        /// <param name="formFieldName">Имя multipart-поля.</param>
        /// <returns>Сериализованное тело HTTP-запроса.</returns>
        /// <remarks>Метод нужен для серверной отправки T2, когда подпись извлекается из хранилища ТИС.</remarks>
        private byte[] BuildMultipartBodyBytes(byte[] xmlBytes, string xmlFileName, byte[] signatureBytes, string signatureFileName, string boundary, string formFieldName)
        {
            var bodyStream = new MemoryStream();
            WriteMultipartBytesPart(bodyStream, boundary, formFieldName, xmlFileName, xmlBytes, "application/xml");
            WriteMultipartBytesPart(bodyStream, boundary, formFieldName, signatureFileName, signatureBytes, "application/octet-stream");
            var footerBytes = Encoding.UTF8.GetBytes("--" + boundary + "--\r\n");
            bodyStream.Write(footerBytes, 0, footerBytes.Length);
            return bodyStream.ToArray();
        }

        /// <summary>
        /// Записывает файловую часть в multipart-поток из массива байтов.
        /// </summary>
        /// <param name="stream">Поток multipart-запроса.</param>
        /// <param name="boundary">Граница multipart-пакета.</param>
        /// <param name="formFieldName">Имя multipart-поля.</param>
        /// <param name="fileName">Имя файла в multipart-части.</param>
        /// <param name="bytes">Содержимое файла.</param>
        /// <param name="contentType">MIME-тип файла.</param>
        /// <remarks>Метод выделен для повторного использования в сценариях T2/T3/T4.</remarks>
        private void WriteMultipartBytesPart(Stream stream, string boundary, string formFieldName, string fileName, byte[] bytes, string contentType)
        {
            var headerBuilder = new StringBuilder();
            headerBuilder.Append("--").Append(boundary).Append("\r\n");
            headerBuilder.Append("Content-Disposition: form-data; name=\"")
                .Append(formFieldName)
                .Append("\"; filename=\"")
                .Append(fileName)
                .Append("\"\r\n");
            headerBuilder.Append("Content-Type: ").Append(contentType).Append("\r\n\r\n");
            var headerBytes = Encoding.UTF8.GetBytes(headerBuilder.ToString());
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(bytes, 0, bytes.Length);
            var lineBreakBytes = Encoding.UTF8.GetBytes("\r\n");
            stream.Write(lineBreakBytes, 0, lineBreakBytes.Length);
        }

        /// <summary>
        /// Выполняет отправку заранее подготовленного multipart-запроса.
        /// </summary>
        /// <param name="endpoint">Полный URL endpoint.</param>
        /// <param name="boundary">Граница multipart-пакета.</param>
        /// <param name="requestBody">Сериализованное тело запроса.</param>
        /// <param name="responseParser">Функция разбора JSON-ответа.</param>
        /// <returns>Нормализованный результат вызова API.</returns>
        /// <remarks>Единая точка HTTP-вызова снижает риск расхождения поведения между сценариями.</remarks>
        private KonturSendTitleResult SendPreparedMultipart(string endpoint, string boundary, byte[] requestBody, Func<int, string, KonturSendTitleResult> responseParser)
        {
            const int maxAttempts = 3;
            var lastHttpStatus = 0;
            var lastResponseText = string.Empty;
            var lastErrorMessage = string.Empty;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    var request = (HttpWebRequest)WebRequest.Create(endpoint);
                    request.Method = "POST";
                    request.ContentType = "multipart/form-data; boundary=" + boundary;
                    ApplyCommonHeaders(request);
                    request.Timeout = 120000;
                    request.ReadWriteTimeout = 120000;
                    request.ContentLength = requestBody.Length;

                    using (var requestStream = request.GetRequestStream())
                    {
                        requestStream.Write(requestBody, 0, requestBody.Length);
                    }

                    using (var response = (HttpWebResponse)request.GetResponse())
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        var responseText = reader.ReadToEnd();
                        return responseParser((int)response.StatusCode, responseText);
                    }
                }
                catch (WebException webException)
                {
                    var httpStatus = 0;
                    var responseText = string.Empty;

                    if (webException.Response != null)
                    {
                        var httpResponse = (HttpWebResponse)webException.Response;
                        httpStatus = (int)httpResponse.StatusCode;
                        using (var stream = httpResponse.GetResponseStream())
                        using (var reader = new StreamReader(stream))
                        {
                            responseText = reader.ReadToEnd();
                        }
                    }

                    lastHttpStatus = httpStatus;
                    lastResponseText = responseText;
                    lastErrorMessage = "HttpError: " + webException.Message;

                    if (attempt < maxAttempts && IsRetryAllowed(httpStatus))
                    {
                        Thread.Sleep(GetRetryDelayMs(attempt, httpStatus));
                        continue;
                    }

                    return new KonturSendTitleResult
                    {
                        IsSuccess = false,
                        HttpStatus = httpStatus,
                        ErrorMessage = lastErrorMessage,
                        SanitizedResponsePayload = Truncate(responseText, 4000)
                    };
                }
                catch (Exception exception)
                {
                    lastHttpStatus = 0;
                    lastResponseText = exception.ToString();
                    lastErrorMessage = "UnhandledError: " + exception.Message;

                    if (attempt < maxAttempts)
                    {
                        Thread.Sleep(GetRetryDelayMs(attempt, 0));
                        continue;
                    }

                    return new KonturSendTitleResult
                    {
                        IsSuccess = false,
                        HttpStatus = 0,
                        ErrorMessage = lastErrorMessage,
                        SanitizedResponsePayload = Truncate(lastResponseText, 4000)
                    };
                }
            }

            return new KonturSendTitleResult
            {
                IsSuccess = false,
                HttpStatus = lastHttpStatus,
                ErrorMessage = string.IsNullOrEmpty(lastErrorMessage) ? "RetryExhausted" : lastErrorMessage,
                SanitizedResponsePayload = Truncate(lastResponseText, 4000)
            };
        }

        /// <summary>
        /// Определяет, можно ли повторить запрос после HTTP-ошибки.
        /// </summary>
        /// <param name="httpStatus">Код HTTP-ответа.</param>
        /// <returns>Истина, если ошибка считается временной.</returns>
        /// <remarks>Повторы применяются только для 408, 429 и серверных 5xx.</remarks>
        private bool IsRetryAllowed(int httpStatus)
        {
            return httpStatus == 408 || httpStatus == 429 || httpStatus >= 500;
        }

        /// <summary>
        /// Возвращает задержку перед повтором HTTP-вызова.
        /// </summary>
        /// <param name="attempt">Номер текущей попытки, начиная с 1.</param>
        /// <param name="httpStatus">Код HTTP-ответа для текущей ошибки.</param>
        /// <returns>Время ожидания в миллисекундах.</returns>
        /// <remarks>Для 429 используется более длинная задержка, чтобы снизить повторный rate-limit.</remarks>
        private int GetRetryDelayMs(int attempt, int httpStatus)
        {
            if (attempt < 1)
            {
                attempt = 1;
            }

            if (httpStatus == 429)
            {
                return attempt * 2000;
            }

            return attempt * 1000;
        }

        /// <summary>
        /// Разбирает успешный JSON-ответ для endpoint первичной отправки.
        /// </summary>
        /// <param name="httpStatus">HTTP-код ответа сервера.</param>
        /// <param name="responseText">Текст JSON-ответа.</param>
        /// <returns>Нормализованный результат отправки.</returns>
        /// <remarks>
        /// В primary-сценариях API возвращает transportationId и titleId,
        /// которые используются как ключевые refs для следующих шагов.
        /// </remarks>
        private KonturSendTitleResult ParseInitialResponse(int httpStatus, string responseText)
        {
            var response = JObject.Parse(responseText);
            var transportationId = Convert.ToString(response["transportationId"]);
            var titleId = Convert.ToString(response["titleId"]);

            return new KonturSendTitleResult
            {
                IsSuccess = httpStatus >= 200 && httpStatus < 300
                            && !string.IsNullOrEmpty(transportationId)
                            && !string.IsNullOrEmpty(titleId),
                HttpStatus = httpStatus,
                TransportationId = transportationId,
                TitleId = titleId,
                SanitizedResponsePayload = Truncate(responseText, 4000),
                ErrorMessage = string.Empty
            };
        }

        /// <summary>
        /// Разбирает успешный JSON-ответ для endpoint отправки черновика.
        /// </summary>
        /// <param name="httpStatus">HTTP-код ответа сервера.</param>
        /// <param name="responseText">Текст JSON-ответа.</param>
        /// <returns>Нормализованный результат отправки.</returns>
        /// <remarks>
        /// Для draft-ветки titleId заменяется на draftId как временный идентификатор отправленного титула.
        /// </remarks>
        private KonturSendTitleResult ParseDraftResponse(int httpStatus, string responseText)
        {
            var response = JObject.Parse(responseText);
            var transportationId = Convert.ToString(response["transportationId"]);
            var draftId = Convert.ToString(response["draftId"]);

            return new KonturSendTitleResult
            {
                IsSuccess = httpStatus >= 200 && httpStatus < 300
                            && !string.IsNullOrEmpty(transportationId)
                            && !string.IsNullOrEmpty(draftId),
                HttpStatus = httpStatus,
                TransportationId = transportationId,
                TitleId = draftId,
                SanitizedResponsePayload = Truncate(responseText, 4000),
                ErrorMessage = string.Empty
            };
        }

        /// <summary>
        /// Заполняет общие заголовки запроса к Kontur Logistics API.
        /// </summary>
        /// <param name="request">HTTP-запрос к Logistics API.</param>
        /// <remarks>
        /// Шаг вынесен отдельно, чтобы авторизация и служебная идентификация решения
        /// выставлялись одинаково во всех сценариях отправки T1-T4.
        /// </remarks>
        private void ApplyCommonHeaders(HttpWebRequest request)
        {
            ApplyAuthorizationHeaders(request);
            request.Accept = "application/json";

            if (!string.IsNullOrEmpty(SolutionInfo))
            {
                request.Headers["X-Solution-Info"] = SolutionInfo;
            }
        }

        /// <summary>
        /// Заполняет заголовки авторизации в зависимости от типа учетных данных.
        /// </summary>
        /// <param name="request">HTTP-запрос к Logistics API.</param>
        /// <remarks>
        /// Поддерживаются явные префиксы:
        /// `bearer:` для OIDC access token и `apiKey:` для API key.
        /// Для обратной совместимости JWT-значение без префикса также отправляется как Bearer.
        /// </remarks>
        private void ApplyAuthorizationHeaders(HttpWebRequest request)
        {
            var credential = (AccessToken ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(credential))
            {
                return;
            }

            if (credential.StartsWith("bearer:", StringComparison.OrdinalIgnoreCase))
            {
                credential = credential.Substring("bearer:".Length).Trim();
                if (!string.IsNullOrEmpty(credential))
                {
                    request.Headers["Authorization"] = "Bearer " + credential;
                }

                return;
            }

            if (credential.StartsWith("apiKey:", StringComparison.OrdinalIgnoreCase))
            {
                credential = credential.Substring("apiKey:".Length).Trim();
                if (!string.IsNullOrEmpty(credential))
                {
                    request.Headers["x-kontur-apikey"] = credential;
                }

                return;
            }

            if (IsLikelyJwt(credential))
            {
                request.Headers["Authorization"] = "Bearer " + credential;
                return;
            }

            request.Headers["x-kontur-apikey"] = credential;
        }

        /// <summary>
        /// Проверяет, похоже ли значение на JWT access token.
        /// </summary>
        /// <param name="value">Строка учетных данных.</param>
        /// <returns>True, если формат соответствует JWT (header.payload.signature).</returns>
        private bool IsLikelyJwt(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            var firstDot = value.IndexOf('.');
            if (firstDot <= 0)
            {
                return false;
            }

            var secondDot = value.IndexOf('.', firstDot + 1);
            return secondDot > firstDot + 1 && secondDot < value.Length - 1;
        }

        /// <summary>
        /// Ограничивает длину диагностического текста для безопасного хранения в БД.
        /// </summary>
        /// <param name="value">Исходная строка payload или ошибки.</param>
        /// <param name="maxLength">Максимально допустимая длина.</param>
        /// <returns>Обрезанная строка или пустое значение.</returns>
        /// <remarks>Нужно для защиты полей raw-логов от переполнения.</remarks>
        private string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, maxLength);
        }
    }
}
