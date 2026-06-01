/*
  ФАЙЛ: KonturProbe.aspx.cs
  НАЗНАЧЕНИЕ: Продуктовая страница ручного запуска этапов ЭТрН Контур внутри ТИС.
  Реализует единый сценарий запуска этапа через оркестратор без Lite-веток и ручного выбора роли отправителя.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  07.05.2026 - Первичное создание страницы для тестового запуска T1.
  12.05.2026 - Полная переработка в production-flow: единый запуск этапов T1/T2 и проверка ролевых доступов.
  12.05.2026 - Добавлен вывод продуктовой API-диагностики (HttpStatus, traceId, error.code, error.message) по raw-логу.
  12.05.2026 - Добавлено чтение диагностики с фильтрацией по этапу и отображением runId попытки.
  13.05.2026 - Добавлен этап T3 в UI-валидацию, ролевую проверку и единый запуск.
  13.05.2026 - Разрешен запуск T3 через внутренние артефакты без обязательных путей к XML и подписи.
  13.05.2026 - Добавлен этап T4 в UI-валидацию, ролевую проверку и единый запуск.
  14.05.2026 - Поля путей XML/SGN заменены на выбор файлов из серверной папки App_Data\Temp\KonturEtrn.
  15.05.2026 - Добавлена кнопка генерации T1 XML в серверной папке для полного контура без Astral UI.
  15.05.2026 - Добавлена генерация T2 XML в App_Data\Temp\KonturEtrn и снято обязательное требование ручного .sgn для T2/T3/T4.
  15.05.2026 - Добавлено авто-восстановление T1 XML в epd_doc_store перед сборкой T2, чтобы BuildTitul2Xml и SignEpd работали без падения.
  18.05.2026 - Добавлено авто-сохранение uid_zak из TransportationId перед сборкой T2 и ранняя диагностика отсутствующей подписи T2.
  18.05.2026 - Добавлен fallback определения idzak через tis_entity_id для сборки T2 на timeline без прямой связки idzak.
  18.05.2026 - Добавлены генерация T3/T4 XML из внутреннего хранилища артефактов и упрощен UI сценария.
  18.05.2026 - Убрано ложное ожидание готовых T3/T4 файлов в серверной папке; уточнена диагностика генерации.
  21.05.2026 - Сборка ИдФайл T2 переведена на прямое наследование участников из T1, чтобы исключить рассинхрон ФНС ИД.
  21.05.2026 - Генерация T1 на странице синхронизирована с контурным override участников, чтобы T2 наследовал актуальные 2BM ФНС ИД.
  21.05.2026 - Генерация T1 дополнена сохранением XML в epd_doc_store, а сборка T2 теперь требует существующую подпись T1.
  21.05.2026 - Добавлен отдельный ручной импорт .sgn из серверной папки в epd_doc_store для сценария локальной подписи T1/T2.
  21.05.2026 - Синхронизация T1 для сборки T2 переведена на приоритет t1_override, чтобы исключить возврат к старым 2AE идентификаторам.
  21.05.2026 - Сборка T2 переведена на текущего пользователя ТИС, чтобы Контур видел фактического подписанта перевозчика.
  21.05.2026 - Перед сборкой T2 добавлена явная проверка права подписи за перевозчика через ResolveEtrnSigningContext.
  22.05.2026 - Добавлен явный выбор подписанта этапа с хранением в Perdoc по TimelineId и StageCode.
  22.05.2026 - Ручной импорт .sgn дополнен предупреждением о несовпадении подписи с выбранным подписантом.
  22.05.2026 - Разрешение выбранного подписанта этапа вынесено в KonturStageSignerService для общей stage-service модели.
  22.05.2026 - Добавлен postback по TimelineId, чтобы подписанты и серверные файлы обновлялись сразу после смены документа.
  23.05.2026 - Добавлены статусы шагов этапа и серверная валидация порядка "сформировать -> подписать -> отправить".
  23.05.2026 - Пересобран рабочий слой артефактов этапов: актуальный XML теперь синхронизируется между БД и папкой без накопления лишних копий.
  23.05.2026 - Нормализация T1 переведена на единый сервис полной синхронизации участников и ИдФайл.
  23.05.2026 - Сборка T1/T2 адаптирована к актуальным сигнатурам snapshot-ветки, а прямой builder T3/T4 отключен до появления совместимых методов.
  23.05.2026 - Добавлена синхронизация узла подписанта в XML этапов T2/T3/T4 по выбранному подписанту этапа.
  23.05.2026 - Нормализация XML этапов T1-T4 переведена на единый фасад KonturEtrnT1234XmlService.
  23.05.2026 - Статусный расчет этапа вынесен в KonturProbeStageFlowService, чтобы сократить нагрузку и объем code-behind.
  25.05.2026 - Сборка T2 дополнена синхронизацией ссылки на T1 и base64-подписи T1, чтобы исключить неконсистентный ИдИнфГО.
  28.05.2026 - Запуск T1/T2 переведен на приоритет SQL-артефакта этапа, чтобы генерация, подпись и отправка использовали одну версию XML.
  28.05.2026 - Добавлен авто-подбор актуальных XML/SGN по этапу при пустом выборе в селекторах, чтобы исключить ложную блокировку шага "Подписать".
  28.05.2026 - Добавлен сброс sig2_detached после пересборки T2 XML и предвалидация соответствия XML/SGN перед отправкой T2.
  28.05.2026 - Ручной импорт подписи T2 переведен на явную привязку к выбранному XML этапа для корректной локальной проверки.
  28.05.2026 - Полностью отключена автоматическая подстановка XML/SGN: запуск этапов переведен на ручной выбор файлов оператором.
  28.05.2026 - Генерация T2 и окно SignEpd переведены на единый нормализованный XML этапа, чтобы исключить рассинхрон хеша между БД и файлом.
  28.05.2026 - Ручной импорт .sgn теперь блокируется при hash-mismatch, чтобы ошибочная подпись не перезаписывала корректный stage-артефакт.
  28.05.2026 - Добавлено чтение флага Kontur-only тестового режима по TimelineId для поэтапного отделения тестового сценария от штатного контура.
  29.05.2026 - Добавлен add-only мост в реконструкционный слой для сборки T3/T4 и импорта подписи без переключения runtime-отправки.
  29.05.2026 - Экранное summary этапа переведено на KonturStageScreenService с отображением источника состояния.
  29.05.2026 - Добавлены bridge отправки T2/T3/T4 через reconstruction use case-ы и явное подтверждение этапа оператором.
  29.05.2026 - Уточнены bridge-комментарии и серверные проверки путей XML/SGN для нового send-path.
  29.05.2026 - Добавлена синхронизация TransportationId и TitleId из TEpdOperatorRef в явное состояние этапа после legacy runtime.
  29.05.2026 - Пересборка T1/T2 переведена на явный сброс состояния этапа, чтобы новый XML не оставлял Sent и старые refs от предыдущей отправки.
  29.05.2026 - Исправлено создание gateway ответных титулов: KonturAdapter собирается через KonturClient и ролевой access context титула.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Security.Cryptography.Pkcs;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Xml.Linq;
using System.Xml;
using tis.Modules;
using TIS.EPD;
using Tis.KonturIntegration.Integration;
using Tis.KonturIntegration.Models;
using Tis.KonturIntegration.Storage;

namespace tis.Account.EpdSrc
{
    /// <summary>
    /// Веб-страница для запуска этапов ЭТрН Контур в рамках ядра ТИС.
    /// </summary>
    public partial class KonturProbe : System.Web.UI.Page
    {
        /// <summary>
        /// Хранит кэш статусного состояния этапа в рамках одного server-request.
        /// </summary>
        /// <remarks>
        /// Кэш нужен, потому что разметка последовательно вызывает несколько getter-методов,
        /// а без кэша страница повторяет одинаковые SQL- и файловые проверки на одном рендере.
        /// </remarks>
        private KonturProbeStageFlowState _currentStageFlowState;

        /// <summary>
        /// Хранит кэш экранной модели этапа в рамках одного server-request.
        /// </summary>
        /// <remarks>
        /// Кэш нужен, чтобы разметка и code-behind не перестраивали ScreenService-модель повторно
        /// при чтении итогового summary и источника состояния на одном рендере страницы.
        /// </remarks>
        private KonturStageScreenModel _currentStageScreenModel;

        /// <summary>
        /// Выполняет инициализацию страницы.
        /// </summary>
        /// <param name="sender">Источник события загрузки страницы.</param>
        /// <param name="e">Аргументы события загрузки.</param>
        /// <remarks>На текущем этапе дополнительная инициализация не требуется.</remarks>
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                BindServerFileSelectors();
                EnsureHiddenStageIsInitialized();
                BindStageSignerSelector(true);
            }
        }

        /// <summary>
        /// Синхронизирует серверный stage-контекст после переключения вкладки этапа.
        /// </summary>
        /// <param name="sender">Источник события скрытой кнопки.</param>
        /// <param name="e">Аргументы события click.</param>
        /// <remarks>Метод нужен, чтобы серверный блок выбора подписанта соответствовал текущей вкладке этапа.</remarks>
        protected void btnApplyStage_Click(object sender, EventArgs e)
        {
            try
            {
                ResetCurrentStageFlowState();
                EnsureHiddenStageIsInitialized();
                BindStageSignerSelector(true);
            }
            catch (Exception ex)
            {
                LogErr("Ошибка синхронизации этапа", ex);
            }
        }
        /// <summary>
        /// Обновляет контекст документа после смены TimelineId.
        /// </summary>
        /// <param name="sender">Источник события текстового поля TimelineId.</param>
        /// <param name="e">Аргументы события изменения текста.</param>
        /// <remarks>
        /// Postback по TimelineId нужен, чтобы оператор сразу видел актуальные серверные файлы
        /// и список подписантов для текущего документа без ручного переключения этапа.
        /// </remarks>
        protected void tbTimelineId_TextChanged(object sender, EventArgs e)
        {
            try
            {
                ResetCurrentStageFlowState();
                EnsureHiddenStageIsInitialized();
                BindServerFileSelectors();
                BindStageSignerSelector(true);
            }
            catch (Exception ex)
            {
                LogErr("Ошибка обновления данных по TimelineId", ex);
            }
        }
        /// <summary>
        /// Сохраняет выбор подписанта текущего этапа.
        /// </summary>
        /// <param name="sender">Источник события списка подписантов.</param>
        /// <param name="e">Аргументы события изменения выбора.</param>
        /// <remarks>
        /// Выбор сохраняется в Perdoc по связке TimelineId и StageCode, чтобы переживать postback
        /// и повторные открытия страницы без повторного ручного выбора.
        /// </remarks>
        protected void ddlStageSigner_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                ResetCurrentStageFlowState();
                EnsureHiddenStageIsInitialized();
                SaveCurrentStageSignerSelection();
            }
            catch (Exception ex)
            {
                LogErr("Ошибка сохранения подписанта этапа", ex);
            }
        }

        /// <summary>
        /// Запускает выбранный этап ЭТрН через единый сервис оркестрации.
        /// </summary>
        /// <param name="sender">Источник события кнопки запуска.</param>
        /// <param name="e">Аргументы события click.</param>
        /// <remarks>
        /// Метод валидирует входные пути и делегирует отправку в KonturEtrnStageService,
        /// который сам выбирает нужную ролевую конфигурацию доступа.
        /// </remarks>
        protected void btnRunStage_Click(object sender, EventArgs e)
        {
            try
            {
                ResetCurrentStageFlowState();
                var timelineId = ParseTimelineId(tbTimelineId.Text.Trim());
                var stageCode = (ddlStage.SelectedValue ?? string.Empty).Trim();
                string xmlPath;
                string signaturePath;
                ResolveServerSelectedPaths(out xmlPath, out signaturePath);
                if (ShouldUseArtifactExecution(timelineId, stageCode))
                {
                    xmlPath = string.Empty;
                    signaturePath = string.Empty;
                }
                EnsureConfiguredSignerSelection(timelineId, stageCode);

                // До сетевого вызова проверяем физическое наличие файлов, чтобы отдавать пользователю понятную диагностику.
                ValidateStageInput(stageCode, timelineId, xmlPath, signaturePath);
                ValidateStageSequenceBeforeSend(stageCode, timelineId, xmlPath, signaturePath);

                var executionPath = "LegacyRuntime";
                var result = ExecuteStageThroughPreferredRuntime(timelineId, stageCode, xmlPath, signaturePath, out executionPath);
                if (!result.IsSuccess)
                {
                    throw new ApplicationException("Kontur " + stageCode + ": " + result.Message);
                }

                // После успешного вызова сразу показываем операторную диагностику, чтобы не открывать SSMS вручную.
                AppendLastApiDiagnostics(timelineId, stageCode);
                LogOk(
                    "Этап " + stageCode + " выполнен успешно" + Environment.NewLine +
                    "executionPath=" + executionPath + Environment.NewLine +
                    "transportationId=" + Safe(result.TransportationId) + Environment.NewLine +
                    "titleId=" + Safe(result.TitleId));
            }
            catch (Exception ex)
            {
                TryAppendDiagnosticsSafe(tbTimelineId.Text.Trim(), (ddlStage.SelectedValue ?? string.Empty).Trim());
                LogErr("Ошибка запуска этапа", ex);
            }
        }

        /// <summary>
        /// Обновляет списки выбора серверных файлов XML/SGN.
        /// </summary>
        /// <param name="sender">Источник события кнопки обновления.</param>
        /// <param name="e">Аргументы события click.</param>
        /// <remarks>Метод нужен для подхвата новых файлов, уже размещенных в папке окружения без перезагрузки страницы.</remarks>
        protected void btnReloadServerFiles_Click(object sender, EventArgs e)
        {
            try
            {
                ResetCurrentStageFlowState();
                BindServerFileSelectors();
                LogOk("Списки серверных файлов обновлены.");
            }
            catch (Exception ex)
            {
                LogErr("Ошибка обновления списков файлов", ex);
            }
        }

        /// <summary>
        /// Импортирует выбранную серверную подпись .sgn в epd_doc_store для T1 или T2.
        /// </summary>
        /// <param name="sender">Источник события кнопки импорта подписи.</param>
        /// <param name="e">Аргументы события click.</param>
        /// <remarks>
        /// Метод нужен для ручного сценария, когда оператор подписывает XML локальным сертификатом вне SignEpd,
        /// затем загружает detached CMS на сервер и связывает его с актуальным XML текущего timeline.
        /// Для T3/T4 импорт в epd_doc_store не выполняется, потому что в таблице нет отдельных слотов этих подписей.
        /// </remarks>
        protected void btnImportServerSignature_Click(object sender, EventArgs e)
        {
            try
            {
                var timelineId = ParseTimelineId(tbTimelineId.Text.Trim());
                var stageCode = (ddlStage.SelectedValue ?? string.Empty).Trim();
                string xmlPath;
                string signaturePath;
                ResolveServerSelectedPaths(out xmlPath, out signaturePath);

                if (string.IsNullOrEmpty(signaturePath))
                {
                    throw new ApplicationException("Для ручного импорта подписи выберите .sgn из серверной папки.");
                }

                if (!File.Exists(signaturePath))
                {
                    throw new ApplicationException("Файл подписи не найден: " + signaturePath);
                }

                var selectedSigner = GetSelectedStageSignerCandidateOrNull(timelineId, stageCode);
                var sigBytes = File.ReadAllBytes(signaturePath);
                EnsureImportedSignatureMatchesStageXml(timelineId, stageCode, xmlPath, sigBytes);
                var stateSource = "LegacyFallback";

                if (CanUseSignatureImportUseCase(timelineId, stageCode))
                {
                    var importResult = CreateImportStageSignatureUseCase().Execute(timelineId, stageCode, signaturePath);
                    if (importResult == null || !importResult.IsSuccess)
                    {
                        throw new ApplicationException(
                            "Импорт подписи через реконструкционный слой завершился ошибкой." + Environment.NewLine +
                            "details=" + Safe(importResult == null ? "EmptyImportStageSignatureResult" : importResult.Message));
                    }

                    SaveLegacySignatureCompatibilityCopy(timelineId, stageCode, xmlPath, sigBytes);
                    stateSource = "ReconstructionUseCase";
                }
                else
                {
                    var signatureSlot = ResolveManualSignatureSlot(stageCode);
                    EnsureDocStoreXmlForManualSignatureImport(timelineId, stageCode, xmlPath);
                    EpdRepo.SaveSignature(
                        timelineId,
                        sigBytes,
                        signatureSlot.ToString(),
                        "Ручной импорт подписи KonturProbe",
                        0);

                    SyncStageSignatureArtifact(timelineId, stageCode, signaturePath, sigBytes, selectedSigner);
                    BridgePreparedStageStateAfterLegacySignatureImport(timelineId, stageCode, xmlPath, sigBytes);
                }

                var warningText = BuildManualSignatureWarningText(timelineId, stageCode, xmlPath, selectedSigner, sigBytes);
                var signatureSlotText = ResolveManualSignatureSlotText(stageCode);
                LogOk(
                    "Подпись импортирована." + Environment.NewLine +
                    "timelineId=" + timelineId + Environment.NewLine +
                    "stage=" + stageCode + Environment.NewLine +
                    "signatureSlot=" + signatureSlotText + Environment.NewLine +
                    "sgnFile=" + Path.GetFileName(signaturePath) + Environment.NewLine +
                    "stateSource=" + stateSource +
                    (string.IsNullOrEmpty(warningText) ? string.Empty : (Environment.NewLine + warningText)));
            }
            catch (Exception ex)
            {
                LogErr("Ошибка ручного импорта подписи", ex);
            }
        }

        /// <summary>
        /// Подтверждает завершение текущего этапа оператором и при необходимости открывает следующий шаг.
        /// </summary>
        /// <param name="sender">Источник события кнопки подтверждения.</param>
        /// <param name="e">Аргументы события click.</param>
        /// <remarks>
        /// Подтверждение используется как временный операторский мост, пока завершение этапа
        /// еще не переводится в Completed автоматически отдельным backend-сценарием.
        /// </remarks>
        protected void btnConfirmStage_Click(object sender, EventArgs e)
        {
            try
            {
                ResetCurrentStageFlowState();
                var timelineId = ParseTimelineId(tbTimelineId.Text.Trim());
                var stageCode = (ddlStage.SelectedValue ?? string.Empty).Trim();
                string xmlPath;
                string signaturePath;
                ResolveServerSelectedPaths(out xmlPath, out signaturePath);

                EnsureStageStateReadyForManualCompletion(timelineId, stageCode, xmlPath, signaturePath);
                var state = CreateConfirmStageCompletionUseCase().Execute(
                    timelineId,
                    stageCode,
                    "Этап подтвержден оператором на странице KonturProbe.");

                LogOk(
                    "Этап подтвержден." + Environment.NewLine +
                    "timelineId=" + timelineId + Environment.NewLine +
                    "stage=" + stageCode + Environment.NewLine +
                    "completed=" + (state != null && state.Completed ? "true" : "false") + Environment.NewLine +
                    "nextStageAllowed=" + (state != null && state.NextStageAllowed ? "true" : "false"));
            }
            catch (Exception ex)
            {
                LogErr("Ошибка подтверждения этапа", ex);
            }
        }

        /// <summary>
        /// Проверяет, что импортируемая подпись соответствует XML текущего этапа.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код текущего этапа UI.</param>
        /// <param name="xmlPath">Путь к выбранному XML этапа.</param>
        /// <param name="signatureBytes">Байты импортируемой подписи.</param>
        /// <remarks>
        /// Проверка выполняется до сохранения в БД, чтобы ручной импорт не затирал корректную подпись этапа
        /// неподходящим .sgn и не создавал ложный статус "Подпись готова".
        /// </remarks>
        private void EnsureImportedSignatureMatchesStageXml(long timelineId, string stageCode, string xmlPath, byte[] signatureBytes)
        {
            if (signatureBytes == null || signatureBytes.Length == 0)
            {
                throw new ApplicationException("Импортируемая подпись пуста.");
            }

            var xmlBytes = GetStageXmlBytesForSignatureWarning(timelineId, stageCode, xmlPath);
            if (xmlBytes == null || xmlBytes.Length == 0)
            {
                throw new ApplicationException("Для проверки подписи не найден XML текущего этапа.");
            }

            string verifyInfo;
            if (!EpdRepo.VerifyDetachedCms(xmlBytes, signatureBytes, out verifyInfo))
            {
                throw new ApplicationException(
                    "Импортируемая подпись не соответствует выбранному XML этапа." + Environment.NewLine +
                    "action=Подпишите именно текущий XML этого этапа и импортируйте новый .sgn." + Environment.NewLine +
                    "details=" + Safe(verifyInfo));
            }
        }
        /// <summary>
        /// Разрешает абсолютные пути к выбранным серверным XML/SGN-файлам.
        /// </summary>
        /// <param name="xmlPath">Итоговый путь к XML на сервере.</param>
        /// <param name="signaturePath">Итоговый путь к подписи на сервере.</param>
        /// <remarks>Пустой выбор возвращается как пустая строка для поддержки внутреннего режима T2/T3/T4.</remarks>
        /// <summary>
        /// Формирует новый XML титула T1 по текущему timeline и сохраняет его в серверную папку выбора.
        /// </summary>
        /// <param name="sender">Источник события кнопки генерации.</param>
        /// <param name="e">Аргументы события click.</param>
        /// <remarks>
        /// Используется штатный builder ТИС ETRNtituls.Titul_1, чтобы не дублировать маппинг полей.
        /// Сформированный XML сохраняется в App_Data\Temp\KonturEtrn и сразу доступен в селекторе.
        /// </remarks>
        protected void btnBuildT1Xml_Click(object sender, EventArgs e)
        {
            try
            {
                var timelineId = ParseTimelineId(tbTimelineId.Text.Trim());
                var tisEntityId = GetTisEntityIdByTimeline(timelineId);
                if (string.IsNullOrEmpty(tisEntityId))
                {
                    throw new ApplicationException("Для указанного TimelineId не найден tis_entity_id.");
                }

                var idzak = ResolveIdzakForT2(timelineId);
                if (idzak <= 0)
                {
                    throw new ApplicationException("Для указанного TimelineId не найден idzak для выбора подписанта T1.");
                }

                var signingUserId = ResolveSigningUserIdForKonturStage(timelineId, idzak, "T1");

                string builderError;
                var xml = global::ETRNtituls.Titul_1(tisEntityId, out builderError, null);
                if (!string.IsNullOrEmpty(builderError))
                {
                    throw new ApplicationException("Ошибка генерации T1: " + builderError);
                }

                if (string.IsNullOrEmpty(xml))
                {
                    throw new ApplicationException("Генератор T1 вернул пустой XML.");
                }

                var root = GetKonturServerFilesDirectory();
                Directory.CreateDirectory(root);

                var sourcePath = Path.Combine(root, BuildT1ServerFileName(timelineId, tisEntityId));
                var xmlBytes = Encoding.GetEncoding(1251).GetBytes(xml);
                File.WriteAllBytes(sourcePath, xmlBytes);

                var normalizedT1Path = ApplyKonturT1ParticipantsOverride(sourcePath, timelineId);
                var normalizedXmlBytes = File.ReadAllBytes(normalizedT1Path);
                EpdRepo.UpsertDoc(timelineId, normalizedXmlBytes, null, null);

                var currentXmlPath = SaveCurrentStageXmlArtifact(timelineId, "T1", normalizedXmlBytes);
                ResetStageStateAfterManualXmlBuild(timelineId, "T1_INITIAL", "T1");
                InvalidateT1SignatureAfterXmlRebuild(timelineId);
                var selectedFileName = Path.GetFileName(currentXmlPath);

                if (File.Exists(sourcePath) && !string.Equals(sourcePath, currentXmlPath, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(sourcePath);
                }

                if (File.Exists(normalizedT1Path) &&
                    !string.Equals(normalizedT1Path, sourcePath, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(normalizedT1Path, currentXmlPath, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(normalizedT1Path);
                }

                BindServerFileSelectors();
                ddlServerXmlFile.SelectedValue = selectedFileName;
                ddlStage.SelectedValue = "T1_INITIAL";

                LogOk(
                    "T1 XML сформирован и сохранен." + Environment.NewLine +
                    "timelineId=" + timelineId + Environment.NewLine +
                    "tisEntityId=" + tisEntityId + Environment.NewLine +
                    "xmlFile=" + selectedFileName + Environment.NewLine +
                    "docStoreSync=done");
            }
            catch (Exception ex)
            {
                LogErr("Ошибка генерации T1 XML", ex);
            }
        }

        /// <summary>
        /// Формирует новый XML титула T2 по текущему timeline и сохраняет его в серверную папку выбора.
        /// </summary>
        /// <param name="sender">Источник события кнопки генерации.</param>
        /// <param name="e">Аргументы события click.</param>
        /// <remarks>
        /// Используется штатный builder ТИС EZZtituls.BuildTitul2Xml, чтобы сохранить совместимость с текущим
        /// форматом титула перевозчика. XML также сохраняется в EPD-хранилище для внутреннего stage-runner.
        /// </remarks>
        protected void btnBuildT2Xml_Click(object sender, EventArgs e)
        {
            try
            {
                var timelineId = ParseTimelineId(tbTimelineId.Text.Trim());
                var idzak = ResolveIdzakForT2(timelineId);
                if (idzak <= 0)
                {
                    throw new ApplicationException("Для указанного TimelineId не найден idzak.");
                }

                EnsureT1XmlInDocStoreForT2(timelineId);
                EnsureUidZakForT2(timelineId);

                var xml = BuildKonturT2Xml(timelineId, idzak);
                if (string.IsNullOrEmpty(xml))
                {
                    throw new ApplicationException("Генератор T2 вернул пустой XML.");
                }

                var xmlBytes = Encoding.GetEncoding(1251).GetBytes(xml);
                var currentXmlPath = SaveCurrentStageXmlArtifact(timelineId, "T2", xmlBytes);
                ResetStageStateAfterManualXmlBuild(timelineId, "T2", "T2");
                var normalizedXmlBytes = File.ReadAllBytes(currentXmlPath);

                // Для T2 критично, чтобы SignEpd, локальная CMS-проверка и отправка в API работали по одному набору байтов.
                // Поэтому сначала сохраняем нормализованный рабочий XML, затем этой же версией обновляем payload_xml_t2 в БД.
                EpdRepo.SaveTitul2Xml(timelineId, normalizedXmlBytes);
                InvalidateT2SignatureAfterXmlRebuild(timelineId);
                var fileName = Path.GetFileName(currentXmlPath);

                BindServerFileSelectors();
                ddlServerXmlFile.SelectedValue = fileName;
                ddlStage.SelectedValue = "T2";

                LogOk(
                    "T2 XML сформирован и сохранен." + Environment.NewLine +
                    "timelineId=" + timelineId + Environment.NewLine +
                    "idzak=" + idzak + Environment.NewLine +
                    "xmlFile=" + fileName + Environment.NewLine +
                    "Подпись T2: откройте SignEpd и подпишите текущий титул для timeline.");
            }
            catch (Exception ex)
            {
                LogErr("Ошибка генерации T2 XML", ex);
            }
        }

        /// <summary>
        /// Собирает XML титула T2 в формате ON_TRNACLPPRIN через штатный builder ETRNtituls.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="idzak">Идентификатор заявки ТИС.</param>
        /// <returns>Готовый XML T2 в кодировке CP1251.</returns>
        /// <remarks>
        /// Метод использует связку на актуальный T1 (ИдФайл, дата/время, подпись) и УИД_ТрН,
        /// чтобы формировать T2 в том же профиле, который успешно проходит в UI Контура.
        /// </remarks>
        private string BuildKonturT2Xml(long timelineId, long idzak)
        {
            var t1XmlText = EpdRepo.GetXmlText(timelineId);
            if (string.IsNullOrEmpty(t1XmlText))
            {
                throw new ApplicationException("Для сборки T2 не найден T1 XML в epd_doc_store.");
            }

            var t1Info = ExtractT1InfoForKonturT2(t1XmlText);
            if (string.IsNullOrEmpty(t1Info.IdFile))
            {
                throw new ApplicationException("Для сборки T2 не удалось определить ИдФайл из T1 XML.");
            }

            var uidTrN = (t1Info.UidTrN ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(uidTrN))
            {
                uidTrN = (EpdRepo.GetUidZak(timelineId) ?? string.Empty).Trim();
            }

            if (string.IsNullOrEmpty(uidTrN))
            {
                throw new ApplicationException("Для сборки T2 не найден УИД_ТрН (из T1 XML/uid_zak).");
            }

            var t1SigBytes = GetLatestNonEmptyT1SignatureBytes(timelineId);
            var t1SigBase64 = t1SigBytes != null && t1SigBytes.Length > 0
                ? Convert.ToBase64String(t1SigBytes)
                : string.Empty;
            if (string.IsNullOrEmpty(t1SigBase64))
            {
                throw new ApplicationException("Для сборки T2 не найдена подпись T1 в epd_doc_store. Сначала подпишите T1 через SignEpd или импортируйте .sgn в БД, затем повторите сборку T2.");
            }
            var prebuiltT2IdFile = BuildKonturT2IdFile(t1Info.IdFile);
            var xml = global::EZZtituls.BuildTitul2Xml(timelineId, idzak.ToString(), string.Empty, string.Empty);
            if (string.IsNullOrEmpty(xml))
            {
                throw new ApplicationException("BuildTitul2Xml вернул пустой XML.");
            }

            return ApplyKonturT2ReferenceOverride(xml, prebuiltT2IdFile, t1Info, t1SigBase64);
        }

        /// <summary>
        /// Формирует ИдФайл T2 в формате ON_TRNACLPPRIN на основе уже принятого Контуром ИдФайл T1.
        /// </summary>
        /// <param name="t1IdFile">ИдФайл титула T1, по которому Контур уже создал перевозку.</param>
        /// <returns>ИдФайл T2 или пустая строка, если из T1 нельзя извлечь участников.</returns>
        /// <remarks>
        /// В успешном контурном сценарии участники T2 должны ссылаться на те же ФНС ИД, что были использованы в T1.
        /// Для T1 порядок сегментов: ТК, ГП, ГО. Для T2 Контур ожидает: ГП, ГО, ТК.
        /// </remarks>
        private string BuildKonturT2IdFile(string t1IdFile)
        {
            var participantIds = ExtractParticipantIdsFromT1IdFile(t1IdFile);
            if (participantIds == null)
            {
                return string.Empty;
            }

            return string.Format(
                "ON_TRNACLPPRIN_{0}_{1}_{2}_0_{3}_{4}",
                participantIds.ConsigneeFnsId,
                participantIds.ConsignorFnsId,
                participantIds.CarrierFnsId,
                DateTime.Now.ToString("yyyyMMdd"),
                Guid.NewGuid().ToString());
        }

        /// <summary>
        /// Извлекает ФНС ИД участников из ИдФайл T1.
        /// </summary>
        /// <param name="t1IdFile">ИдФайл T1 в формате ON_TRNACLGROT_... .</param>
        /// <returns>Набор идентификаторов участников или null, если формат не распознан.</returns>
        /// <remarks>
        /// Сегменты ИдФайл T1 используются как первичный источник истины, потому что именно они уже были приняты Контуром.
        /// Это исключает конфликт между настройками окружения и фактическим составом участников перевозки.
        /// </remarks>
        private T1ParticipantIds ExtractParticipantIdsFromT1IdFile(string t1IdFile)
        {
            var normalizedIdFile = (t1IdFile ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(normalizedIdFile))
            {
                return null;
            }

            var parts = normalizedIdFile.Split('_');
            if (parts.Length < 7 || !string.Equals(parts[0], "ON", StringComparison.OrdinalIgnoreCase) || !string.Equals(parts[1], "TRNACLGROT", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // Для T1 Контур использует порядок сегментов: ТК, ГП, ГО.
            var carrierFnsId = (parts[2] ?? string.Empty).Trim();
            var consigneeFnsId = (parts[3] ?? string.Empty).Trim();
            var consignorFnsId = (parts[4] ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(carrierFnsId) || string.IsNullOrEmpty(consigneeFnsId) || string.IsNullOrEmpty(consignorFnsId))
            {
                return null;
            }

            return new T1ParticipantIds
            {
                CarrierFnsId = carrierFnsId,
                ConsigneeFnsId = consigneeFnsId,
                ConsignorFnsId = consignorFnsId
            };
        }
        private T1InfoForKonturT2 ExtractT1InfoForKonturT2(string t1XmlText)
        {
            var xml = XDocument.Parse(t1XmlText);
            return new T1InfoForKonturT2
            {
                IdFile = ReadFirstAttributeValue(xml, "ИдФайл"),
                DocDate = ReadFirstAttributeValue(xml, "ДатИнфГО"),
                DocTime = ReadFirstAttributeValue(xml, "ВрИнфГО"),
                UidTrN = ReadFirstAttributeValue(xml, "УИД_ТрН")
            };
        }

        /// <summary>
        /// Читает первое непустое значение атрибута в XML-документе.
        /// </summary>
        /// <param name="xml">XML-документ для поиска.</param>
        /// <param name="attributeName">Имя искомого атрибута.</param>
        /// <returns>Значение атрибута или пустая строка.</returns>
        /// <remarks>Нужен для устойчивого извлечения полей T1 при различиях сериализации.</remarks>
        private string ReadFirstAttributeValue(XDocument xml, string attributeName)
        {
            if (xml == null || string.IsNullOrEmpty(attributeName))
            {
                return string.Empty;
            }

            var rootAttribute = xml.Root != null ? xml.Root.Attribute(attributeName) : null;
            if (rootAttribute != null && !string.IsNullOrEmpty(rootAttribute.Value))
            {
                return rootAttribute.Value;
            }

            foreach (var element in xml.Descendants())
            {
                var attribute = element.Attribute(attributeName);
                if (attribute != null && !string.IsNullOrEmpty(attribute.Value))
                {
                    return attribute.Value;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Контейнер атрибутов T1 для построения T2.
        /// </summary>
        private sealed class T1InfoForKonturT2
        {
            /// <summary>Идентификатор файла T1 (ИдФайл).</summary>
            public string IdFile { get; set; }

            /// <summary>Дата документа T1 (ДатИнфГО).</summary>
            public string DocDate { get; set; }

            /// <summary>Время документа T1 (ВрИнфГО).</summary>
            public string DocTime { get; set; }

            /// <summary>УИД транспортной накладной из T1 (УИД_ТрН).</summary>
            public string UidTrN { get; set; }
        }

        /// <summary>
        /// Контейнер ФНС ИД участников, извлеченных из ИдФайл T1.
        /// </summary>
        private sealed class T1ParticipantIds
        {
            /// <summary>ФНС ИД перевозчика.</summary>
            public string CarrierFnsId { get; set; }

            /// <summary>ФНС ИД грузополучателя.</summary>
            public string ConsigneeFnsId { get; set; }

            /// <summary>ФНС ИД грузоотправителя.</summary>
            public string ConsignorFnsId { get; set; }
        }
        protected void btnBuildT3Xml_Click(object sender, EventArgs e)
        {
            BuildStoredStageXml("T3");
        }

        /// <summary>
        /// Готовит XML титула T4 из внутреннего хранилища артефактов и сохраняет его в серверную папку.
        /// </summary>
        /// <param name="sender">Источник события кнопки генерации.</param>
        /// <param name="e">Аргументы события click.</param>
        /// <remarks>
        /// Для T4 используется текущий builder stage-runner: он читает подготовленный XML из TEpdTitleArtifact.
        /// Если артефакт отсутствует, нужно сначала пройти предыдущие этапы и получить/сохранить T4-данные.
        /// </remarks>
        protected void btnBuildT4Xml_Click(object sender, EventArgs e)
        {
            BuildStoredStageXml("T4");
        }


        /// <summary>
        /// Определяет idzak для сборки T2 по timeline.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline.</param>
        /// <returns>Идентификатор заявки idzak или 0, если определить не удалось.</returns>
        /// <remarks>
        /// Базовый источник — EpdRepo.GetIdzakByTimeline.
        /// Для тестовых/ручных timeline применяется fallback на числовой tis_entity_id из epd_timeline.
        /// </remarks>
        private long ResolveIdzakForT2(long timelineId)
        {
            var idzak = EpdRepo.GetIdzakByTimeline(timelineId);
            if (idzak > 0)
            {
                return idzak;
            }

            var tisEntityId = GetTisEntityIdByTimeline(timelineId);
            long parsed;
            if (long.TryParse((tisEntityId ?? string.Empty).Trim(), out parsed) && parsed > 0)
            {
                LogOk("idzak не найден напрямую, использован fallback через tis_entity_id=" + parsed + ".");
                return parsed;
            }

            return 0;
        }

        /// <summary>
        /// Гарантирует наличие T1 XML в epd_doc_store перед сборкой T2.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline, для которого формируется T2.</param>
        /// <remarks>
        /// BuildTitul2Xml читает T1 из БД (payload_xml). Если запись отсутствует, метод пытается
        /// восстановить T1 из серверной папки и записать ее в epd_doc_store через штатный UpsertDoc.
        /// </remarks>
                private void EnsureT1XmlInDocStoreForT2(long timelineId)
        {
            var existingXml = EpdRepo.GetXmlText(timelineId);
            var seedBytes = ResolveT1XmlSeedBytes(timelineId);

            if (seedBytes == null || seedBytes.Length == 0)
            {
                if (!string.IsNullOrEmpty(existingXml))
                {
                    return;
                }

                throw new ApplicationException(
                    "В БД отсутствует T1 XML для сборки T2, и не найдено серверных T1 XML для инициализации. " +
                    "Сначала сформируйте T1 XML и обновите списки файлов.");
            }

            // Для корректной сборки T2 используем актуальный T1 XML из серверного источника,
            // чтобы ссылка ИдФайлИнфГО соответствовала последней отправке T1.
            EpdRepo.UpsertDoc(timelineId, seedBytes, null, null);

            if (string.IsNullOrEmpty(existingXml))
            {
                LogOk("T1 XML инициализирован в epd_doc_store для сборки T2.");
            }
            else
            {
                LogOk("T1 XML синхронизирован в epd_doc_store перед сборкой T2.");
            }
        }
        /// <summary>
        /// Гарантирует наличие uid_zak в epd_timeline перед сборкой T2.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline, для которого формируется T2.</param>
        /// <remarks>
        /// BuildTitul2Xml требует uid_zak. Если его нет в timeline и нет события epd.uuid.defined,
        /// используем TransportationId из таблицы операторских ссылок Kontur как источник УИД.
        /// </remarks>
                private void EnsureUidZakForT2(long timelineId)
        {
            var refRepository = new KonturOperatorRefRepository(Connection.conStr());
            var transportationId = (refRepository.GetLatestRefValue(timelineId, "Kontur", "TransportationId") ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(transportationId))
            {
                throw new ApplicationException(
                    "Для сборки T2 отсутствует uid_zak (epd.uuid.defined), и не найден TransportationId в TEpdOperatorRef. " +
                    "Сначала выполните T1_INITIAL успешно.");
            }

            var existingUid = (EpdRepo.GetUidZak(timelineId) ?? string.Empty).Trim();
            if (string.Equals(existingUid, transportationId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Для T2 критично использовать идентификатор последнего успешного T1.
            // Если uid_zak устарел после повторной отправки T1, принудительно синхронизируем его.
            EpdRepo.SaveUidZak(timelineId, transportationId, "uid_zak сохранён из Kontur TransportationId");
            if (string.IsNullOrEmpty(existingUid))
            {
                LogOk("uid_zak восстановлен из TransportationId для сборки T2.");
            }
            else
            {
                LogOk("uid_zak обновлён до последнего TransportationId для сборки T2.");
            }
        }
        /// <summary>
        /// Формирует XML указанного титула через внутренний builder и сохраняет файл в серверную папку.
        /// </summary>
        /// <param name="titleCode">Код титула T3 или T4.</param>
        /// <remarks>
        /// Метод не отправляет документ оператору, а только подготавливает XML для визуального контроля
        /// и ручных диагностических сценариев в пределах рабочего окружения.
        /// </remarks>
        private void BuildStoredStageXml(string titleCode)
        {
            try
            {
                var timelineId = ParseTimelineId(tbTimelineId.Text.Trim());
                var buildResult = CreateBuildStageTitleUseCase().Execute(timelineId, titleCode, string.Empty);
                var artifact = buildResult == null ? null : buildResult.Artifact;
                var stateSource = "ReconstructionUseCase";

                if (buildResult == null || !buildResult.IsSuccess || artifact == null || artifact.TitleXml == null || artifact.TitleXml.Length == 0)
                {
                    stateSource = "LegacyFallback";
                    var runner = new KonturStageRunner(Connection.conStr());
                    buildResult = runner.TitleBuilder.Build(timelineId, titleCode, string.Empty);
                    artifact = buildResult.Artifact;

                    if (!buildResult.IsSuccess || artifact == null || artifact.TitleXml == null || artifact.TitleXml.Length == 0)
                    {
                        // Если builder не собрал T3/T4, пробуем восстановить артефакт из серверного XML.
                        artifact = TryRestoreTitleArtifactFromSelectedXml(timelineId, titleCode);
                    }

                    if (artifact == null || artifact.TitleXml == null || artifact.TitleXml.Length == 0)
                    {
                        // Если артефакт отсутствует, пробуем прямую сборку T3/T4 через актуальный builder ETRNtituls.
                        artifact = TryBuildStageXmlDirect(timelineId, titleCode);
                    }
                }

                if (artifact == null || artifact.TitleXml == null || artifact.TitleXml.Length == 0)
                {
                    var builderMessage = buildResult == null ? string.Empty : (buildResult.Message ?? string.Empty);

                    throw new ApplicationException(
                        "No XML artifact for " + titleCode + "." + Environment.NewLine +
                        "builderMessage=" + builderMessage + Environment.NewLine +
                        "reason=Stage XML is generated by internal builder/artifact flow, not by pre-existing server file." + Environment.NewLine +
                        "action=Run the previous stage successfully and ensure title artifact is saved before generating " + titleCode + ".");
                }

                var fullPath = SaveCurrentStageXmlArtifact(timelineId, titleCode, artifact.TitleXml);
                var fileName = Path.GetFileName(fullPath);

                BindServerFileSelectors();
                ddlServerXmlFile.SelectedValue = fileName;
                ddlStage.SelectedValue = titleCode;

                LogOk(
                    titleCode + " XML formed and saved." + Environment.NewLine +
                    "timelineId=" + timelineId + Environment.NewLine +
                    "xmlFile=" + fileName + Environment.NewLine +
                    "stateSource=" + stateSource);
            }
            catch (Exception ex)
            {
                LogErr("Stage XML generation error: " + titleCode, ex);
            }
        }

        /// <summary>
        /// Восстанавливает артефакт T3/T4 из серверного XML-файла.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline.</param>
        /// <param name="titleCode">Код титула T3 или T4.</param>
        /// <returns>Сохраненный артефакт или null, если восстановление невозможно.</returns>
        /// <remarks>
        /// Используется только выбранный файл из селектора, если он соответствует T3/T4.
        /// Автопоиск по маске не применяется, чтобы не создавать ложное ожидание готовых файлов до генерации.
        /// </remarks>
        private KonturTitleArtifact TryRestoreTitleArtifactFromSelectedXml(long timelineId, string titleCode)
        {
            var root = GetKonturServerFilesDirectory();
            Directory.CreateDirectory(root);

            var selectedPath = ResolveServerFilePath(root, ddlServerXmlFile);
            var resolvedPath = ResolveStageXmlPathForRestore(root, selectedPath, timelineId, titleCode);
            if (string.IsNullOrEmpty(resolvedPath) || !File.Exists(resolvedPath))
            {
                return null;
            }

            var fileName = Path.GetFileName(resolvedPath) ?? string.Empty;
            var xmlBytes = File.ReadAllBytes(resolvedPath);
            if (xmlBytes == null || xmlBytes.Length == 0)
            {
                return null;
            }

            var artifact = new KonturTitleArtifact
            {
                TimelineId = timelineId,
                TitleCode = titleCode,
                XmlFileName = fileName,
                TitleXml = xmlBytes,
                SignatureFileName = null,
                TitleSgn = null,
                Thumbprint = null,
                SignerRole = null,
                SignedAt = null
            };

            var repository = CreateTitleArtifactRepository();
            repository.SaveDraftArtifact(artifact);
            return repository.GetLatest(timelineId, titleCode);
        }

        /// <summary>
        /// Подбирает серверный XML для восстановления артефакта T3/T4.
        /// </summary>
        /// <param name="root">Папка серверных файлов.</param>
        /// <param name="selectedPath">Путь выбранного файла из селектора.</param>
        /// <param name="timelineId">Идентификатор timeline.</param>
        /// <param name="titleCode">Код титула T3 или T4.</param>
        /// <returns>Абсолютный путь подходящего XML или null.</returns>
        private string ResolveStageXmlPathForRestore(string root, string selectedPath, long timelineId, string titleCode)
        {
            var expectedPrefix = titleCode.Equals("T3", StringComparison.OrdinalIgnoreCase) ? "t3_" : "t4_";

            if (!string.IsNullOrEmpty(selectedPath) && File.Exists(selectedPath))
            {
                var selectedName = (Path.GetFileName(selectedPath) ?? string.Empty).ToLowerInvariant();
                if (selectedName.StartsWith(expectedPrefix))
                {
                    return selectedPath;
                }
            }

            return null;
        }


        /// <summary>
        /// Пробует собрать XML T3/T4 напрямую через ETRNtituls и подготовить артефакт для дальнейшего запуска этапа.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline.</param>
        /// <param name="titleCode">Код титула T3 или T4.</param>
        /// <returns>Артефакт XML или null, если прямую сборку выполнить нельзя.</returns>
        /// <remarks>
        /// Нужен как переходный механизм, пока T3/T4 не выделены в отдельный внутрений KonturTitleBuilder.
        /// </remarks>
        private KonturTitleArtifact TryBuildStageXmlDirect(long timelineId, string titleCode)
        {
            try
            {
                var normalizedTitle = (titleCode ?? string.Empty).Trim().ToUpperInvariant();
                if (normalizedTitle != "T3" && normalizedTitle != "T4")
                {
                    return null;
                }

                var idzak = ResolveIdzakForT2(timelineId);
                if (idzak <= 0)
                {
                    return null;
                }

                var uidTrN = (EpdRepo.GetUidZak(timelineId) ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(uidTrN))
                {
                    return null;
                }

                // В текущем snapshot прямые builder-методы T3/T4 отсутствуют.
                // До возврата совместимых legacy-методов используем только сохраненные артефакты.
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Подменяет ИдФайл в готовом XML T2 на согласованный контурный формат.
        /// </summary>
        /// <param name="xmlText">Исходный XML титула T2.</param>
        /// <param name="prebuiltIdFile">Готовый ИдФайл, выведенный из принятого T1.</param>
        /// <returns>XML с обновленным ИдФайл или исходный XML, если замена невозможна.</returns>
        /// <remarks>
        /// В snapshot-ветке BuildTitul2Xml не принимает внешний ИдФайл и не гарантирует корректную ссылку на T1,
        /// поэтому после сборки синхронизируем ИдФайл T2, ссылку ИдИнфГО и detached-подпись T1.
        /// </remarks>
        private string ApplyKonturT2ReferenceOverride(string xmlText, string prebuiltIdFile, T1InfoForKonturT2 t1Info, string t1SignatureBase64)
        {
            if (string.IsNullOrEmpty(xmlText) || string.IsNullOrEmpty(prebuiltIdFile))
            {
                return xmlText;
            }

            try
            {
                var document = XDocument.Parse(xmlText);
                var root = document.Root;
                if (root == null)
                {
                    return xmlText;
                }

                var idFileAttr = root.Attribute("ИдФайл");
                if (idFileAttr == null)
                {
                    return xmlText;
                }

                idFileAttr.Value = prebuiltIdFile;
                ApplyKonturT2SourceTitleReference(root, t1Info, t1SignatureBase64);
                return document.Declaration == null
                    ? document.ToString()
                    : document.Declaration + Environment.NewLine + document.ToString();
            }
            catch
            {
                return xmlText;
            }
        }

        /// <summary>
        /// Синхронизирует в T2 ссылку на исходный T1 и его detached-подпись.
        /// </summary>
        /// <param name="root">Корневой XML-узел T2.</param>
        /// <param name="t1Info">Минимальные сведения об уже принятом титуле T1.</param>
        /// <param name="t1SignatureBase64">Detached-подпись T1 в base64-виде.</param>
        /// <remarks>
        /// Этот шаг нужен, потому что BuildTitul2Xml в snapshot-ветке может оставить техническую заглушку ЭП="0"
        /// или устаревшую ссылку на T1. Контур затем получает формально валидный XML, но без реальной привязки к
        /// подписанному T1, что приводит к неявным ошибкам оператора вида HTTP 500.
        /// </remarks>
        private void ApplyKonturT2SourceTitleReference(XElement root, T1InfoForKonturT2 t1Info, string t1SignatureBase64)
        {
            if (root == null || t1Info == null)
            {
                return;
            }

            var sourceInfoNode = root.Descendants("ИдИнфГО").FirstOrDefault();
            if (sourceInfoNode == null)
            {
                return;
            }

            SetAttributeValue(sourceInfoNode, "ИдФайлИнфГО", t1Info.IdFile);
            SetAttributeValue(sourceInfoNode, "ДатФайлИнфГО", t1Info.DocDate);
            SetAttributeValue(sourceInfoNode, "ВрФайлИнфГО", t1Info.DocTime);

            if (!string.IsNullOrEmpty(t1SignatureBase64))
            {
                SetAttributeValue(sourceInfoNode, "ЭП", t1SignatureBase64);
            }
        }

        /// <summary>
        /// Безопасно обновляет атрибут XML-узла только при наличии содержательного значения.
        /// </summary>
        /// <param name="element">Узел, в котором нужно обновить атрибут.</param>
        /// <param name="attributeName">Имя атрибута.</param>
        /// <param name="value">Новое значение атрибута.</param>
        /// <remarks>
        /// Локальный helper нужен, чтобы не затирать builder-значения пустой строкой, если в одном из источников
        /// отсутствуют дата, время или подпись исходного T1.
        /// </remarks>
        private void SetAttributeValue(XElement element, string attributeName, string value)
        {
            if (element == null || string.IsNullOrEmpty(attributeName) || string.IsNullOrEmpty(value))
            {
                return;
            }

            var attribute = element.Attribute(attributeName);
            if (attribute != null)
            {
                attribute.Value = value;
            }
        }

        /// <summary>
        /// Инициализирует скрытый серверный код этапа, если он еще не установлен.
        /// </summary>
        /// <remarks>Метод нужен, чтобы клиентские вкладки и серверные postback работали от одного значения stageCode.</remarks>
        private void EnsureHiddenStageIsInitialized()
        {
            if (ddlStage.Items.Count == 0)
            {
                return;
            }

            if (string.IsNullOrEmpty((ddlStage.SelectedValue ?? string.Empty).Trim()))
            {
                ddlStage.SelectedValue = "T1_INITIAL";
            }
        }

        /// <summary>
        /// Возвращает текущий код вкладки этапа в форме T1/T2/T3/T4.
        /// </summary>
        /// <returns>Нормализованный код вкладки этапа.</returns>
        protected string GetCurrentStageTabCode()
        {
            return StageToTitle((ddlStage.SelectedValue ?? string.Empty).Trim());
        }

        /// <summary>
        /// Возвращает CSS-класс состояния шага формирования XML.
        /// </summary>
        /// <returns>CSS-класс для текущего состояния первого шага.</returns>
        protected string GetCurrentXmlStepCssClass()
        {
            return GetCurrentStageFlowState().XmlStep.CssClass;
        }

        /// <summary>
        /// Возвращает текст состояния шага формирования XML.
        /// </summary>
        /// <returns>Короткий текст для первого шага.</returns>
        protected string GetCurrentXmlStepText()
        {
            return GetCurrentStageFlowState().XmlStep.Text;
        }

        /// <summary>
        /// Возвращает CSS-класс состояния шага подписи.
        /// </summary>
        /// <returns>CSS-класс для второго шага.</returns>
        protected string GetCurrentSignatureStepCssClass()
        {
            return GetCurrentStageFlowState().SignatureStep.CssClass;
        }

        /// <summary>
        /// Возвращает текст состояния шага подписи.
        /// </summary>
        /// <returns>Короткий текст для второго шага.</returns>
        protected string GetCurrentSignatureStepText()
        {
            return GetCurrentStageFlowState().SignatureStep.Text;
        }

        /// <summary>
        /// Возвращает CSS-класс состояния шага отправки.
        /// </summary>
        /// <returns>CSS-класс для третьего шага.</returns>
        protected string GetCurrentSendStepCssClass()
        {
            return GetCurrentStageFlowState().SendStep.CssClass;
        }

        /// <summary>
        /// Возвращает текст состояния шага отправки.
        /// </summary>
        /// <returns>Короткий текст для третьего шага.</returns>
        protected string GetCurrentSendStepText()
        {
            return GetCurrentStageFlowState().SendStep.Text;
        }

        /// <summary>
        /// Возвращает CSS-класс состояния шага результата.
        /// </summary>
        /// <returns>CSS-класс для четвертого шага.</returns>
        protected string GetCurrentResultStepCssClass()
        {
            return GetCurrentStageFlowState().ResultStep.CssClass;
        }

        /// <summary>
        /// Возвращает текст состояния шага результата.
        /// </summary>
        /// <returns>Короткий текст для четвертого шага.</returns>
        protected string GetCurrentResultStepText()
        {
            return GetCurrentStageFlowState().ResultStep.Text;
        }

        /// <summary>
        /// Возвращает компактное итоговое состояние текущего этапа.
        /// </summary>
        /// <returns>Текст для блока результата в рабочей последовательности.</returns>
        protected string GetCurrentResultSummaryText()
        {
            var screenModel = GetCurrentStageScreenModel();
            if (screenModel != null && !string.IsNullOrEmpty(screenModel.ResultSummary))
            {
                return screenModel.ResultSummary;
            }

            return GetCurrentStageFlowState().ResultSummary;
        }

        /// <summary>
        /// Возвращает текст источника состояния текущего этапа.
        /// </summary>
        /// <returns>Текст источника состояния для операторной диагностики.</returns>
        /// <remarks>
        /// Подсказка нужна, чтобы оператор видел, построено ли summary по явной модели состояния
        /// реконструкционного слоя или страница все еще опирается на fallback read-model.
        /// </remarks>
        protected string GetCurrentStateSourceText()
        {
            var screenModel = GetCurrentStageScreenModel();
            if (screenModel == null)
            {
                return "Источник состояния: недоступен";
            }

            if (string.Equals(screenModel.StateSource, "ExplicitStageState", StringComparison.OrdinalIgnoreCase))
            {
                return "Источник состояния: явная модель этапа";
            }

            if (string.Equals(screenModel.StateSource, "FallbackReadModel", StringComparison.OrdinalIgnoreCase))
            {
                return "Источник состояния: fallback read-model";
            }

            return "Источник состояния: " + Safe(screenModel.StateSource);
        }

        /// <summary>
        /// Собирает текущее состояние шагов рабочего потока для выбранного этапа.
        /// </summary>
        /// <returns>Агрегированное состояние четырех шагов этапа.</returns>
        /// <remarks>
        /// Метод работает как тонкая прослойка между UI и текущими артефактами этапа,
        /// чтобы разметка показывала оператору фактическое состояние без ручного анализа файлов и логов.
        /// </remarks>
        private KonturProbeStageFlowState GetCurrentStageFlowState()
        {
            if (_currentStageFlowState != null)
            {
                return _currentStageFlowState;
            }

            try
            {
                long timelineId;
                if (!long.TryParse((tbTimelineId.Text ?? string.Empty).Trim(), out timelineId) || timelineId <= 0)
                {
                    _currentStageFlowState = new KonturProbeStageFlowState
                    {
                        XmlStep = new KonturProbeFlowStepState { CssClass = "step-state step-state-warn", Text = "Ожидает TimelineId" },
                        SignatureStep = new KonturProbeFlowStepState { CssClass = "step-state step-state-warn", Text = "Ожидает TimelineId" },
                        SendStep = new KonturProbeFlowStepState { CssClass = "step-state step-state-warn", Text = "Ожидает TimelineId" },
                        ResultStep = new KonturProbeFlowStepState { CssClass = "step-state step-state-warn", Text = "Результат отсутствует" },
                        ResultSummary = "Укажите корректный TimelineId, чтобы загрузить шаги этапа."
                    };
                    return _currentStageFlowState;
                }

                string xmlPath;
                string signaturePath;
                ResolveServerSelectedPaths(out xmlPath, out signaturePath);
                _currentStageFlowState = CreateProbeStageFlowService().BuildState(
                    timelineId,
                    (ddlStage.SelectedValue ?? string.Empty).Trim(),
                    xmlPath,
                    signaturePath);
                return _currentStageFlowState;
            }
            catch (Exception ex)
            {
                _currentStageFlowState = new KonturProbeStageFlowState
                {
                    XmlStep = new KonturProbeFlowStepState { CssClass = "step-state step-state-error", Text = "Ошибка чтения" },
                    SignatureStep = new KonturProbeFlowStepState { CssClass = "step-state step-state-error", Text = "Ошибка чтения" },
                    SendStep = new KonturProbeFlowStepState { CssClass = "step-state step-state-error", Text = "Ошибка чтения" },
                    ResultStep = new KonturProbeFlowStepState { CssClass = "step-state step-state-error", Text = "Требует проверки" },
                    ResultSummary = Safe(ex.Message)
                };
                return _currentStageFlowState;
            }
        }

        /// <summary>
        /// Собирает экранную read-model текущего этапа поверх fallback-состояния.
        /// </summary>
        /// <returns>Экранная модель этапа для summary и диагностических подсказок.</returns>
        /// <remarks>
        /// Метод не влияет на runtime-отправку и используется только для безопасного чтения нового
        /// явного состояния этапа через KonturStageScreenService рядом с текущим fallback-контуром.
        /// </remarks>
        private KonturStageScreenModel GetCurrentStageScreenModel()
        {
            if (_currentStageScreenModel != null)
            {
                return _currentStageScreenModel;
            }

            try
            {
                long timelineId;
                if (!long.TryParse((tbTimelineId.Text ?? string.Empty).Trim(), out timelineId) || timelineId <= 0)
                {
                    return null;
                }

                string xmlPath;
                string signaturePath;
                ResolveServerSelectedPaths(out xmlPath, out signaturePath);
                _currentStageScreenModel = CreateStageScreenService().Build(
                    timelineId,
                    (ddlStage.SelectedValue ?? string.Empty).Trim(),
                    xmlPath,
                    signaturePath);
                return _currentStageScreenModel;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Валидирует последовательность шагов перед отправкой этапа в Контур.
        /// </summary>
        /// <param name="stageCode">Код этапа UI.</param>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="xmlPath">Путь к выбранному XML-файлу.</param>
        /// <param name="signaturePath">Путь к выбранному .sgn-файлу.</param>
        /// <remarks>
        /// Метод не блокирует исследовательские действия на странице, но запрещает сетевую отправку,
        /// если обязательные шаги "сформировать" и "подписать" еще не завершены.
        /// </remarks>
        private void ValidateStageSequenceBeforeSend(string stageCode, long timelineId, string xmlPath, string signaturePath)
        {
            var state = CreateProbeStageFlowService().BuildState(timelineId, stageCode, xmlPath, signaturePath);
            if (!string.Equals(state.XmlStep.CssClass, "step-state step-state-ready", StringComparison.OrdinalIgnoreCase))
            {
                throw new ApplicationException(
                    "Нельзя отправить этап " + stageCode + ": шаг 1 \"Сформировать XML\" еще не завершен." + Environment.NewLine +
                    "action=Сначала сформируйте актуальный XML текущего этапа.");
            }

            if (!string.Equals(state.SignatureStep.CssClass, "step-state step-state-ready", StringComparison.OrdinalIgnoreCase))
            {
                throw new ApplicationException(
                    "Нельзя отправить этап " + stageCode + ": шаг 2 \"Подписать\" еще не завершен." + Environment.NewLine +
                    "action=Подготовьте подпись через SignEpd или импортируйте актуальный .sgn для текущего этапа.");
            }

            EnsureT2SignatureMatchesSelectedXml(stageCode, timelineId, xmlPath, signaturePath);

        }

        /// <summary>
        /// Выполняет отправку этапа через предпочтительный runtime-путь для текущего состояния реконструкции.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код текущего этапа UI.</param>
        /// <param name="xmlPath">Путь к выбранному XML-файлу.</param>
        /// <param name="signaturePath">Путь к выбранной подписи.</param>
        /// <param name="executionPath">Имя фактически использованного runtime-пути.</param>
        /// <returns>Результат отправки этапа.</returns>
        /// <remarks>
        /// Для T2/T3/T4 при наличии явного состояния текущего этапа страница использует reconstruction send use case.
        /// Во всех остальных случаях сохраняется текущий runtime-путь через KonturEtrnStageService.
        /// </remarks>
        private KonturStageExecutionResult ExecuteStageThroughPreferredRuntime(long timelineId, string stageCode, string xmlPath, string signaturePath, out string executionPath)
        {
            if (CanUseReconstructionRecipientSendUseCase(timelineId, stageCode))
            {
                executionPath = "ReconstructionSendUseCase";
                return ExecuteRecipientStageThroughReconstructionUseCase(timelineId, stageCode);
            }

            executionPath = "LegacyRuntime";
            var service = new KonturEtrnStageService(Connection.conStr());
            var result = service.Execute(stageCode, timelineId, xmlPath, signaturePath);
            if (result != null && result.IsSuccess)
            {
                BridgeLegacySentStageStateAfterSuccessfulRun(timelineId, stageCode, xmlPath, signaturePath, result.Message);
            }

            return result;
        }

        /// <summary>
        /// Определяет, можно ли запускать send use case реконструкционного слоя для ответного титула.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код текущего этапа UI.</param>
        /// <returns>True, если этап T2/T3/T4 уже имеет явное подготовленное состояние; иначе false.</returns>
        /// <remarks>
        /// Проверка ограничивает новый send-path только теми этапами, где reconstruction слой уже владеет
        /// XML/SGN-подготовкой. Это исключает внезапное переключение T1 и неподготовленных legacy-прогонов.
        /// </remarks>
        private bool CanUseReconstructionRecipientSendUseCase(long timelineId, string stageCode)
        {
            var titleCode = StageToTitle(stageCode);
            if (!string.Equals(titleCode, "T2", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(titleCode, "T3", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(titleCode, "T4", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var state = CreateStageStateRepository().Get(timelineId, stageCode);
            return state != null && state.XmlBuilt && state.SignatureImported;
        }

        /// <summary>
        /// Отправляет ответный титул через reconstruction send use case.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код этапа T2/T3/T4.</param>
        /// <returns>Результат отправки этапа через новый слой.</returns>
        /// <remarks>
        /// Выбор конкретного use case локализован здесь, чтобы основной обработчик UI не знал
        /// о деталях отдельных сценариев T2, T3 и T4.
        /// </remarks>
        private KonturStageExecutionResult ExecuteRecipientStageThroughReconstructionUseCase(long timelineId, string stageCode)
        {
            var senderBoxId = ResolveSenderBoxIdForStage(stageCode);

            if (string.Equals(stageCode, "T2", StringComparison.OrdinalIgnoreCase))
            {
                return CreateSendT2RecipientTitleUseCase().Execute(timelineId, senderBoxId);
            }

            if (string.Equals(stageCode, "T3", StringComparison.OrdinalIgnoreCase))
            {
                return CreateSendT3RecipientTitleUseCase().Execute(timelineId, senderBoxId);
            }

            if (string.Equals(stageCode, "T4", StringComparison.OrdinalIgnoreCase))
            {
                return CreateSendT4RecipientTitleUseCase().Execute(timelineId, senderBoxId);
            }

            return new KonturStageExecutionResult
            {
                IsSuccess = false,
                StageCode = stageCode,
                TimelineId = timelineId,
                Message = "UnsupportedRecipientStage"
            };
        }

        /// <summary>
        /// Для этапа T2 проверяет, что подпись действительно соответствует текущему XML.
        /// </summary>
        /// <param name="stageCode">Код этапа UI.</param>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="xmlPath">Путь к XML этапа T2.</param>
        /// <param name="signaturePath">Путь к подписи этапа T2 (может быть пустым).</param>
        /// <remarks>
        /// Проверка выполняется до сетевого вызова, чтобы не получать "SignatureVerifyFailed" из глубины адаптера.
        /// Если файл подписи не выбран, используется sig2_detached из epd_doc_store.
        /// </remarks>
        private void EnsureT2SignatureMatchesSelectedXml(string stageCode, long timelineId, string xmlPath, string signaturePath)
        {
            if (!string.Equals(StageToTitle(stageCode), "T2", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var artifact = GetLatestStageArtifact(timelineId, "T2");
            if (artifact != null && artifact.HasXml && artifact.HasSignature)
            {
                string artifactVerifyInfo;
                if (!EpdRepo.VerifyDetachedCms(artifact.TitleXml, artifact.TitleSgn, out artifactVerifyInfo))
                {
                    throw new ApplicationException(
                        "Подпись T2 не соответствует выбранному XML (локальная проверка CMS)." + Environment.NewLine +
                        "action=После каждой новой генерации T2 подпишите именно текущий t2_timeline...xml и повторно импортируйте .sgn.");
                }

                return;
            }

            if (string.IsNullOrEmpty(xmlPath) || !File.Exists(xmlPath))
            {
                return;
            }

            byte[] xmlBytes = File.ReadAllBytes(xmlPath);
            byte[] signatureBytes = null;
            if (!string.IsNullOrEmpty(signaturePath) && File.Exists(signaturePath))
            {
                signatureBytes = File.ReadAllBytes(signaturePath);
            }
            else
            {
                signatureBytes = EpdRepo.GetSig2Bytes(timelineId);
            }

            if (signatureBytes == null || signatureBytes.Length == 0)
            {
                return;
            }

            string verifyInfo;
            if (!EpdRepo.VerifyDetachedCms(xmlBytes, signatureBytes, out verifyInfo))
            {
                throw new ApplicationException(
                    "Подпись T2 не соответствует выбранному XML (локальная проверка CMS)." + Environment.NewLine +
                    "action=После каждой новой генерации T2 подпишите именно текущий t2_timeline...xml и повторно импортируйте .sgn.");
            }
        }

        /// <summary>
        /// Определяет, можно ли запускать этап по внутреннему SQL-артефакту без опоры на выбранные файлы.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код этапа UI.</param>
        /// <returns>True, если у этапа уже есть пригодный артефакт для отправки.</returns>
        /// <remarks>
        /// Для T1 достаточно актуального XML-артефакта. Для T2 требуется пара XML+SGN в одном артефакте,
        /// чтобы не читать XML и подпись из разных источников.
        /// </remarks>
        private bool ShouldUseArtifactExecution(long timelineId, string stageCode)
        {
            var titleCode = StageToTitle(stageCode);
            if (string.Equals(titleCode, "T1", StringComparison.OrdinalIgnoreCase))
            {
                var t1Artifact = GetLatestStageArtifact(timelineId, "T1");
                return t1Artifact != null && t1Artifact.HasXml;
            }

            if (string.Equals(titleCode, "T2", StringComparison.OrdinalIgnoreCase))
            {
                var t2Artifact = GetLatestStageArtifact(timelineId, "T2");
                return t2Artifact != null && t2Artifact.HasXml && t2Artifact.HasSignature;
            }

            return false;
        }

        /// <summary>
        /// Возвращает последний SQL-артефакт выбранного этапа.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="titleCode">Код титула T1/T2/T3/T4.</param>
        /// <returns>Артефакт этапа или null.</returns>
        /// <remarks>
        /// Метод централизует чтение TEpdTitleArtifact, чтобы страница не дублировала логику репозитория в разных проверках.
        /// </remarks>
        private KonturTitleArtifact GetLatestStageArtifact(long timelineId, string titleCode)
        {
            return CreateTitleArtifactRepository().GetLatest(timelineId, titleCode);
        }

        /// <summary>
        /// Сбрасывает подпись T2 в epd_doc_store после пересборки XML титула T2.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <remarks>
        /// После смены XML старая sig2_detached становится невалидной по хешу.
        /// Сброс исключает ложный статус "Подпись готова" и принуждает к повторной подписи актуального XML.
        /// </remarks>
        private void InvalidateT2SignatureAfterXmlRebuild(long timelineId)
        {
            using (var connection = new SqlConnection(Connection.conStr()))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
UPDATE d
   SET sig2_detached = NULL,
       sig2_hash = NULL,
       sig2_stamp_text = NULL,
       sig2_signed_at = NULL,
       sig2_idMchd = 0,
       idUser2 = 0,
       updated_at = GETDATE()
FROM Perdoc.dbo.epd_doc_store d
WHERE d.id = (
    SELECT TOP (1) id
    FROM Perdoc.dbo.epd_doc_store
    WHERE timeline_id = @timelineId
    ORDER BY version_no DESC, id DESC
);";
                command.Parameters.AddWithValue("@timelineId", timelineId);
                connection.Open();
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Сбрасывает подпись T1 в epd_doc_store после пересборки XML титула T1.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <remarks>
        /// После смены XML старая sig_detached становится невалидной по хешу.
        /// Сброс исключает ложный статус "Подпись готова" и заставляет повторно подписать именно новый T1 XML.
        /// </remarks>
        private void InvalidateT1SignatureAfterXmlRebuild(long timelineId)
        {
            using (var connection = new SqlConnection(Connection.conStr()))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
UPDATE d
   SET sig_detached = NULL,
       sig_hash = NULL,
       sig_stamp_text = NULL,
       sig_signed_at = NULL,
       idMchd = 0,
       idUser = 0,
       updated_at = GETDATE()
FROM Perdoc.dbo.epd_doc_store d
WHERE d.id = (
    SELECT TOP (1) id
    FROM Perdoc.dbo.epd_doc_store
    WHERE timeline_id = @timelineId
    ORDER BY version_no DESC, id DESC
);";
                command.Parameters.AddWithValue("@timelineId", timelineId);
                connection.Open();
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Сбрасывает кэш статусного состояния текущего этапа.
        /// </summary>
        /// <remarks>
        /// Сброс нужен перед любым действием, которое меняет XML, подпись, выбор этапа или выбранный TimelineId,
        /// чтобы следующий рендер не использовал устаревшее состояние шага.
        /// </remarks>
        private void ResetCurrentStageFlowState()
        {
            _currentStageFlowState = null;
            _currentStageScreenModel = null;
        }

        /// <summary>
        /// Создает сервис расчета статусного состояния этапа для экрана KonturProbe.
        /// </summary>
        /// <returns>Экземпляр KonturProbeStageFlowService с текущими зависимостями страницы.</returns>
        /// <remarks>
        /// Сервис изолирует файловую и SQL-диагностику этапа от WebForms-страницы,
        /// чтобы code-behind оставался координатором UI, а не местом хранения подробной логики анализа артефактов.
        /// </remarks>
        private KonturProbeStageFlowService CreateProbeStageFlowService()
        {
            return new KonturProbeStageFlowService(Connection.conStr(), GetKonturServerFilesDirectory());
        }

        /// <summary>
        /// Создает сервис экранной read-model этапа.
        /// </summary>
        /// <returns>Сервис построения экранной модели поверх явного состояния и fallback read-model.</returns>
        /// <remarks>
        /// Сервис подключается только для чтения summary и источника состояния,
        /// чтобы безопасно вводить reconstruction screen-layer без смены send runtime.
        /// </remarks>
        private KonturStageScreenService CreateStageScreenService()
        {
            return new KonturStageScreenService(CreateStageStateRepository(), CreateProbeStageFlowService());
        }

        /// <summary>
        /// Создает сценарий операторского подтверждения завершения этапа.
        /// </summary>
        /// <returns>Use case подтверждения завершения этапа.</returns>
        /// <remarks>
        /// Сценарий выделен отдельно, чтобы Completed и NextStageAllowed выставлялись
        /// через прикладную границу, а не прямой записью в code-behind.
        /// </remarks>
        private ConfirmStageCompletionUseCase CreateConfirmStageCompletionUseCase()
        {
            return new ConfirmStageCompletionUseCase(CreateStageStateRepository());
        }

        /// <summary>
        /// Создает repository legacy refs оператора Контур.
        /// </summary>
        /// <returns>Repository чтения и записи operator refs.</returns>
        /// <remarks>
        /// Репозиторий собирается в composition-root страницы, чтобы bridge-слой не тянул SQL-реализацию
        /// прямо из отдельных UI-обработчиков.
        /// </remarks>
        private IKonturOperatorRefRepository CreateOperatorRefRepository()
        {
            return new KonturOperatorRefRepository(Connection.conStr());
        }

        /// <summary>
        /// Создает сценарий синхронизации refs в явное состояние этапа.
        /// </summary>
        /// <returns>Use case синхронизации TransportationId и TitleId.</returns>
        /// <remarks>
        /// Сценарий используется как безопасный мост между legacy runtime и reconstruction state,
        /// чтобы completion-путь T1 не зависел от ручной SQL-синхронизации refs.
        /// </remarks>
        private SyncStageStateRefsUseCase CreateSyncStageStateRefsUseCase()
        {
            return new SyncStageStateRefsUseCase(
                CreateStageStateRepository(),
                CreateOperatorRefRepository());
        }

        /// <summary>
        /// Создает policy проверки переходов этапов реконструкционного слоя.
        /// </summary>
        /// <returns>Policy проверки готовности этапов.</returns>
        /// <remarks>Policy используется send use case-ами и не должна собираться вручную в нескольких местах страницы.</remarks>
        private KonturStageTransitionPolicy CreateStageTransitionPolicy()
        {
            return new KonturStageTransitionPolicy(CreateStageStateRepository());
        }

        /// <summary>
        /// Создает gateway отправки ответного титула через текущий KonturAdapter.
        /// </summary>
        /// <param name="titleCode">Код титула T2, T3 или T4.</param>
        /// <returns>Gateway отправки T2/T3/T4.</returns>
        /// <remarks>
        /// Gateway создается через ролевой контекст конкретного титула, потому что KonturAdapter
        /// принимает готовый KonturClient, а не строку подключения к базе.
        /// </remarks>
        private IKonturRecipientTitleGateway CreateRecipientTitleGateway(string titleCode)
        {
            return new KonturAdapterRecipientTitleGateway(CreateKonturApiAdapter(titleCode));
        }

        /// <summary>
        /// Создает API-адаптер Контур для конкретного титула.
        /// </summary>
        /// <param name="titleCode">Код титула T1, T2, T3 или T4.</param>
        /// <returns>Адаптер Контур с настроенным клиентом и SQL-репозиториями диагностики.</returns>
        /// <remarks>
        /// Метод оставляет страницу composition-root слоем и не переносит бизнес-логику
        /// в code-behind. UI только связывает порты с legacy-адаптером.
        /// </remarks>
        private Tis.KonturIntegration.KonturAdapter.KonturAdapter CreateKonturApiAdapter(string titleCode)
        {
            var access = ResolveKonturAccessForTitle(titleCode);
            var client = new Tis.KonturIntegration.KonturClient.KonturClient(
                access.ApiUrl,
                access.AccessToken,
                access.SolutionInfo);

            return new Tis.KonturIntegration.KonturAdapter.KonturAdapter(client)
            {
                SettingsRepository = new KonturSettingsRepository(Connection.conStr()),
                OperatorRefRepository = new KonturOperatorRefRepository(Connection.conStr()),
                RawLogRepository = new KonturRawLogRepository(Connection.conStr()),
                TimelineRepository = new KonturTimelineRepository(Connection.conStr())
            };
        }

        /// <summary>
        /// Разрешает контекст доступа Контур для конкретного титула.
        /// </summary>
        /// <param name="titleCode">Код титула T1, T2, T3 или T4.</param>
        /// <returns>Готовый контекст доступа к API Контур.</returns>
        /// <remarks>
        /// Резолвинг доступа централизован, чтобы T2/T3/T4 не отправлялись случайно
        /// с токеном или boxId другого участника.
        /// </remarks>
        private KonturAccessContext ResolveKonturAccessForTitle(string titleCode)
        {
            var resolver = new KonturAccessResolver(
                new KonturSettingsRepository(Connection.conStr()),
                new KonturRoleAccessRepository(Connection.conStr()));
            var access = resolver.ResolveByTitle(titleCode);

            if (access == null || !access.IsReady)
            {
                throw new ApplicationException(
                    "Не удалось разрешить доступ Контур для титула." + Environment.NewLine +
                    "title=" + Safe(titleCode) + Environment.NewLine +
                    "role=" + Safe(access == null ? string.Empty : access.SenderRole) + Environment.NewLine +
                    "reason=" + Safe(access == null ? "AccessContextMissing" : access.Message));
            }

            return access;
        }

        /// <summary>
        /// Создает gateway транспортного контекста реконструкционного слоя.
        /// </summary>
        /// <returns>Gateway чтения TransportationId и статуса timeline.</returns>
        /// <remarks>Gateway нужен, чтобы send use case-ы не читали refs и legacy timeline напрямую.</remarks>
        private IKonturTransportContextGateway CreateTransportContextGateway()
        {
            return new KonturTransportContextGateway(
                (KonturOperatorRefRepository)CreateOperatorRefRepository(),
                new KonturTimelineRepository(Connection.conStr()));
        }

        /// <summary>
        /// Создает сценарий отправки этапа T2 через reconstruction слой.
        /// </summary>
        /// <returns>Use case отправки T2.</returns>
        private SendT2RecipientTitleUseCase CreateSendT2RecipientTitleUseCase()
        {
            return new SendT2RecipientTitleUseCase(
                CreateRecipientTitleGateway("T2"),
                CreateStageTransitionPolicy(),
                CreateTransportContextGateway(),
                CreateTitleArtifactRepository(),
                CreateArtifactWorkspaceService(),
                CreateStageStateRepository());
        }

        /// <summary>
        /// Создает сценарий отправки этапа T3 через reconstruction слой.
        /// </summary>
        /// <returns>Use case отправки T3.</returns>
        private SendT3RecipientTitleUseCase CreateSendT3RecipientTitleUseCase()
        {
            return new SendT3RecipientTitleUseCase(
                CreateRecipientTitleGateway("T3"),
                CreateStageTransitionPolicy(),
                CreateTransportContextGateway(),
                CreateTitleArtifactRepository(),
                CreateArtifactWorkspaceService(),
                CreateStageStateRepository());
        }

        /// <summary>
        /// Создает сценарий отправки этапа T4 через reconstruction слой.
        /// </summary>
        /// <returns>Use case отправки T4.</returns>
        private SendT4RecipientTitleUseCase CreateSendT4RecipientTitleUseCase()
        {
            return new SendT4RecipientTitleUseCase(
                CreateRecipientTitleGateway("T4"),
                CreateStageTransitionPolicy(),
                CreateTransportContextGateway(),
                CreateTitleArtifactRepository(),
                CreateArtifactWorkspaceService(),
                CreateStageStateRepository());
        }

        /// <summary>
        /// Создает сервис выбора подписанта этапа для текущего запроса.
        /// </summary>
        /// <returns>Экземпляр KonturStageSignerService с текущей строкой подключения.</returns>
        /// <remarks>Сервис используется как единая точка разрешения подписанта этапа для UI и сборки XML.</remarks>
        private KonturStageSignerService CreateStageSignerService()
        {
            return new KonturStageSignerService(Connection.conStr());
        }

        /// <summary>
        /// Создает сервис чтения состояния тестового режима Kontur-only.
        /// </summary>
        /// <returns>Экземпляр сервиса состояния тестового режима.</returns>
        /// <remarks>
        /// Выделенный сервис нужен, чтобы переключение документа в специальный тестовый контур
        /// не разрасталось в прямые SQL-вызовы из code-behind.
        /// </remarks>
        private KonturTestModeService CreateKonturTestModeService()
        {
            return new KonturTestModeService(Connection.conStr());
        }

        /// <summary>
        /// Создает сервис тестового контекста подписантов Kontur-only.
        /// </summary>
        /// <returns>Экземпляр сервиса тестовых подписантов Контур.</returns>
        /// <remarks>
        /// Сервис используется только для test-only режима и не подменяет штатный источник
        /// подписантов ТИС вне явного включения Kontur-only по TimelineId.
        /// </remarks>
        private KonturTestSigningContextService CreateKonturTestSigningContextService()
        {
            return new KonturTestSigningContextService(Connection.conStr());
        }

        /// <summary>
        /// Проверяет, включен ли для timeline специальный тестовый режим Kontur-only.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <returns>True, если режим включен; иначе false.</returns>
        private bool IsKonturOnlyTestModeEnabled(long timelineId)
        {
            if (timelineId <= 0)
            {
                return false;
            }

            return CreateKonturTestModeService().IsEnabled(timelineId);
        }

        /// <summary>
        /// Возвращает контекст подписантов с учетом тестового режима Kontur-only.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код этапа T1/T2/T3/T4.</param>
        /// <param name="forceRefresh">Признак принудительного обновления кэша.</param>
        /// <returns>Контекст подписантов для текущего сценария.</returns>
        private KonturStageSignerContext GetEffectiveStageSignerContext(long timelineId, string stageCode, bool forceRefresh)
        {
            if (IsKonturOnlyTestModeEnabled(timelineId))
            {
                return GetCachedTestStageSignerContext(timelineId, stageCode, forceRefresh);
            }

            return GetCachedStageSignerContext(timelineId, stageCode, forceRefresh);
        }

        /// <summary>
        /// Возвращает сохраненный выбор подписанта с учетом тестового режима Kontur-only.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код этапа T1/T2/T3/T4.</param>
        /// <returns>Сохраненный выбор подписанта или null.</returns>
        private KonturStageSignerSelection GetEffectiveStageSignerSelection(long timelineId, string stageCode)
        {
            if (IsKonturOnlyTestModeEnabled(timelineId))
            {
                return CreateKonturTestSigningContextService().GetSelection(timelineId, StageToTitle(stageCode));
            }

            return CreateStageSignerService().GetSelection(timelineId, stageCode);
        }

        /// <summary>
        /// Сохраняет выбор подписанта с учетом тестового режима Kontur-only.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код этапа T1/T2/T3/T4.</param>
        /// <param name="signerFizLicoId">Идентификатор выбранного подписанта.</param>
        private void SaveEffectiveStageSignerSelection(long timelineId, string stageCode, long signerFizLicoId)
        {
            if (IsKonturOnlyTestModeEnabled(timelineId))
            {
                CreateKonturTestSigningContextService().SaveSelection(timelineId, StageToTitle(stageCode), signerFizLicoId, GetCurrentUserId());
                return;
            }

            CreateStageSignerService().SaveSelection(timelineId, stageCode, signerFizLicoId, GetCurrentUserId());
        }

        /// <summary>
        /// Удаляет сохраненный выбор подписанта с учетом тестового режима Kontur-only.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код этапа T1/T2/T3/T4.</param>
        private void DeleteEffectiveStageSignerSelection(long timelineId, string stageCode)
        {
            if (IsKonturOnlyTestModeEnabled(timelineId))
            {
                CreateKonturTestSigningContextService().DeleteSelection(timelineId, StageToTitle(stageCode));
                return;
            }

            CreateStageSignerService().DeleteSelection(timelineId, stageCode);
        }

        /// <summary>
        /// Возвращает выбранного подписанта с учетом тестового режима Kontur-only.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код этапа T1/T2/T3/T4.</param>
        /// <returns>Выбранный подписант этапа.</returns>
        private KonturStageSignerCandidate ResolveEffectiveSelectedSigner(long timelineId, string stageCode)
        {
            if (IsKonturOnlyTestModeEnabled(timelineId))
            {
                return CreateKonturTestSigningContextService().ResolveSelectedSigner(timelineId, StageToTitle(stageCode));
            }

            return CreateStageSignerService().ResolveSelectedSigner(timelineId, StageToTitle(stageCode));
        }

        /// <summary>
        /// Пытается вернуть выбранного подписанта с учетом тестового режима Kontur-only.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код этапа T1/T2/T3/T4.</param>
        /// <returns>Выбранный подписант или null.</returns>
        private KonturStageSignerCandidate TryResolveEffectiveSelectedSigner(long timelineId, string stageCode)
        {
            if (IsKonturOnlyTestModeEnabled(timelineId))
            {
                return CreateKonturTestSigningContextService().TryResolveSelectedSigner(timelineId, StageToTitle(stageCode));
            }

            return CreateStageSignerService().TryResolveSelectedSigner(timelineId, StageToTitle(stageCode));
        }


        /// <summary>
        /// Синхронизирует блок выбора подписанта с текущим TimelineId и этапом.
        /// </summary>
        /// <remarks>
        /// Метод каждый раз строит компактный серверный блок выбора подписанта, чтобы оператор видел
        /// актуальную роль этапа, организацию и сохраненный выбор без привязки к текущей сессии пользователя.
        /// </remarks>
        private void BindStageSignerSelector(bool forceRefresh)
        {
            ddlStageSigner.Items.Clear();
            ddlStageSigner.Items.Add(new System.Web.UI.WebControls.ListItem("-- выберите подписанта этапа --", string.Empty));
            ddlStageSigner.Enabled = false;
            litStageSignerContext.Text = string.Empty;
            litStageSignerWarning.Text = "<span class='signer-status signer-status-warn'>Укажите TimelineId и выберите этап, чтобы загрузить список подписантов.</span>";

            long timelineId;
            if (!long.TryParse((tbTimelineId.Text ?? string.Empty).Trim(), out timelineId) || timelineId <= 0)
            {
                return;
            }

            var stageCode = GetCurrentStageTabCode();
            var context = GetEffectiveStageSignerContext(timelineId, stageCode, forceRefresh);
            litStageSignerContext.Text = BuildSignerContextHtml(context);
            AppendKonturTestModeContextHint(timelineId);
            if (!context.IsResolved)
            {
                litStageSignerWarning.Text = "<span class='signer-status signer-status-err'>" + Server.HtmlEncode(context.ErrorMessage ?? "Контекст подписанта не определен.") + "</span>";
                AppendKonturTestModeWarningHint(timelineId);
                return;
            }

            if (context.Candidates.Count == 0)
            {
                litStageSignerWarning.Text = "<span class='signer-status signer-status-err'>Для этапа не найдены допустимые подписанты. Действия этапа заблокированы до появления полномочий.</span>";
                AppendKonturTestModeWarningHint(timelineId);
                return;
            }

            for (var i = 0; i < context.Candidates.Count; i++)
            {
                var candidate = context.Candidates[i];
                ddlStageSigner.Items.Add(new System.Web.UI.WebControls.ListItem(
                    BuildSignerOptionText(candidate),
                    candidate.SignerFizLicoId.ToString()));
            }

            ddlStageSigner.Enabled = true;

            var selection = GetEffectiveStageSignerSelection(timelineId, stageCode);
            if (selection != null)
            {
                var item = ddlStageSigner.Items.FindByValue(selection.SignerFizLicoId.ToString());
                if (item != null)
                {
                    ddlStageSigner.SelectedValue = item.Value;
                    litStageSignerWarning.Text = "<span class='signer-status signer-status-ok'>Выбранный подписант восстановлен из сохраненного состояния этапа.</span>";
                    AppendKonturTestModeWarningHint(timelineId);
                    return;
                }

                litStageSignerWarning.Text = "<span class='signer-status signer-status-warn'>Ранее выбранный подписант больше не входит в допустимый список. Выберите нового подписанта этапа.</span>";
                AppendKonturTestModeWarningHint(timelineId);
                return;
            }

            litStageSignerWarning.Text = "<span class='signer-status signer-status-warn'>Подписант этапа еще не выбран. Без явного выбора формирование и отправка этапа недоступны.</span>";
            AppendKonturTestModeWarningHint(timelineId);
        }

        /// <summary>
        /// Добавляет в контекст подписанта пояснение о включенном тестовом режиме Kontur-only.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <remarks>
        /// Пояснение нужно, чтобы оператор видел документ, который уже переведен в отдельный тестовый
        /// контур Контур и не должен анализироваться как обычный боевой сценарий подписания.
        /// </remarks>
        private void AppendKonturTestModeContextHint(long timelineId)
        {
            if (!IsKonturOnlyTestModeEnabled(timelineId))
            {
                return;
            }

            litStageSignerContext.Text +=
                "<br /><span class='signer-status signer-status-warn'>Активен тестовый режим Kontur-only. " +
                "Для этого TimelineId будет использоваться отдельный тестовый контур подписи Контур.</span>";
        }

        /// <summary>
        /// Добавляет в предупреждение по подписанту пометку о включенном тестовом режиме Kontur-only.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <remarks>
        /// Пометка нужна, чтобы различать обычную проблему выбора подписанта и документ,
        /// который уже переведен в отдельный тестовый сценарий интеграции Контур.
        /// </remarks>
        private void AppendKonturTestModeWarningHint(long timelineId)
        {
            if (!IsKonturOnlyTestModeEnabled(timelineId))
            {
                return;
            }

            litStageSignerWarning.Text +=
                "<br /><span class='signer-status signer-status-warn'>Для этого TimelineId включен тестовый режим Kontur-only.</span>";
        }

        /// <summary>
        /// Сохраняет выбранного подписанта текущего этапа в Perdoc.
        /// </summary>
        /// <remarks>Если оператор возвращается к пустому пункту, сохраненный выбор этапа удаляется.</remarks>
        private void SaveCurrentStageSignerSelection()
        {
            var timelineId = ParseTimelineId(tbTimelineId.Text.Trim());
            var stageCode = GetCurrentStageTabCode();
            var selectedValue = (ddlStageSigner.SelectedValue ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(selectedValue))
            {
                DeleteEffectiveStageSignerSelection(timelineId, stageCode);
                litStageSignerWarning.Text = "<span class='signer-status signer-status-warn'>Выбор подписанта очищен. Для действий этапа нужно выбрать подписанта заново.</span>";
                return;
            }

            long signerFizLicoId;
            if (!long.TryParse(selectedValue, out signerFizLicoId) || signerFizLicoId <= 0)
            {
                if (signerFizLicoId >= 0)
                {
                    throw new ApplicationException("Некорректный идентификатор выбранного подписанта этапа.");
                }
            }

            SaveEffectiveStageSignerSelection(timelineId, stageCode, signerFizLicoId);
            litStageSignerWarning.Text = "<span class='signer-status signer-status-ok'>Подписант этапа сохранен и будет использоваться для сборки XML, SignEpd и ручного импорта подписи.</span>";
            AppendKonturTestModeWarningHint(timelineId);
        }

        /// <summary>
        /// Проверяет, что для этапа уже выбран допустимый подписант.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код этапа UI.</param>
        /// <remarks>Проверка нужна перед запуском действий, которые должны опираться на явный выбор подписанта.</remarks>
        private void EnsureConfiguredSignerSelection(long timelineId, string stageCode)
        {
            ResolveEffectiveSelectedSigner(timelineId, StageToTitle(stageCode));
        }

        /// <summary>
        /// Возвращает выбранного подписанта этапа или null, если выбор пока не настроен.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код этапа UI.</param>
        /// <returns>Кандидат выбранного подписанта или null.</returns>
        /// <remarks>Метод используется в мягких сценариях, где нужна только предупреждающая диагностика.</remarks>
        private KonturStageSignerCandidate GetSelectedStageSignerCandidateOrNull(long timelineId, string stageCode)
        {
            return TryResolveEffectiveSelectedSigner(timelineId, StageToTitle(stageCode));
        }

        /// <summary>
        /// Возвращает выбранного и допустимого подписанта этапа.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="idzak">Идентификатор заявки ТИС.</param>
        /// <param name="titleCode">Код титула T1/T2/T3/T4.</param>
        /// <returns>Кандидат выбранного подписанта.</returns>
        /// <remarks>
        /// Метод одновременно проверяет наличие списка кандидатов, сохраненного выбора и его актуальность
        /// относительно текущих полномочий организации роли этапа.
        /// </remarks>
        private KonturStageSignerCandidate GetSelectedStageSignerCandidate(long timelineId, long idzak, string titleCode)
        {
            return ResolveEffectiveSelectedSigner(timelineId, StageToTitle(titleCode));
        }

        /// <summary>
        /// Формирует компактное описание роли и организации для блока выбора подписанта.
        /// </summary>
        /// <param name="context">Контекст выбора подписанта.</param>
        /// <returns>HTML-строка для вывода в верхнем блоке.</returns>
        /// <summary>
        /// Возвращает контекст подписанта этапа из кэша сессии или загружает его заново.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код этапа T1/T2/T3/T4.</param>
        /// <param name="forceRefresh">Признак принудительного обновления контекста из БД.</param>
        /// <returns>Контекст допустимых подписантов для выбранного этапа.</returns>
        /// <remarks>
        /// Кэш нужен только для ускорения UI-переходов между postback. Бизнес-валидация отправки и сборки
        /// по-прежнему опирается на сервис и БД, поэтому ускорение не меняет корректность сценария.
        /// </remarks>
        private KonturStageSignerContext GetCachedStageSignerContext(long timelineId, string stageCode, bool forceRefresh)
        {
            var cacheKey = BuildStageSignerContextCacheKey(timelineId, stageCode);
            if (!forceRefresh)
            {
                var cachedContext = Session[cacheKey] as KonturStageSignerContext;
                if (cachedContext != null)
                {
                    return cachedContext;
                }
            }

            var freshContext = CreateStageSignerService().GetContext(timelineId, stageCode);
            Session[cacheKey] = freshContext;
            return freshContext;
        }

        /// <summary>
        /// Возвращает закэшированный контекст тестовых подписантов Kontur-only.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код этапа T1/T2/T3/T4.</param>
        /// <param name="forceRefresh">Признак принудительного обновления кэша.</param>
        /// <returns>Контекст тестовых подписантов этапа.</returns>
        /// <remarks>
        /// Отдельный кэш нужен, чтобы тестовый контур не смешивался с обычным кэшем штатных
        /// подписантов и не подхватывал неактуальные бизнес-кандидаты.
        /// </remarks>
        private KonturStageSignerContext GetCachedTestStageSignerContext(long timelineId, string stageCode, bool forceRefresh)
        {
            var cacheKey = BuildTestStageSignerContextCacheKey(timelineId, stageCode);
            if (!forceRefresh)
            {
                var cachedContext = Session[cacheKey] as KonturStageSignerContext;
                if (cachedContext != null)
                {
                    return cachedContext;
                }
            }

            var freshContext = CreateKonturTestSigningContextService().GetContext(timelineId, stageCode);
            Session[cacheKey] = freshContext;
            return freshContext;
        }

        /// <summary>
        /// Формирует ключ кэша контекста подписанта этапа в рамках текущей сессии.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код этапа T1/T2/T3/T4.</param>
        /// <returns>Строковый ключ для Session.</returns>
        private string BuildStageSignerContextCacheKey(long timelineId, string stageCode)
        {
            return "KonturStageSignerContext::" + timelineId + "::" + StageToTitle(stageCode);
        }

        /// <summary>
        /// Формирует ключ session-кэша контекста тестовых подписантов Kontur-only.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код этапа T1/T2/T3/T4.</param>
        /// <returns>Ключ session-кэша тестового контекста.</returns>
        private string BuildTestStageSignerContextCacheKey(long timelineId, string stageCode)
        {
            return "KonturTestStageSignerContext::" + timelineId + "::" + StageToTitle(stageCode);
        }
        private string BuildSignerContextHtml(KonturStageSignerContext context)
        {
            if (context == null)
            {
                return string.Empty;
            }

            var roleName = Server.HtmlEncode(context.RequiredRoleName ?? string.Empty);
            var orgName = Server.HtmlEncode(context.RequiredKontragentName ?? string.Empty);
            var orgInn = Server.HtmlEncode(context.RequiredKontragentInn ?? string.Empty);
            return "<span class='signer-caption'>Этап " + Server.HtmlEncode(context.TitleCode ?? string.Empty) +
                   "</span><span class='signer-divider'>•</span>" +
                   "<span class='signer-caption'>Роль: " + roleName + "</span><span class='signer-divider'>•</span>" +
                   "<span class='signer-caption'>Организация: " + orgName + "</span><span class='signer-divider'>•</span>" +
                   "<span class='signer-caption'>ИНН: " + orgInn + "</span>";
        }

        /// <summary>
        /// Формирует строку списка для выбора подписанта.
        /// </summary>
        /// <param name="candidate">Кандидат подписанта.</param>
        /// <returns>Читаемая строка выбора в dropdown.</returns>
        private string BuildSignerOptionText(KonturStageSignerCandidate candidate)
        {
            var parts = new List<string>();
            parts.Add(candidate.SignerFio ?? string.Empty);
            if (!string.IsNullOrEmpty(candidate.Position))
            {
                parts.Add(candidate.Position);
            }

            if (!string.IsNullOrEmpty(candidate.AuthorityDescription))
            {
                parts.Add(candidate.AuthorityDescription);
            }

            if (!string.IsNullOrEmpty(candidate.MchdNumber))
            {
                parts.Add("МЧД: " + candidate.MchdNumber);
            }

            return string.Join(" | ", parts.ToArray());
        }

        /// <summary>
        /// Строит предупреждение по ручной подписи, если сертификат не похож на выбранного подписанта.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код этапа UI.</param>
        /// <param name="selectedSigner">Выбранный подписант этапа.</param>
        /// <param name="signatureBytes">Импортируемая detached CMS-подпись.</param>
        /// <returns>Текст предупреждения или пустую строку.</returns>
        /// <remarks>
        /// Предупреждение не блокирует импорт .sgn, но делает видимым риск рассинхрона между XML,
        /// выбранным подписантом и фактическим сертификатом подписи до отправки в Контур.
        /// </remarks>
        private string BuildManualSignatureWarningText(long timelineId, string stageCode, string xmlPath, KonturStageSignerCandidate selectedSigner, byte[] signatureBytes)
        {
            if (selectedSigner == null || signatureBytes == null || signatureBytes.Length == 0)
            {
                return string.Empty;
            }

            var xmlBytes = GetStageXmlBytesForSignatureWarning(timelineId, stageCode, xmlPath);
            if (xmlBytes == null || xmlBytes.Length == 0)
            {
                return "warning=Подпись импортирована без проверки сертификата: не найден XML этапа для сопоставления.";
            }

            string certificateSubject;
            string verificationError;
            bool? isMatch = TryMatchSignatureWithSelectedSigner(xmlBytes, signatureBytes, selectedSigner, out certificateSubject, out verificationError);
            if (!isMatch.HasValue)
            {
                return "warning=Подпись импортирована, но сертификат не удалось проверить: " + Safe(verificationError);
            }

            if (isMatch.Value)
            {
                return string.Empty;
            }

            return "warning=Подпись импортирована, но сертификат не совпал с выбранным подписантом." + Environment.NewLine +
                   "selectedSigner=" + Safe(selectedSigner.SignerFio) + Environment.NewLine +
                   "certificateSubject=" + Safe(certificateSubject);
        }

        /// <summary>
        /// Возвращает XML этапа, который используется для проверки detached CMS-подписи.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код этапа UI.</param>
        /// <returns>Байты XML текущего этапа или null.</returns>
        private byte[] GetStageXmlBytesForSignatureWarning(long timelineId, string stageCode, string xmlPath)
        {
            if (!string.IsNullOrEmpty(xmlPath) && File.Exists(xmlPath))
            {
                return File.ReadAllBytes(xmlPath);
            }

            var titleCode = StageToTitle(stageCode);
            if (titleCode == "T1")
            {
                return EpdRepo.GetXmlPayloadTitul(timelineId);
            }

            if (titleCode == "T2")
            {
                return EpdRepo.GetTitul2Xml(timelineId);
            }

            return null;
        }

        /// <summary>
        /// Сопоставляет сертификат подписи с выбранным подписантом этапа.
        /// </summary>
        /// <param name="xmlBytes">Байты XML, к которому относится detached подпись.</param>
        /// <param name="signatureBytes">Байты detached CMS-подписи.</param>
        /// <param name="selectedSigner">Выбранный подписант этапа.</param>
        /// <param name="certificateSubject">Возвращает subject сертификата, если он прочитан.</param>
        /// <param name="verificationError">Возвращает описание ошибки проверки, если она произошла.</param>
        /// <returns>
        /// True, если сертификат похож на выбранного подписанта;
        /// False, если сертификат прочитан, но не совпал;
        /// null, если проверку выполнить не удалось.
        /// </returns>
        private bool? TryMatchSignatureWithSelectedSigner(byte[] xmlBytes, byte[] signatureBytes, KonturStageSignerCandidate selectedSigner, out string certificateSubject, out string verificationError)
        {
            certificateSubject = string.Empty;
            verificationError = string.Empty;

            try
            {
                var cms = new SignedCms(new ContentInfo(xmlBytes), true);
                cms.Decode(signatureBytes);
                cms.CheckSignature(true);
                if (cms.SignerInfos.Count == 0 || cms.SignerInfos[0].Certificate == null)
                {
                    verificationError = "В подписи отсутствует сертификат подписанта.";
                    return null;
                }

                certificateSubject = cms.SignerInfos[0].Certificate.Subject ?? string.Empty;
                var certInn = ExtractCertificateSubjectValue(certificateSubject, "ИНН");
                var certSurname = ExtractCertificateSubjectValue(certificateSubject, "SN");
                var certGiven = ExtractCertificateSubjectValue(certificateSubject, "G");

                if (!string.IsNullOrEmpty(selectedSigner.SignerInnFl) && !string.IsNullOrEmpty(certInn))
                {
                    return string.Equals(selectedSigner.SignerInnFl.Trim(), certInn.Trim(), StringComparison.OrdinalIgnoreCase);
                }

                var selectedFio = (selectedSigner.SignerFio ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(selectedFio))
                {
                    return null;
                }

                var certFio = (certSurname + " " + certGiven).Trim();
                if (string.IsNullOrEmpty(certFio))
                {
                    return null;
                }

                return string.Equals(selectedFio, certFio, StringComparison.OrdinalIgnoreCase)
                    || selectedFio.StartsWith(certSurname + " ", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                verificationError = ex.Message;
                return null;
            }
        }

        /// <summary>
        /// Извлекает значение поля из subject сертификата.
        /// </summary>
        /// <param name="subject">Строка subject сертификата.</param>
        /// <param name="fieldName">Имя поля subject, например ИНН, SN или G.</param>
        /// <returns>Значение поля или пустую строку.</returns>
        private string ExtractCertificateSubjectValue(string subject, string fieldName)
        {
            if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(fieldName))
            {
                return string.Empty;
            }

            var match = Regex.Match(subject, "(?:^|,\\s*)" + Regex.Escape(fieldName) + "=([^,]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return match.Success ? (match.Groups[1].Value ?? string.Empty).Trim() : string.Empty;
        }

        /// <summary>
        /// Определяет подписанта, явно выбранного для сборки XML текущего этапа Контур.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="idzak">Идентификатор заявки ТИС.</param>
        /// <param name="titleCode">Код титула Контур.</param>
        /// <returns>Идентификатор TFizLico, выбранного и допустимого для подписи этапа.</returns>
        /// <remarks>
        /// Метод опирается на сохраненный выбор оператора, а не на текущую сессию ТИС.
        /// Это нужно, чтобы один оператор мог прогонять T1-T4 с разными подписантами по ролям.
        /// </remarks>
        private int ResolveSigningUserIdForKonturStage(long timelineId, long idzak, string titleCode)
        {
            if (IsKonturOnlyTestModeEnabled(timelineId))
            {
                // В тестовом режиме реальный TFizLico тестового сертификата в ТИС отсутствует.
                // Поэтому builder получает текущего пользователя сессии, а финальный субъект подписи
                // будет синхронизирован отдельным Kontur-only override на уровне XML.
                return (int)GetCurrentUserId();
            }

            return CreateStageSignerService().ResolveSigningUserId(timelineId, StageToTitle(titleCode));
        }

        /// <summary>
        /// Возвращает идентификатор текущего пользователя ТИС.
        /// </summary>
        /// <returns>Идентификатор TFizLico из Session["UserId"] или 0, если определить его не удалось.</returns>
        /// <remarks>Метод нужен для аудита сохранения выбора подписанта и совместимости с текущей моделью авторизации ТИС.</remarks>
        private int GetCurrentUserId()
        {
            try
            {
                var raw = Session["UserId"];
                int userId;
                return raw != null && int.TryParse(raw.ToString(), out userId) ? userId : 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Извлекает значение атрибута из XML по локальному имени атрибута.
        /// </summary>
        /// <param name="xmlText">Текст XML.</param>
        /// <param name="attributeName">Локальное имя атрибута.</param>
        /// <returns>Значение атрибута или пустая строка.</returns>
        private string ExtractAttrFromXml(string xmlText, string attributeName)
        {
            if (string.IsNullOrEmpty(xmlText) || string.IsNullOrEmpty(attributeName))
            {
                return string.Empty;
            }

            try
            {
                var doc = XDocument.Parse(xmlText, LoadOptions.PreserveWhitespace);
                foreach (var element in doc.Descendants())
                {
                    foreach (var attribute in element.Attributes())
                    {
                        if (string.Equals(attribute.Name.LocalName, attributeName, StringComparison.OrdinalIgnoreCase))
                        {
                            return (attribute.Value ?? string.Empty).Trim();
                        }
                    }
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        /// <summary>
        /// Получает текст XML титула T3 для построения T4.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline.</param>
        /// <returns>Текст T3 XML или пустая строка.</returns>
        private string ResolveT3XmlTextForT4(long timelineId)
        {
            var repository = new KonturTitleArtifactRepository(Connection.conStr());
            var artifact = repository.GetLatest(timelineId, "T3");
            if (artifact != null && artifact.HasXml)
            {
                return Encoding.GetEncoding(1251).GetString(artifact.TitleXml);
            }

            var root = GetKonturServerFilesDirectory();
            var selectedPath = ResolveServerFilePath(root, ddlServerXmlFile);
            if (!string.IsNullOrEmpty(selectedPath) && File.Exists(selectedPath))
            {
                var name = (Path.GetFileName(selectedPath) ?? string.Empty).ToLowerInvariant();
                if (name.StartsWith("t3_"))
                {
                    return File.ReadAllText(selectedPath, Encoding.GetEncoding(1251));
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Получает подпись T3 в Base64 для построения T4.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline.</param>
        /// <returns>Подпись T3 в Base64 или пустая строка.</returns>
        private string ResolveT3SignatureBase64(long timelineId)
        {
            var root = GetKonturServerFilesDirectory();
            var selectedSgnPath = ResolveServerFilePath(root, ddlServerSgnFile);
            if (!string.IsNullOrEmpty(selectedSgnPath) && File.Exists(selectedSgnPath))
            {
                var bytes = File.ReadAllBytes(selectedSgnPath);
                if (bytes != null && bytes.Length > 0)
                {
                    return Convert.ToBase64String(bytes);
                }
            }

            return EpdRepo.GetSignatureBase64(timelineId) ?? string.Empty;
        }
        /// <summary>
        /// Подбирает актуальный T1 XML для синхронизации в epd_doc_store перед сборкой T2.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <returns>Байты T1 XML или null, если подходящий источник не найден.</returns>
        /// <remarks>
        /// Приоритет отдается t1_override, потому что этот файл уже нормализован под Контур
        /// и содержит фактически используемые 2BM идентификаторы участников.
        /// </remarks>
        private byte[] ResolveT1XmlSeedBytes(long timelineId)
        {
            var currentPath = CreateArtifactWorkspaceService().FindCurrentXmlPath(timelineId, "T1");
            if (!string.IsNullOrEmpty(currentPath))
            {
                return File.ReadAllBytes(currentPath);
            }

            var root = GetKonturServerFilesDirectory();
            var selectedPath = ResolveServerFilePath(root, ddlServerXmlFile);
            if (IsT1XmlFileName(Path.GetFileName(selectedPath)))
            {
                return File.ReadAllBytes(selectedPath);
            }

            // Для T2 сначала берем нормализованный T1 override, потому что именно он содержит
            // принятые Контуром 2BM идентификаторы участников и должен оставаться источником истины.
            var overrideMask = string.Format("t1_override_{0}_*.xml", timelineId);
            var overrideCandidates = Directory.GetFiles(root, overrideMask, SearchOption.TopDirectoryOnly);
            if (overrideCandidates != null && overrideCandidates.Length > 0)
            {
                Array.Sort(overrideCandidates, StringComparer.OrdinalIgnoreCase);
                Array.Reverse(overrideCandidates);
                return File.ReadAllBytes(overrideCandidates[0]);
            }

            var mask = string.Format("t1_timeline{0}_*.xml", timelineId);
            var candidates = Directory.GetFiles(root, mask, SearchOption.TopDirectoryOnly);
            if (candidates == null || candidates.Length == 0)
            {
                return null;
            }

            Array.Sort(candidates, StringComparer.OrdinalIgnoreCase);
            Array.Reverse(candidates);
            return File.ReadAllBytes(candidates[0]);
        }

        /// <summary>
        /// Проверяет, что имя файла похоже на T1 XML.
        /// </summary>
        /// <param name="fileName">Имя файла без пути.</param>
        /// <returns>True, если файл можно использовать как источник T1 XML.</returns>
        /// <remarks>
        /// Метод нужен как простая защита от случайного выбора T2/T3 XML при восстановлении payload_xml.
        /// </remarks>
        private bool IsT1XmlFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return false;
            }

            var lower = fileName.ToLowerInvariant();
            return lower.StartsWith("t1_") || lower.StartsWith("on_trnaclgrot");
        }

        /// <summary>
        /// Определяет слот подписи epd_doc_store для ручного импорта по этапу Контур.
        /// </summary>
        /// <param name="stageCode">Код текущего этапа UI.</param>
        /// <returns>Номер подписи: 1 для T1, 2 для T2.</returns>
        /// <remarks>
        /// В текущей схеме epd_doc_store хранит только первую и вторую подпись документа.
        /// Поэтому ручной импорт в БД ограничен T1/T2, а T3/T4 используют прямую отправку выбранного .sgn на этап.
        /// </remarks>
        /// <summary>
        /// Возвращает последнюю непустую подпись T1 из epd_doc_store для сборки T2.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <returns>Байты detached-подписи T1 или null, если подпись не найдена.</returns>
        /// <remarks>
        /// После ручных синхронизаций XML в epd_doc_store может появиться более новая версия строки без sig_detached.
        /// Для сборки T2 нужно брать не просто последнюю запись, а последнюю запись с фактически сохраненной подписью T1.
        /// </remarks>
        private byte[] GetLatestNonEmptyT1SignatureBytes(long timelineId)
        {
            using (var connection = new SqlConnection(Connection.conStr()))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT TOP (1) sig_detached
FROM Perdoc.dbo.epd_doc_store
WHERE timeline_id = @tid
  AND sig_detached IS NOT NULL
  AND DATALENGTH(sig_detached) > 0
ORDER BY version_no DESC, id DESC;";
                command.Parameters.AddWithValue("@tid", timelineId);
                connection.Open();
                var value = command.ExecuteScalar();
                return value == null || value == DBNull.Value ? null : (byte[])value;
            }
        }
        private int ResolveManualSignatureSlot(string stageCode)
        {
            if (string.Equals(stageCode, "T1_INITIAL", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stageCode, "T1_DRAFT", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (string.Equals(stageCode, "T2", StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            throw new ApplicationException("Ручной импорт подписи в epd_doc_store поддержан только для T1 и T2. Для T3/T4 используйте выбранный .sgn при запуске этапа.");
        }

        /// <summary>
        /// Гарантирует наличие актуального XML в epd_doc_store перед ручным импортом подписи.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код текущего этапа UI.</param>
        /// <param name="xmlPath">Путь к выбранному XML в серверной папке.</param>
        /// <remarks>
        /// Ручная detached-подпись должна быть привязана к тому же XML, который затем участвует в сборке и отправке.
        /// Поэтому перед сохранением .sgn страница при необходимости досинхронизирует XML из серверного файла в БД.
        /// </remarks>
        private void EnsureDocStoreXmlForManualSignatureImport(long timelineId, string stageCode, string xmlPath)
        {
            if (string.Equals(stageCode, "T2", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(xmlPath) || !File.Exists(xmlPath))
                {
                    var t2Bytes = EpdRepo.GetTitul2Xml(timelineId);
                    if (t2Bytes != null && t2Bytes.Length > 0)
                    {
                        return;
                    }

                    throw new ApplicationException("Для ручного импорта подписи T2 сначала выберите T2 XML из серверной папки или выполните генерацию титула.");
                }

                // Для T2 всегда привязываем импортируемую подпись к выбранному XML,
                // чтобы локальная CMS-проверка и последующая отправка работали по одной версии титула.
                EpdRepo.SaveTitul2Xml(timelineId, File.ReadAllBytes(xmlPath));
                return;
            }

            var xmlText = EpdRepo.GetXmlText(timelineId);
            if (!string.IsNullOrEmpty(xmlText))
            {
                return;
            }

            if (string.IsNullOrEmpty(xmlPath) || !File.Exists(xmlPath))
            {
                throw new ApplicationException("Для ручного импорта подписи T1 сначала выберите T1 XML из серверной папки или выполните генерацию титула.");
            }

            EpdRepo.UpsertDoc(timelineId, File.ReadAllBytes(xmlPath), null, null);
        }
        /// <summary>
        /// Разрешает абсолютные пути к выбранным серверным XML/SGN-файлам.
        /// </summary>
        /// <param name="xmlPath">Итоговый путь к XML на сервере.</param>
        /// <param name="signaturePath">Итоговый путь к подписи на сервере.</param>
        /// <remarks>
        /// Пустой выбор возвращается как пустая строка для поддержки внутренних сценариев,
        /// где XML и подпись уже берутся из SQL-артефактов или рабочего файлового слоя реконструкции.
        /// </remarks>
        private void ResolveServerSelectedPaths(out string xmlPath, out string signaturePath)
        {
            var root = GetKonturServerFilesDirectory();
            xmlPath = ResolveServerFilePath(root, ddlServerXmlFile);
            signaturePath = ResolveServerFilePath(root, ddlServerSgnFile);
        }

        /// <summary>
        /// Заполняет селекторы файлов XML/SGN на основании содержимого серверной папки.
        /// </summary>
        /// <remarks>В списках сохраняются относительные имена файлов, а полный путь строится на сервере при запуске этапа.</remarks>
        private void BindServerFileSelectors()
        {
            var root = GetKonturServerFilesDirectory();
            Directory.CreateDirectory(root);

            BindSelectorByMask(ddlServerXmlFile, root, "*.xml");
            BindSelectorByMask(ddlServerSgnFile, root, "*.sgn");
        }

        /// <summary>
        /// Заполняет указанный выпадающий список файлами по маске.
        /// </summary>
        /// <param name="selector">Целевой выпадающий список.</param>
        /// <param name="directory">Абсолютный путь к директории поиска.</param>
        /// <param name="mask">Маска поиска файлов.</param>
        /// <remarks>Первый пункт оставляется пустым для возможности запуска через внутренний runner.</remarks>
        private void BindSelectorByMask(System.Web.UI.WebControls.DropDownList selector, string directory, string mask)
        {
            selector.Items.Clear();
            selector.Items.Add(string.Empty);

            var files = Directory.GetFiles(directory, mask, SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            Array.Reverse(files);
            for (var i = 0; i < files.Length; i++)
            {
                var name = Path.GetFileName(files[i]);
                selector.Items.Add(name);
            }
        }

        /// <summary>
        /// Преобразует выбранное имя файла в абсолютный путь с валидацией существования.
        /// </summary>
        /// <param name="rootDirectory">Корневая директория серверных файлов Контур.</param>
        /// <param name="selector">Выпадающий список выбора файла.</param>
        /// <returns>Абсолютный путь к выбранному файлу или пустая строка.</returns>
        /// <remarks>Проверка имени через Path.GetFileName защищает от попыток выхода за пределы рабочей папки.</remarks>
        private string ResolveServerFilePath(string rootDirectory, System.Web.UI.WebControls.DropDownList selector)
        {
            if (selector == null)
            {
                return string.Empty;
            }

            var selected = (selector.SelectedValue ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(selected))
            {
                return string.Empty;
            }

            var safeFileName = Path.GetFileName(selected);
            var fullPath = Path.Combine(rootDirectory, safeFileName);
            if (!File.Exists(fullPath))
            {
                throw new ApplicationException("Выбранный файл не найден в серверной папке: " + safeFileName);
            }

            return fullPath;
        }

        /// <summary>
        /// Создает сервис рабочего файлового слоя артефактов этапов.
        /// </summary>
        /// <returns>Сервис управления текущими XML-файлами этапов.</returns>
        /// <remarks>Сервис изолирует правила именования и очистки рабочей папки от остального кода страницы.</remarks>
        private KonturStageArtifactWorkspaceService CreateArtifactWorkspaceService()
        {
            return new KonturStageArtifactWorkspaceService(GetKonturServerFilesDirectory());
        }

        /// <summary>
        /// Создает repository явного состояния этапа Контур.
        /// </summary>
        /// <returns>Repository чтения и сохранения явного состояния этапа.</returns>
        /// <remarks>
        /// Выделенная фабрика нужна, чтобы bridge-логика страницы использовала тот же SQL-контракт реконструкции,
        /// но не создавала зависимости new прямо внутри обработчиков кнопок.
        /// </remarks>
        private IKonturStageStateRepository CreateStageStateRepository()
        {
            return new KonturStageStateRepository(Connection.conStr());
        }

        /// <summary>
        /// Создает репозиторий SQL-артефактов титулов Контур.
        /// </summary>
        /// <returns>Репозиторий артефактов титулов.</returns>
        private KonturTitleArtifactRepository CreateTitleArtifactRepository()
        {
            return new KonturTitleArtifactRepository(Connection.conStr());
        }

        /// <summary>
        /// Создает единый фасад нормализации XML титулов T1-T4.
        /// </summary>
        /// <returns>Сервис этапной нормализации XML.</returns>
        /// <remarks>
        /// Фасад нужен, чтобы правила пост-обработки XML не расползались между страницей,
        /// runner и отдельными сервисами этапов.
        /// </remarks>
        private KonturEtrnT1234XmlService CreateEtrnT1234XmlService()
        {
            return new KonturEtrnT1234XmlService(Connection.conStr());
        }

        /// <summary>
        /// Создает сценарий сборки XML титула этапа реконструкционного слоя.
        /// </summary>
        /// <returns>Use case сборки XML титула этапа.</returns>
        /// <remarks>
        /// Страница использует этот сценарий только для add-only мостов, чтобы build-логика жила в реконструкционном слое,
        /// а code-behind оставался координатором UI и fallback-переходов.
        /// </remarks>
        private BuildStageTitleUseCase CreateBuildStageTitleUseCase()
        {
            return new BuildStageTitleUseCase(
                new KonturTitleBuilder(CreateTitleArtifactRepository(), Connection.conStr()),
                CreateTitleArtifactRepository(),
                CreateStageStateRepository());
        }

        /// <summary>
        /// Создает сценарий импорта detached-подписи реконструкционного слоя.
        /// </summary>
        /// <returns>Use case импорта и локальной проверки подписи этапа.</returns>
        /// <remarks>
        /// Сценарий подключается только там, где уже существует явное состояние XML этапа.
        /// Это сохраняет fallback на legacy-импорт до полного перевода build-шага в реконструкционный слой.
        /// </remarks>
        private ImportStageSignatureUseCase CreateImportStageSignatureUseCase()
        {
            var artifactRepository = CreateTitleArtifactRepository();
            return new ImportStageSignatureUseCase(
                new KonturSignatureService(artifactRepository),
                artifactRepository,
                CreateStageStateRepository());
        }

        /// <summary>
        /// Сохраняет текущий XML этапа одновременно в рабочую папку и в SQL-хранилище артефактов.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="titleCode">Код титула T1/T2/T3/T4.</param>
        /// <param name="xmlBytes">Байты XML титула.</param>
        /// <returns>Абсолютный путь к актуальному XML-файлу рабочего слоя.</returns>
        /// <remarks>
        /// БД остается источником истины по артефакту этапа, а рабочая папка хранит только одну актуальную копию,
        /// которую оператор может выбрать или выгрузить без разбора служебных версий.
        /// </remarks>
        private string SaveCurrentStageXmlArtifact(long timelineId, string titleCode, byte[] xmlBytes)
        {
            xmlBytes = CreateEtrnT1234XmlService().NormalizeBytes(timelineId, titleCode, xmlBytes);
            var workspace = CreateArtifactWorkspaceService();
            var currentPath = workspace.SaveCurrentXml(timelineId, titleCode, xmlBytes);
            var repository = CreateTitleArtifactRepository();
            repository.SaveDraftArtifact(new KonturTitleArtifact
            {
                TimelineId = timelineId,
                TitleCode = titleCode,
                XmlFileName = Path.GetFileName(currentPath),
                TitleXml = xmlBytes
            });

            return currentPath;
        }

        /// <summary>
        /// Сбрасывает явное состояние этапа после ручной пересборки XML через legacy-кнопки страницы.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код этапа UI.</param>
        /// <param name="titleCode">Код титула этапа.</param>
        /// <remarks>
        /// T1 и T2 пока собираются через legacy-ветки страницы и обходят BuildStageTitleUseCase.
        /// Поэтому после новой версии XML нужно вручную привести state к тому же черновому состоянию:
        /// сбросить подпись, Sent, Completed, NextStageAllowed и старые operator refs текущего этапа.
        /// </remarks>
        private void ResetStageStateAfterManualXmlBuild(long timelineId, string stageCode, string titleCode)
        {
            var repository = CreateStageStateRepository();
            var current = repository.Get(timelineId, stageCode);
            var state = current ?? new KonturStageState
            {
                TimelineId = timelineId,
                StageCode = stageCode,
                TitleCode = titleCode
            };

            state.TitleCode = titleCode;
            state.XmlBuilt = true;
            state.SignatureImported = false;
            state.Sent = false;
            state.Completed = false;
            state.NextStageAllowed = false;
            state.TransportationId = string.Empty;
            state.TitleId = string.Empty;
            state.LastOperatorStatus = string.Empty;
            state.LastErrorCode = string.Empty;
            state.LastErrorMessage = string.Empty;

            repository.Save(state);
        }

        /// <summary>
        /// Синхронизирует detached-подпись текущего этапа в SQL-хранилище артефактов Контур.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код текущего этапа UI.</param>
        /// <param name="signaturePath">Путь к выбранной подписи в рабочей папке.</param>
        /// <param name="signatureBytes">Байты detached-подписи.</param>
        /// <param name="selectedSigner">Выбранный подписант этапа.</param>
        /// <remarks>
        /// Синхронизация нужна, чтобы повторный запуск этапа и будущая выгрузка документооборота опирались
        /// на единый SQL-артефакт, а не только на epd_doc_store и рабочую папку.
        /// </remarks>
        private void SyncStageSignatureArtifact(long timelineId, string stageCode, string signaturePath, byte[] signatureBytes, KonturStageSignerCandidate selectedSigner)
        {
            var titleCode = StageToTitle(stageCode);
            var repository = CreateTitleArtifactRepository();
            repository.SaveSignature(
                timelineId,
                titleCode,
                Path.GetFileName(signaturePath),
                signatureBytes,
                string.Empty,
                selectedSigner == null ? string.Empty : selectedSigner.RequiredRoleName,
                DateTime.Now);
        }

        /// <summary>
        /// Синхронизирует явное состояние этапа после legacy-импорта подписи.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код текущего этапа UI.</param>
        /// <param name="xmlPath">Путь к выбранному XML этапа.</param>
        /// <param name="signatureBytes">Байты detached-подписи этапа.</param>
        /// <remarks>
        /// Шаг нужен для T1/T2 legacy-сценария, где импорт подписи еще может идти старым путем,
        /// но reconstruction слой уже должен увидеть подготовленный XML и SGN как явное состояние этапа.
        /// </remarks>
        private void BridgePreparedStageStateAfterLegacySignatureImport(long timelineId, string stageCode, string xmlPath, byte[] signatureBytes)
        {
            var repository = CreateStageStateRepository();
            var current = repository.Get(timelineId, stageCode);
            var state = current ?? new KonturStageState
            {
                TimelineId = timelineId,
                StageCode = stageCode,
                TitleCode = StageToTitle(stageCode)
            };

            state.TitleCode = StageToTitle(stageCode);
            state.XmlBuilt = HasStageXmlReadyForStateBridge(timelineId, stageCode, xmlPath);
            state.SignatureImported = signatureBytes != null && signatureBytes.Length > 0;
            state.Sent = false;
            state.Completed = false;
            state.NextStageAllowed = false;
            state.LastOperatorStatus = "Подпись импортирована через legacy bridge.";
            state.LastErrorCode = string.Empty;
            state.LastErrorMessage = string.Empty;

            repository.Save(state);
        }

        /// <summary>
        /// Синхронизирует явное состояние этапа после успешной отправки через legacy runtime.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код текущего этапа UI.</param>
        /// <param name="xmlPath">Путь к XML этапа.</param>
        /// <param name="signaturePath">Путь к подписи этапа.</param>
        /// <param name="operatorStatus">Операторское сообщение успешного runtime-вызова.</param>
        /// <remarks>
        /// Синхронизация нужна, чтобы после успешного старого send-path следующий шаг уже мог опираться
        /// на явную модель состояния без ручного SQL-вмешательства.
        /// </remarks>
        private void BridgeLegacySentStageStateAfterSuccessfulRun(long timelineId, string stageCode, string xmlPath, string signaturePath, string operatorStatus)
        {
            var repository = CreateStageStateRepository();
            var current = repository.Get(timelineId, stageCode);
            var state = current ?? new KonturStageState
            {
                TimelineId = timelineId,
                StageCode = stageCode,
                TitleCode = StageToTitle(stageCode)
            };

            state.TitleCode = StageToTitle(stageCode);
            state.XmlBuilt = HasStageXmlReadyForStateBridge(timelineId, stageCode, xmlPath);
            state.SignatureImported = HasStageSignatureReadyForStateBridge(timelineId, stageCode, signaturePath);
            state.Sent = true;
            state.Completed = false;
            state.NextStageAllowed = false;
            state.LastOperatorStatus = string.IsNullOrEmpty(operatorStatus) ? "Этап отправлен через legacy runtime." : operatorStatus;
            state.LastErrorCode = string.Empty;
            state.LastErrorMessage = string.Empty;

            repository.Save(state);

            // После legacy runtime refs уже записаны в Perdoc, поэтому сразу подтягиваем их
            // в явное состояние этапа и убираем зависимость T1 completion-пути от ручной SQL-синхронизации.
            CreateSyncStageStateRefsUseCase().Execute(timelineId, stageCode);
        }

        /// <summary>
        /// Готовит или валидирует явное состояние этапа перед ручным подтверждением завершения.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код текущего этапа UI.</param>
        /// <param name="xmlPath">Путь к XML этапа.</param>
        /// <param name="signaturePath">Путь к подписи этапа.</param>
        /// <remarks>
        /// Если явное состояние еще не было синхронизировано, но fallback read-model уже видит успешный ответ оператора,
        /// страница создает мостовую запись Sent=true, чтобы оператор мог подтвердить завершение этапа без SSMS.
        /// </remarks>
        private void EnsureStageStateReadyForManualCompletion(long timelineId, string stageCode, string xmlPath, string signaturePath)
        {
            var repository = CreateStageStateRepository();
            var current = repository.Get(timelineId, stageCode);
            if (current != null && (current.Sent || current.Completed))
            {
                return;
            }

            var fallbackState = CreateProbeStageFlowService().BuildState(timelineId, stageCode, xmlPath, signaturePath);
            var sendReady = fallbackState != null &&
                            fallbackState.SendStep != null &&
                            string.Equals(fallbackState.SendStep.CssClass, "step-state step-state-ready", StringComparison.OrdinalIgnoreCase);
            var resultReady = fallbackState != null &&
                              fallbackState.ResultStep != null &&
                              string.Equals(fallbackState.ResultStep.CssClass, "step-state step-state-ready", StringComparison.OrdinalIgnoreCase);

            if (!sendReady || !resultReady)
            {
                throw new ApplicationException("Нельзя подтвердить этап, пока на странице не виден успешный результат отправки.");
            }

            BridgeLegacySentStageStateAfterSuccessfulRun(
                timelineId,
                stageCode,
                xmlPath,
                signaturePath,
                "Этап синхронизирован из fallback read-model перед ручным подтверждением.");
        }

        /// <summary>
        /// Проверяет, что XML этапа уже доступен для явного state-bridge.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код текущего этапа UI.</param>
        /// <param name="xmlPath">Путь к выбранному XML этапа.</param>
        /// <returns>True, если XML уже доступен; иначе false.</returns>
        private bool HasStageXmlReadyForStateBridge(long timelineId, string stageCode, string xmlPath)
        {
            var titleCode = StageToTitle(stageCode);
            if (!string.IsNullOrEmpty(xmlPath) && File.Exists(xmlPath))
            {
                return true;
            }

            var artifact = CreateTitleArtifactRepository().GetLatest(timelineId, titleCode);
            if (artifact != null && artifact.HasXml)
            {
                return true;
            }

            return !string.IsNullOrEmpty(CreateArtifactWorkspaceService().FindCurrentXmlPath(timelineId, titleCode));
        }

        /// <summary>
        /// Проверяет, что подпись этапа уже доступна для явного state-bridge.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код текущего этапа UI.</param>
        /// <param name="signaturePath">Путь к выбранной подписи этапа.</param>
        /// <returns>True, если подпись уже доступна; иначе false.</returns>
        /// <remarks>
        /// Для T1/T2 допускается чтение legacy-подписей из EPD-хранилища, потому что именно они
        /// пока используются частью старого runtime-контура и должны быть видимы reconstruction состоянию.
        /// </remarks>
        private bool HasStageSignatureReadyForStateBridge(long timelineId, string stageCode, string signaturePath)
        {
            var titleCode = StageToTitle(stageCode);
            if (!string.IsNullOrEmpty(signaturePath) && File.Exists(signaturePath))
            {
                return true;
            }

            var artifact = CreateTitleArtifactRepository().GetLatest(timelineId, titleCode);
            if (artifact != null && artifact.HasSignature)
            {
                return true;
            }

            if (string.Equals(titleCode, "T1", StringComparison.OrdinalIgnoreCase))
            {
                var t1Signature = GetLatestNonEmptyT1SignatureBytes(timelineId);
                return t1Signature != null && t1Signature.Length > 0;
            }

            if (string.Equals(titleCode, "T2", StringComparison.OrdinalIgnoreCase))
            {
                var sig2Bytes = EpdRepo.GetSig2Bytes(timelineId);
                return sig2Bytes != null && sig2Bytes.Length > 0;
            }

            return !string.IsNullOrEmpty(CreateArtifactWorkspaceService().FindCurrentSignaturePath(timelineId, titleCode));
        }

        /// <summary>
        /// Определяет, можно ли безопасно импортировать подпись через реконструкционный use case.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код текущего этапа UI.</param>
        /// <returns>True, если явное состояние этапа уже подтверждает готовность XML; иначе false.</returns>
        /// <remarks>
        /// Проверка нужна, чтобы не блокировать ручной legacy-сценарий на документах, где build еще выполнялся
        /// вне реконструкционного слоя и явная запись TEpdKonturStageState пока не создана.
        /// </remarks>
        private bool CanUseSignatureImportUseCase(long timelineId, string stageCode)
        {
            var stageState = CreateStageStateRepository().Get(timelineId, stageCode);
            return stageState != null && stageState.XmlBuilt;
        }

        /// <summary>
        /// Сохраняет совместимую копию подписи в legacy-хранилище, если старый runtime еще зависит от него.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код текущего этапа UI.</param>
        /// <param name="xmlPath">Путь к выбранному XML этапа.</param>
        /// <param name="signatureBytes">Байты detached-подписи.</param>
        /// <remarks>
        /// Для T1 и T2 текущий runtime-отправитель еще умеет читать подпись из epd_doc_store.
        /// Поэтому даже после успешного импорта в реконструкционный слой страница делает совместимую запись в legacy-слой.
        /// Для T3/T4 такой шаг не нужен, потому что в legacy-таблице нет отдельных слотов подписи этапа.
        /// </remarks>
        private void SaveLegacySignatureCompatibilityCopy(long timelineId, string stageCode, string xmlPath, byte[] signatureBytes)
        {
            if (!string.Equals(stageCode, "T2", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(stageCode, "T1_INITIAL", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(stageCode, "T1_DRAFT", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var signatureSlot = ResolveManualSignatureSlot(stageCode);
            EnsureDocStoreXmlForManualSignatureImport(timelineId, stageCode, xmlPath);
            EpdRepo.SaveSignature(
                timelineId,
                signatureBytes,
                signatureSlot.ToString(),
                "Совместимая копия подписи после импорта через реконструкционный слой",
                0);
        }

        /// <summary>
        /// Возвращает текст слота подписи для операторной диагностики.
        /// </summary>
        /// <param name="stageCode">Код текущего этапа UI.</param>
        /// <returns>Номер слота T1/T2 или пометка, что legacy-слот не используется.</returns>
        /// <remarks>
        /// Текст отделен в отдельный метод, чтобы лог не падал на T3/T4, где ручной импорт уже идет
        /// через stage-артефакт и явное состояние, а не через epd_doc_store.
        /// </remarks>
        private string ResolveManualSignatureSlotText(string stageCode)
        {
            if (string.Equals(stageCode, "T1_INITIAL", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stageCode, "T1_DRAFT", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stageCode, "T2", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveManualSignatureSlot(stageCode).ToString();
            }

            return "artifact-only";
        }
        /// <summary>
        /// Применяет к серверному T1 XML те же подмены участников, что используются при отправке в Контур.
        /// </summary>
        /// <param name="xmlPath">Путь к сохраненному T1 XML.</param>
        /// <param name="timelineId">Идентификатор timeline.</param>
        /// <returns>Путь к исходному или нормализованному XML.</returns>
        /// <remarks>
        /// Метод нужен, чтобы локально сформированный T1 и отправляемый T1 совпадали по составу участников и ИдФайл.
        /// Иначе T2 наследует устаревшие 2AE-идентификаторы из сырого XML и не совпадает с созданной перевозкой.
        /// </remarks>
        private string ApplyKonturT1ParticipantsOverride(string xmlPath, long timelineId)
        {
            return CreateEtrnT1234XmlService().NormalizeFile(xmlPath, timelineId, "T1");
        }
        /// <summary>
        /// Возвращает серверную папку хранения XML/SGN для ручного прогона этапов Контур.
        /// </summary>
        /// <returns>Абсолютный путь к App_Data\\Temp\\KonturEtrn.</returns>
        /// <remarks>Папка единая для runner и ручного выбора, чтобы исключить расхождения по окружению.</remarks>
        private string GetKonturServerFilesDirectory()
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(baseDirectory, "App_Data", "Temp", "KonturEtrn");
        }

        /// <summary>
        /// Возвращает tis_entity_id по timeline из таблицы epd_timeline.
        /// </summary>
        /// <param name="timelineId">Идентификатор документа в timeline ТИС.</param>
        /// <returns>Значение tis_entity_id или пустая строка, если запись не найдена.</returns>
        /// <remarks>Идентификатор нужен для вызова штатного T1 builder, работающего от сущности заявки.</remarks>
        private string GetTisEntityIdByTimeline(long timelineId)
        {
            using (var connection = new SqlConnection(Connection.conStr()))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT TOP (1) tis_entity_id
FROM Perdoc.dbo.epd_timeline WITH (NOLOCK)
WHERE id = @timelineId;";
                command.Parameters.AddWithValue("@timelineId", timelineId);
                connection.Open();
                var value = command.ExecuteScalar();
                return value == null || value == DBNull.Value ? string.Empty : value.ToString().Trim();
            }
        }

        /// <summary>
        /// Формирует имя временного исходного XML для титула T1 перед контурной нормализацией участников.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline текущего прогона.</param>
        /// <param name="tisEntityId">Идентификатор сущности ТИС, использованный при сборке.</param>
        /// <returns>Имя временного исходного XML-файла для папки App_Data\Temp\KonturEtrn.</returns>
        /// <remarks>
        /// Исходный файл нужен только как промежуточный шаг до применения override участников.
        /// После нормализации в рабочем слое остается только один актуальный t1_override.
        /// </remarks>
        private string BuildT1ServerFileName(long timelineId, string tisEntityId)
        {
            return string.Format("t1_source_timeline{0}.xml", timelineId);
        }

        /// <summary>
        /// Формирует имя актуального серверного XML для титула T2.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline текущего прогона.</param>
        /// <param name="idzak">Идентификатор заявки idzak, использованный при сборке T2.</param>
        /// <returns>Имя актуального XML-файла для папки App_Data\Temp\KonturEtrn.</returns>
        /// <remarks>Рабочий слой хранит только один текущий T2 XML, а история этапа остается в SQL-артефактах.</remarks>
        private string BuildT2ServerFileName(long timelineId, long idzak)
        {
            return CreateArtifactWorkspaceService().GetCurrentXmlFileName(timelineId, "T2");
        }

        /// <summary>
        /// Формирует имя актуального серверного XML для титула этапа.
        /// </summary>
        /// <param name="titleCode">Код титула T3 или T4.</param>
        /// <param name="timelineId">Идентификатор timeline.</param>
        /// <returns>Имя актуального XML-файла этапа.</returns>
        /// <remarks>Рабочий слой хранит только одну актуальную копию XML на этап и timeline.</remarks>
        private string BuildStageServerFileName(string titleCode, long timelineId)
        {
            return CreateArtifactWorkspaceService().GetCurrentXmlFileName(timelineId, titleCode);
        }

        /// <summary>
        /// Проверяет ролевые доступы и настройки для выбранного этапа без отправки документа.
        /// </summary>
        /// <param name="sender">Источник события кнопки проверки.</param>
        /// <param name="e">Аргументы события click.</param>
        /// <remarks>Метод используется как предчек на рабочем контуре перед реальным запуском этапа.</remarks>
        protected void btnCheckAccess_Click(object sender, EventArgs e)
        {
            try
            {
                var stageCode = (ddlStage.SelectedValue ?? string.Empty).Trim();
                var titleCode = StageToTitle(stageCode);
                var resolver = new KonturAccessResolver(
                    new KonturSettingsRepository(Connection.conStr()),
                    new KonturRoleAccessRepository(Connection.conStr()));
                var access = resolver.ResolveByTitle(titleCode);

                if (!access.IsReady)
                {
                    LogErr(
                        "Доступ для этапа не готов: " + stageCode + Environment.NewLine +
                        "title=" + Safe(access.TitleCode) + Environment.NewLine +
                        "role=" + Safe(access.SenderRole) + Environment.NewLine +
                        "reason=" + Safe(access.Message),
                        null);
                    return;
                }

                LogOk(
                    "Доступ для этапа готов: " + stageCode + Environment.NewLine +
                    "title=" + Safe(access.TitleCode) + Environment.NewLine +
                    "role=" + Safe(access.SenderRole) + Environment.NewLine +
                    "apiUrl=" + Safe(access.ApiUrl) + Environment.NewLine +
                    "senderBoxId=" + Safe(access.SenderBoxId));
            }
            catch (Exception ex)
            {
                LogErr("Ошибка проверки доступов", ex);
            }
        }

        /// <summary>
        /// Разрешает senderBoxId для текущего этапа через ролевой access resolver.
        /// </summary>
        /// <param name="stageCode">Код этапа UI.</param>
        /// <returns>SenderBoxId, разрешенный для текущего титула.</returns>
        /// <remarks>
        /// Новый send-path использует тот же источник role-based доступа, что и штатный предчек страницы,
        /// чтобы не разъезжаться по boxId между старым и реконструкционным контурами.
        /// </remarks>
        private string ResolveSenderBoxIdForStage(string stageCode)
        {
            var titleCode = StageToTitle(stageCode);
            var resolver = new KonturAccessResolver(
                new KonturSettingsRepository(Connection.conStr()),
                new KonturRoleAccessRepository(Connection.conStr()));
            var access = resolver.ResolveByTitle(titleCode);
            if (access == null || !access.IsReady)
            {
                throw new ApplicationException(
                    "Не удалось разрешить senderBoxId для этапа." + Environment.NewLine +
                    "stage=" + stageCode + Environment.NewLine +
                    "reason=" + Safe(access == null ? "AccessContextMissing" : access.Message));
            }

            return access.SenderBoxId;
        }

        /// <summary>
        /// Валидирует входные данные запуска этапа на уровне UI.
        /// </summary>
        /// <param name="stageCode">Код этапа запуска.</param>
        /// <param name="timelineId">Идентификатор timeline запуска.</param>
        /// <param name="xmlPath">Путь к XML текущего титула.</param>
        /// <param name="signaturePath">Путь к подписи для T2/T3/T4.</param>
        /// <remarks>Валидация вынесена отдельно для единообразной диагностики и снижения шума в основном обработчике.</remarks>
        private void ValidateStageInput(string stageCode, long timelineId, string xmlPath, string signaturePath)
        {
            var titleCode = StageToTitle(stageCode);
            var isT2 = string.Equals(stageCode, "T2", StringComparison.OrdinalIgnoreCase);
            var isT3 = string.Equals(stageCode, "T3", StringComparison.OrdinalIgnoreCase);
            var isT4 = string.Equals(stageCode, "T4", StringComparison.OrdinalIgnoreCase);
            if (ShouldUseArtifactExecution(timelineId, stageCode))
            {
                return;
            }

            if (string.IsNullOrEmpty(xmlPath))
            {
                throw new ApplicationException("Для этого этапа требуется загрузить XML титула.");
            }

            if (!File.Exists(xmlPath))
            {
                throw new ApplicationException("XML-файл не найден: " + xmlPath);
            }

            if (!IsStageServerFile(Path.GetFileName(xmlPath), titleCode))
            {
                throw new ApplicationException(
                    "Выбранный XML не относится к текущему этапу " + titleCode + "." + Environment.NewLine +
                    "xmlFile=" + Path.GetFileName(xmlPath) + Environment.NewLine +
                    "action=Выберите XML этого же этапа в верхнем списке файлов.");
            }

            if (isT2 || isT3 || isT4)
            {
                if (string.IsNullOrEmpty(signaturePath))
                {
                    throw new ApplicationException("Для этого этапа требуется загрузить .sgn подпись.");
                }
            }

            if (!string.IsNullOrEmpty(signaturePath) && !File.Exists(signaturePath))
            {
                throw new ApplicationException("Файл подписи не найден: " + signaturePath);
            }

            if (!string.IsNullOrEmpty(signaturePath) && !IsStageServerFile(Path.GetFileName(signaturePath), titleCode))
            {
                throw new ApplicationException(
                    "Выбранный .sgn не относится к текущему этапу " + titleCode + "." + Environment.NewLine +
                    "sgnFile=" + Path.GetFileName(signaturePath) + Environment.NewLine +
                    "action=Выберите подпись этого же этапа в верхнем списке файлов.");
            }

            // timelineId пока сохраняется в сигнатуре метода, чтобы не ломать внешние вызовы в code-behind.
        }

        /// <summary>
        /// Проверяет, что файл относится к ожидаемому этапу T1/T2/T3/T4.
        /// </summary>
        /// <param name="fileName">Имя файла без пути.</param>
        /// <param name="titleCode">Код титула текущего этапа.</param>
        /// <returns>True, если файл совпадает с ожидаемым этапом по имени.</returns>
        /// <remarks>
        /// Проверка защищает ручной сценарий от глобальных выпадающих списков XML/SGN,
        /// где после предыдущего прогона мог остаться файл другого этапа.
        /// </remarks>
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
                case "T3":
                    return lower.StartsWith("t3_");
                case "T4":
                    return lower.StartsWith("t4_");
                default:
                    return false;
            }
        }


        /// <summary>
        /// Преобразует код этапа UI в код титула ЭТрН.
        /// </summary>
        /// <param name="stageCode">Код этапа UI.</param>
        /// <returns>Код титула T1/T2/T3/T4.</returns>
        /// <remarks>Метод нужен для резолвера доступа, который работает по титулу.</remarks>
        private string StageToTitle(string stageCode)
        {
            if (string.Equals(stageCode, "T1_INITIAL", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stageCode, "T1_DRAFT", StringComparison.OrdinalIgnoreCase))
            {
                return "T1";
            }

            if (string.Equals(stageCode, "T2", StringComparison.OrdinalIgnoreCase))
            {
                return "T2";
            }

            if (string.Equals(stageCode, "T3", StringComparison.OrdinalIgnoreCase))
            {
                return "T3";
            }

            if (string.Equals(stageCode, "T4", StringComparison.OrdinalIgnoreCase))
            {
                return "T4";
            }

            return stageCode;
        }

        /// <summary>
        /// Разбирает TimelineId из текстового поля.
        /// </summary>
        /// <param name="value">Текстовое значение TimelineId из UI.</param>
        /// <returns>Положительный числовой идентификатор timeline.</returns>
        /// <remarks>Метод выбрасывает исключение при пустом или некорректном значении.</remarks>
        private long ParseTimelineId(string value)
        {
            long timelineId;
            if (!long.TryParse(value, out timelineId) || timelineId <= 0)
            {
                throw new ApplicationException("Некорректный TimelineId.");
            }

            return timelineId;
        }

        /// <summary>
        /// Добавляет в лог страницы сообщение об успешной операции.
        /// </summary>
        /// <param name="message">Текст сообщения для пользователя.</param>
        /// <remarks>Текст экранируется перед выводом в HTML.</remarks>
        private void LogOk(string message)
        {
            litLog.Text = "<div class='ok'>" + Server.HtmlEncode(message) + "</div>" + litLog.Text;
        }

        /// <summary>
        /// Добавляет в лог страницы сообщение об ошибке.
        /// </summary>
        /// <param name="message">Краткое описание ошибки.</param>
        /// <param name="exception">Исключение с деталями, если доступно.</param>
        /// <remarks>Полный текст исключения выводится для ускорения диагностики на тестовом сервере.</remarks>
        private void LogErr(string message, Exception exception)
        {
            var fullText = message + (exception != null ? (Environment.NewLine + exception) : string.Empty);
            litLog.Text = "<div class='err'>" + Server.HtmlEncode(fullText) + "</div>" + litLog.Text;
        }

        /// <summary>
        /// Добавляет в лог страницы диагностику последнего операторного response из raw-лога.
        /// </summary>
        /// <param name="timelineId">Идентификатор документа в timeline ТИС.</param>
        /// <param name="stageCode">Код запущенного этапа для пояснения в UI.</param>
        /// <remarks>
        /// Метод не заменяет ошибку выполнения, а дает рядом готовую расшифровку ответа Контур API.
        /// Основная цель — убрать необходимость ручного разбора JSON в SSMS.
        /// </remarks>
        private void AppendLastApiDiagnostics(long timelineId, string stageCode)
        {
            var repository = new KonturRawLogRepository(Connection.conStr());
            var logStage = NormalizeStageForLog(stageCode);
            var lastResponse = repository.GetLastResponseLog(timelineId, "Kontur", logStage);
            if (lastResponse == null)
            {
                LogOk(
                    "API-диагностика (" + stageCode + "):" + Environment.NewLine +
                    "response-лог по timeline пока отсутствует.");
                return;
            }

            var runId = ExtractRunId(lastResponse.SanitizedPayload);
            var traceId = ExtractJsonValue(lastResponse.SanitizedPayload, "traceId");
            var errorCode = ExtractJsonValue(lastResponse.SanitizedPayload, "code");
            var errorMessage = ExtractJsonValue(lastResponse.SanitizedPayload, "message");

            var diagnosticText =
                "API-диагностика (" + stageCode + "):" + Environment.NewLine +
                "rawLogId=" + lastResponse.Id + Environment.NewLine +
                "endpoint=" + Safe(lastResponse.Endpoint) + Environment.NewLine +
                "httpStatus=" + (lastResponse.HttpStatus.HasValue ? lastResponse.HttpStatus.Value.ToString() : "<null>") + Environment.NewLine +
                "runId=" + Safe(runId) + Environment.NewLine +
                "traceId=" + Safe(traceId) + Environment.NewLine +
                "error.code=" + Safe(errorCode) + Environment.NewLine +
                "error.message=" + Safe(errorMessage);

            if ((lastResponse.HttpStatus ?? 0) >= 400)
            {
                litLog.Text = "<div class='err'>" + Server.HtmlEncode(diagnosticText) + "</div>" + litLog.Text;
                return;
            }

            litLog.Text = "<div class='ok'>" + Server.HtmlEncode(diagnosticText) + "</div>" + litLog.Text;
        }

        /// <summary>
        /// Безопасно добавляет API-диагностику в блоке catch без выбрасывания новых исключений.
        /// </summary>
        /// <param name="timelineText">Текстовое значение timeline из UI.</param>
        /// <param name="stageCode">Код этапа из UI.</param>
        /// <remarks>Метод изолирует вторичную диагностику, чтобы не терять основную ошибку выполнения.</remarks>
        private void TryAppendDiagnosticsSafe(string timelineText, string stageCode)
        {
            try
            {
                long timelineId;
                if (!long.TryParse(timelineText, out timelineId) || timelineId <= 0)
                {
                    return;
                }

                AppendLastApiDiagnostics(timelineId, stageCode);
            }
            catch
            {
                // Ошибки secondary-диагностики осознанно подавляются, чтобы не перекрыть первичную бизнес-ошибку.
            }
        }

        /// <summary>
        /// Извлекает строковое значение JSON-поля из sanitized payload.
        /// </summary>
        /// <param name="json">JSON-строка ответа Контур API.</param>
        /// <param name="fieldName">Имя поля для поиска.</param>
        /// <returns>Найденное значение или пустая строка.</returns>
        /// <remarks>
        /// Сначала используется JavaScriptSerializer, затем regex-fallback для кривых/усеченных payload.
        /// Это повышает устойчивость диагностики на старом стеке .NET Framework 4.0.
        /// </remarks>
        private string ExtractJsonValue(string json, string fieldName)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(fieldName))
            {
                return string.Empty;
            }

            try
            {
                var serializer = new JavaScriptSerializer();
                var root = serializer.DeserializeObject(json) as IDictionary;
                if (root != null)
                {
                    var fromRoot = GetFieldFromNestedDictionary(root, fieldName);
                    if (!string.IsNullOrEmpty(fromRoot))
                    {
                        return fromRoot;
                    }
                }
            }
            catch
            {
                // Переходим к regex-fallback без прерывания UI.
            }

            var pattern = "\"" + Regex.Escape(fieldName) + "\"\\s*:\\s*\"([^\"]*)\"";
            var match = Regex.Match(json, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        /// <summary>
        /// Извлекает runId из префикса sanitized payload.
        /// </summary>
        /// <param name="sanitizedPayload">Санитизированный payload из raw-лога.</param>
        /// <returns>runId попытки или пустая строка.</returns>
        /// <remarks>runId позволяет связать request и response одной попытки в массовых прогонах.</remarks>
        private string ExtractRunId(string sanitizedPayload)
        {
            if (string.IsNullOrEmpty(sanitizedPayload))
            {
                return string.Empty;
            }

            var match = Regex.Match(sanitizedPayload, "runId=([a-fA-F0-9]{32})", RegexOptions.CultureInvariant);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        /// <summary>
        /// Нормализует код этапа UI в код этапа raw-лога.
        /// </summary>
        /// <param name="stageCode">Код этапа из UI.</param>
        /// <returns>Код этапа для фильтрации в Direction.</returns>
        /// <remarks>Метод нужен для совпадения с форматом Direction вида response:&lt;stageCode&gt;.</remarks>
        private string NormalizeStageForLog(string stageCode)
        {
            return string.IsNullOrEmpty(stageCode) ? string.Empty : stageCode.Trim().ToUpperInvariant();
        }

        /// <summary>
        /// Рекурсивно ищет поле в словаре, включая вложенные словари.
        /// </summary>
        /// <param name="dictionary">Текущий словарь для обхода.</param>
        /// <param name="fieldName">Искомое имя поля.</param>
        /// <returns>Найденное строковое значение или пустая строка.</returns>
        /// <remarks>Метод используется для поиска traceId и вложенных полей error.code/error.message.</remarks>
        private string GetFieldFromNestedDictionary(IDictionary dictionary, string fieldName)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                var key = Convert.ToString(entry.Key);
                if (string.Equals(key, fieldName, StringComparison.OrdinalIgnoreCase))
                {
                    return Convert.ToString(entry.Value);
                }

                var nested = entry.Value as IDictionary;
                if (nested != null)
                {
                    var nestedValue = GetFieldFromNestedDictionary(nested, fieldName);
                    if (!string.IsNullOrEmpty(nestedValue))
                    {
                        return nestedValue;
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Возвращает безопасное строковое представление для логов страницы.
        /// </summary>
        /// <param name="value">Исходное значение.</param>
        /// <returns>Непустое значение или маркер &lt;empty&gt;.</returns>
        /// <remarks>Метод используется для читаемого вывода диагностических блоков.</remarks>
        private string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? "<empty>" : value;
        }
    }
}

