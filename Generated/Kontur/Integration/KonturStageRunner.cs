/*
  ФАЙЛ: KonturStageRunner.cs
  НАЗНАЧЕНИЕ: Единый конвейер запуска этапа ЭТрН Контур по внутренним артефактам ТИС.
  Выполняет цепочку: собрать XML, получить подпись, проверить подпись, сохранить артефакт и отправить этап.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  13.05.2026 - Первичное создание stage-runner и подключение сценария T3 по внутренним артефактам.
  13.05.2026 - Добавлены сценарии T2/T4 по внутренним артефактам и подписи из ТИС.
  14.05.2026 - Убрана зависимость от системного temp-корня: временные XML/SGN перенесены в App_Data\Temp\KonturEtrn.
  23.05.2026 - Builder этапов переведен на общий фасад нормализации XML T1-T4.
*/

using System;
using System.IO;
using Tis.KonturIntegration.Models;
using Tis.KonturIntegration.Storage;

namespace Tis.KonturIntegration.Integration
{
    /// <summary>
    /// Оркестрирует внутренний запуск этапа ЭТрН через Контур без ручной передачи XML/SGN-файлов пользователем.
    /// Используется как верхний сценарий над builder, signature service, artifact storage и operator adapter.
    /// </summary>
    public class KonturStageRunner
    {
        /// <summary>
        /// Инициализирует stage-runner строкой подключения к ТИС.
        /// </summary>
        /// <param name="connectionString">Строка подключения к базе ТИС.</param>
        /// <remarks>Внутренние зависимости собираются здесь как composition root для интеграционного контура Контур.</remarks>
        public KonturStageRunner(string connectionString)
        {
            ConnectionString = connectionString;
            ArtifactRepository = new KonturTitleArtifactRepository(connectionString);
            TitleBuilder = new KonturTitleBuilder(ArtifactRepository, connectionString);
            SignatureService = new KonturSignatureService(ArtifactRepository);
        }

        /// <summary>
        /// Получает строку подключения к ТИС.
        /// </summary>
        public string ConnectionString { get; private set; }

        /// <summary>
        /// Получает builder XML титулов.
        /// </summary>
        public IKonturTitleBuilder TitleBuilder { get; private set; }

        /// <summary>
        /// Получает сервис получения и проверки подписи.
        /// </summary>
        public IKonturSignatureService SignatureService { get; private set; }

        /// <summary>
        /// Получает репозиторий артефактов титулов.
        /// </summary>
        public KonturTitleArtifactRepository ArtifactRepository { get; private set; }

        /// <summary>
        /// ��������� ���� T1 initial �� ����������� ��������� �����.
        /// </summary>
        /// <param name="timelineId">������������� timeline ���������.</param>
        /// <param name="tisEntityId">������������� �������� ���; ��� ��������� ������� ����� ���� ������.</param>
        /// <returns>��������������� ��������� ����� ��� UI.</returns>
        /// <remarks>��������� �������� ��� ���������� ��������� T1, ����� XML �� ������������� ����� �������� � ���������.</remarks>
        public KonturStageExecutionResult ExecuteT1Initial(long timelineId, string tisEntityId)
        {
            var buildResult = TitleBuilder.Build(timelineId, "T1", tisEntityId);
            if (!buildResult.IsSuccess || buildResult.Artifact == null)
            {
                return Fail("T1_INITIAL", timelineId, buildResult.Message);
            }

            return ExecuteWithTempXmlFile("T1_INITIAL", timelineId, buildResult.Artifact, new KonturT1Service(ConnectionString).Execute);
        }

        /// <summary>
        /// ��������� ���� T1 draft �� ����������� ��������� �����.
        /// </summary>
        /// <param name="timelineId">������������� timeline ���������.</param>
        /// <param name="tisEntityId">������������� �������� ���; ��� ��������� ������� ����� ���� ������.</param>
        /// <returns>��������������� ��������� ����� ��� UI.</returns>
        /// <remarks>���� ���������� ��� �� XML-�������� T1, ��� � ���� �������, ��� ��������� ����������� �����.</remarks>
        public KonturStageExecutionResult ExecuteT1Draft(long timelineId, string tisEntityId)
        {
            var buildResult = TitleBuilder.Build(timelineId, "T1", tisEntityId);
            if (!buildResult.IsSuccess || buildResult.Artifact == null)
            {
                return Fail("T1_DRAFT", timelineId, buildResult.Message);
            }

            return ExecuteWithTempXmlFile("T1_DRAFT", timelineId, buildResult.Artifact, new KonturT1Service(ConnectionString).ExecuteDraft);
        }

        /// <summary>
        /// Запускает этап T3 по внутренним артефактам ТИС.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="tisEntityId">Идентификатор сущности ТИС; для T3 может быть пустым, если XML уже сохранен.</param>
        /// <param name="signaturePath">Необязательный путь к подписи для совместимости с ручным запуском.</param>
        /// <returns>Унифицированный результат этапа для UI.</returns>
        /// <remarks>Метод сначала использует сохраненный T3 XML, затем подпись из артефакта или legacy-хранилища.</remarks>
        public KonturStageExecutionResult ExecuteT3(long timelineId, string tisEntityId, string signaturePath)
        {
            var buildResult = TitleBuilder.Build(timelineId, "T3", tisEntityId);
            if (!buildResult.IsSuccess || buildResult.Artifact == null)
            {
                return Fail("T3", timelineId, buildResult.Message);
            }

            var artifact = buildResult.Artifact;
            var signatureResult = SignatureService.Resolve(timelineId, "T3", artifact.TitleXml, signaturePath);
            if (!signatureResult.IsSuccess)
            {
                return Fail("T3", timelineId, signatureResult.Message);
            }

            // Сохраняем скомпонованный XML+SGN перед отправкой, чтобы повторный запуск не зависел от UI и файловой системы.
            artifact.TitleSgn = signatureResult.SignatureBytes;
            artifact.SignatureFileName = signatureResult.SignatureFileName;
            artifact.Thumbprint = signatureResult.Thumbprint;
            artifact.SignerRole = signatureResult.SignerRole;
            artifact.SignedAt = DateTime.Now;
            ArtifactRepository.Insert(artifact);

            var result = new KonturT3Service(ConnectionString).ExecuteArtifact(timelineId, artifact);
            return new KonturStageExecutionResult
            {
                IsSuccess = result != null && result.IsSuccess,
                StageCode = "T3",
                TimelineId = result != null ? result.TimelineId : timelineId,
                TransportationId = result != null ? result.TransportationId : string.Empty,
                TitleId = result != null ? result.TitleId : string.Empty,
                Message = result != null ? result.Message : "EmptyT3Result"
            };
        }

        /// <summary>
        /// Запускает этап T2 по внутренним артефактам ТИС.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="tisEntityId">Идентификатор сущности ТИС; для T2 может быть пустым.</param>
        /// <param name="signaturePath">Необязательный путь к подписи для совместимости с ручным запуском.</param>
        /// <returns>Унифицированный результат этапа для UI.</returns>
        /// <remarks>XML собирается из текущего ТИС-контура, подпись берется из артефакта или legacy-хранилища.</remarks>
        public KonturStageExecutionResult ExecuteT2(long timelineId, string tisEntityId, string signaturePath)
        {
            var buildResult = TitleBuilder.Build(timelineId, "T2", tisEntityId);
            if (!buildResult.IsSuccess || buildResult.Artifact == null)
            {
                return Fail("T2", timelineId, buildResult.Message);
            }

            var artifact = buildResult.Artifact;
            var signatureResult = SignatureService.Resolve(timelineId, "T2", artifact.TitleXml, signaturePath);
            if (!signatureResult.IsSuccess)
            {
                return Fail("T2", timelineId, signatureResult.Message);
            }

            artifact.TitleSgn = signatureResult.SignatureBytes;
            artifact.SignatureFileName = signatureResult.SignatureFileName;
            artifact.Thumbprint = signatureResult.Thumbprint;
            artifact.SignerRole = signatureResult.SignerRole;
            artifact.SignedAt = DateTime.Now;
            ArtifactRepository.Insert(artifact);

            return ExecuteWithTempFiles("T2", timelineId, artifact, new KonturT2Service(ConnectionString).Execute);
        }

        /// <summary>
        /// Запускает этап T4 по внутренним артефактам ТИС.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="tisEntityId">Идентификатор сущности ТИС; для T4 может быть пустым.</param>
        /// <param name="signaturePath">Необязательный путь к подписи для совместимости с ручным запуском.</param>
        /// <returns>Унифицированный результат этапа для UI.</returns>
        /// <remarks>На текущем этапе T4 читается из хранилища артефактов до выделения отдельного XML-builder.</remarks>
        public KonturStageExecutionResult ExecuteT4(long timelineId, string tisEntityId, string signaturePath)
        {
            var buildResult = TitleBuilder.Build(timelineId, "T4", tisEntityId);
            if (!buildResult.IsSuccess || buildResult.Artifact == null)
            {
                return Fail("T4", timelineId, buildResult.Message);
            }

            var artifact = buildResult.Artifact;
            var signatureResult = SignatureService.Resolve(timelineId, "T4", artifact.TitleXml, signaturePath);
            if (!signatureResult.IsSuccess)
            {
                return Fail("T4", timelineId, signatureResult.Message);
            }

            artifact.TitleSgn = signatureResult.SignatureBytes;
            artifact.SignatureFileName = signatureResult.SignatureFileName;
            artifact.Thumbprint = signatureResult.Thumbprint;
            artifact.SignerRole = signatureResult.SignerRole;
            artifact.SignedAt = DateTime.Now;
            ArtifactRepository.Insert(artifact);

            return ExecuteWithTempFiles("T4", timelineId, artifact, new KonturT4Service(ConnectionString).Execute);
        }

        /// <summary>
        /// Выполняет этап через временные файлы XML/SGN и очищает их после завершения.
        /// </summary>
        /// <param name="stageCode">Код этапа.</param>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="artifact">Артефакт с XML и подписью.</param>
        /// <param name="executor">Функция выполнения этапа по путям к файлам.</param>
        /// <returns>Унифицированный результат этапа.</returns>
        /// <remarks>Временные файлы используются только как транспорт между legacy-сервисами без пользовательского ввода путей.</remarks>
        /// <summary>
        /// ��������� ���� ����� ��������� XML-���� ��� detached-�������.
        /// </summary>
        /// <param name="stageCode">��� �����.</param>
        /// <param name="timelineId">������������� timeline ���������.</param>
        /// <param name="artifact">�������� � XML ������.</param>
        /// <param name="executor">������� ���������� ����� �� ���� � XML.</param>
        /// <returns>��������������� ��������� �����.</returns>
        /// <remarks>����� ������������ ��� T1, ��� ������� ���������� ��������� �������� � �� ��������� � multipart-������� �����.</remarks>
        private KonturStageExecutionResult ExecuteWithTempXmlFile(string stageCode, long timelineId, KonturTitleArtifact artifact, Func<long, string, dynamic> executor)
        {
            var xmlPath = string.Empty;
            try
            {
                var tempDirectory = GetKonturTempDirectory();
                xmlPath = Path.Combine(tempDirectory, Guid.NewGuid().ToString("N") + "_" + (string.IsNullOrEmpty(artifact.XmlFileName) ? stageCode.ToLowerInvariant() + ".xml" : artifact.XmlFileName));
                File.WriteAllBytes(xmlPath, artifact.TitleXml ?? new byte[0]);

                var result = executor(timelineId, xmlPath);
                return new KonturStageExecutionResult
                {
                    IsSuccess = result != null && result.IsSuccess,
                    StageCode = stageCode,
                    TimelineId = result != null ? result.TimelineId : timelineId,
                    TransportationId = result != null ? result.TransportationId : string.Empty,
                    TitleId = result != null ? result.TitleId : string.Empty,
                    Message = result != null ? result.Message : ("Empty" + stageCode + "Result")
                };
            }
            finally
            {
                TryDeleteFile(xmlPath);
            }
        }
        private KonturStageExecutionResult ExecuteWithTempFiles(string stageCode, long timelineId, KonturTitleArtifact artifact, Func<long, string, string, dynamic> executor)
        {
            var xmlPath = string.Empty;
            var sgnPath = string.Empty;
            try
            {
                var tempDirectory = GetKonturTempDirectory();
                xmlPath = Path.Combine(tempDirectory, Guid.NewGuid().ToString("N") + "_" + (string.IsNullOrEmpty(artifact.XmlFileName) ? stageCode.ToLowerInvariant() + ".xml" : artifact.XmlFileName));
                sgnPath = Path.Combine(tempDirectory, Guid.NewGuid().ToString("N") + "_" + (string.IsNullOrEmpty(artifact.SignatureFileName) ? stageCode.ToLowerInvariant() + ".sig" : artifact.SignatureFileName));

                File.WriteAllBytes(xmlPath, artifact.TitleXml ?? new byte[0]);
                File.WriteAllBytes(sgnPath, artifact.TitleSgn ?? new byte[0]);

                var result = executor(timelineId, xmlPath, sgnPath);
                return new KonturStageExecutionResult
                {
                    IsSuccess = result != null && result.IsSuccess,
                    StageCode = stageCode,
                    TimelineId = result != null ? result.TimelineId : timelineId,
                    TransportationId = result != null ? result.TransportationId : string.Empty,
                    TitleId = result != null ? result.TitleId : string.Empty,
                    Message = result != null ? result.Message : ("Empty" + stageCode + "Result")
                };
            }
            finally
            {
                TryDeleteFile(xmlPath);
                TryDeleteFile(sgnPath);
            }
        }

        /// <summary>
        /// Возвращает рабочую директорию временных файлов контура Контур.
        /// </summary>
        /// <returns>Абсолютный путь к директории для временных XML/SGN.</returns>
        /// <remarks>Приоритет: App_Data\Temp\KonturEtrn внутри сайта; fallback: системный temp-каталог процесса.</remarks>
        private string GetKonturTempDirectory()
        {
            try
            {
                var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                var appTempDirectory = Path.Combine(baseDirectory, "App_Data", "Temp", "KonturEtrn");
                Directory.CreateDirectory(appTempDirectory);
                return appTempDirectory;
            }
            catch
            {
                var fallbackDirectory = Path.Combine(Path.GetTempPath(), "TisKonturEtrn");
                Directory.CreateDirectory(fallbackDirectory);
                return fallbackDirectory;
            }
        }

        /// <summary>
        /// Пытается удалить временный файл без выброса исключений.
        /// </summary>
        /// <param name="path">Путь к файлу.</param>
        /// <remarks>Удаление в finally не должно перекрывать основной результат этапа.</remarks>
        private void TryDeleteFile(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Формирует неуспешный результат этапа.
        /// </summary>
        /// <param name="stageCode">Код этапа.</param>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="message">Причина остановки сценария.</param>
        /// <returns>Унифицированный результат этапа с ошибкой.</returns>
        /// <remarks>Единый формат нужен для WebForms UI и диагностических страниц.</remarks>
        private KonturStageExecutionResult Fail(string stageCode, long timelineId, string message)
        {
            return new KonturStageExecutionResult
            {
                IsSuccess = false,
                StageCode = stageCode,
                TimelineId = timelineId,
                Message = string.IsNullOrEmpty(message) ? "StageRunnerFailed" : message
            };
        }
    }
}
