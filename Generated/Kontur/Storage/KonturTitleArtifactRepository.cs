/*
  ФАЙЛ: KonturTitleArtifactRepository.cs
  НАЗНАЧЕНИЕ: SQL-хранилище XML и подписей титулов ЭТрН для интеграции Контур.
  Отделяет stage-runner от структуры таблиц Perdoc и от пользовательских файлов на диске.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  13.05.2026 - Первичное создание репозитория артефактов титулов ЭТрН.
  23.05.2026 - Добавлено сохранение актуального draft-артефакта и синхронизация подписи этапа без лишнего роста версий.
*/

using System;
using System.Data;
using System.Data.SqlClient;
using Tis.KonturIntegration.Models;

namespace Tis.KonturIntegration.Storage
{
    /// <summary>
    /// Репозиторий хранения артефактов титулов ЭТрН в SQL-границе Perdoc.
    /// Используется оркестратором Контур для повторяемого запуска этапов без ручных XML/SGN-файлов.
    /// </summary>
    public class KonturTitleArtifactRepository
    {
        /// <summary>
        /// Инициализирует репозиторий строкой подключения к базе ТИС.
        /// </summary>
        /// <param name="connectionString">Строка подключения к SQL Server, где доступна схема Perdoc.</param>
        /// <remarks>Репозиторий не создает подключение заранее и открывает его только на время операции.</remarks>
        public KonturTitleArtifactRepository(string connectionString)
        {
            ConnectionString = connectionString;
        }

        /// <summary>
        /// Получает строку подключения, используемую для операций с артефактами.
        /// </summary>
        public string ConnectionString { get; private set; }

        /// <summary>
        /// Сохраняет новую версию артефакта титула.
        /// </summary>
        /// <param name="artifact">Артефакт с XML, подписью и техническими признаками подписанта.</param>
        /// <returns>Идентификатор созданной записи.</returns>
        /// <remarks>Метод всегда добавляет новую версию, чтобы предыдущие попытки можно было диагностировать и повторить.</remarks>
        public long Insert(KonturTitleArtifact artifact)
        {
            if (artifact == null)
            {
                throw new ArgumentNullException("artifact");
            }

            using (var cn = new SqlConnection(ConnectionString))
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"
DECLARE @versionNo int;

SELECT @versionNo = ISNULL(MAX(VersionNo), 0) + 1
  FROM Perdoc.dbo.TEpdTitleArtifact
 WHERE TimelineId = @TimelineId
   AND TitleCode = @TitleCode;

INSERT INTO Perdoc.dbo.TEpdTitleArtifact
(
    TimelineId,
    TitleCode,
    VersionNo,
    XmlFileName,
    TitleXml,
    SignatureFileName,
    TitleSgn,
    Thumbprint,
    SignerRole,
    SignedAt,
    CreatedAt
)
VALUES
(
    @TimelineId,
    @TitleCode,
    @versionNo,
    @XmlFileName,
    @TitleXml,
    @SignatureFileName,
    @TitleSgn,
    @Thumbprint,
    @SignerRole,
    @SignedAt,
    GETDATE()
);

SELECT CAST(SCOPE_IDENTITY() AS bigint);";

                AddCommonParameters(cmd, artifact);

                cn.Open();
                var value = cmd.ExecuteScalar();
                return value == null || value == DBNull.Value ? 0 : Convert.ToInt64(value);
            }
        }

        /// <summary>
        /// Сохраняет актуальный черновой XML-артефакт этапа без бесконтрольного роста версий.
        /// </summary>
        /// <param name="artifact">Артефакт с XML текущего этапа.</param>
        /// <returns>Идентификатор актуальной записи артефакта.</returns>
        /// <remarks>
        /// Если последняя версия этапа еще не подписана, XML обновляется в той же записи.
        /// Если последняя версия уже подписана, создается новая версия, чтобы сохранить историю фактического документооборота.
        /// </remarks>
        public long SaveDraftArtifact(KonturTitleArtifact artifact)
        {
            if (artifact == null)
            {
                throw new ArgumentNullException("artifact");
            }

            var latest = GetLatest(artifact.TimelineId, artifact.TitleCode);
            if (latest != null && !latest.HasSignature)
            {
                UpdateDraftArtifact(latest.Id, artifact);
                return latest.Id;
            }

            return Insert(artifact);
        }

        /// <summary>
        /// Сохраняет detached-подпись для актуального артефакта этапа.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="titleCode">Код титула T1/T2/T3/T4.</param>
        /// <param name="signatureFileName">Имя файла подписи из рабочего слоя.</param>
        /// <param name="titleSgn">Байты detached-подписи.</param>
        /// <param name="thumbprint">Отпечаток сертификата, если его удалось определить.</param>
        /// <param name="signerRole">Роль подписанта этапа для диагностических сценариев.</param>
        /// <param name="signedAt">Момент подписи, если он известен.</param>
        /// <returns>Идентификатор записи, в которую сохранена подпись.</returns>
        /// <remarks>
        /// Подпись добавляется к последнему неподписанному XML этапа. Если такой записи нет,
        /// создается новая версия на основе последнего XML-артефакта, чтобы не терять историю прохождения.
        /// </remarks>
        public long SaveSignature(long timelineId, string titleCode, string signatureFileName, byte[] titleSgn, string thumbprint, string signerRole, DateTime? signedAt)
        {
            if (timelineId <= 0)
            {
                throw new ArgumentOutOfRangeException("timelineId");
            }

            if (titleSgn == null || titleSgn.Length == 0)
            {
                throw new ArgumentNullException("titleSgn");
            }

            var latest = GetLatest(timelineId, titleCode);
            if (latest != null && !latest.HasSignature)
            {
                UpdateSignature(latest.Id, signatureFileName, titleSgn, thumbprint, signerRole, signedAt);
                return latest.Id;
            }

            var artifact = new KonturTitleArtifact
            {
                TimelineId = timelineId,
                TitleCode = titleCode,
                XmlFileName = latest == null ? string.Empty : latest.XmlFileName,
                TitleXml = latest == null ? null : latest.TitleXml,
                SignatureFileName = signatureFileName,
                TitleSgn = titleSgn,
                Thumbprint = thumbprint,
                SignerRole = signerRole,
                SignedAt = signedAt
            };

            return Insert(artifact);
        }

        /// <summary>
        /// Возвращает последнюю сохраненную версию артефакта титула.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа в ТИС.</param>
        /// <param name="titleCode">Код титула: T1, T2, T3 или T4.</param>
        /// <returns>Последний артефакт или null, если данных нет.</returns>
        /// <remarks>Метод используется как источник для повторного запуска этапа без ручного выбора файлов.</remarks>
        public KonturTitleArtifact GetLatest(long timelineId, string titleCode)
        {
            using (var cn = new SqlConnection(ConnectionString))
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"
SELECT TOP (1)
       Id,
       TimelineId,
       TitleCode,
       XmlFileName,
       TitleXml,
       SignatureFileName,
       TitleSgn,
       Thumbprint,
       SignerRole,
       SignedAt
  FROM Perdoc.dbo.TEpdTitleArtifact
 WHERE TimelineId = @TimelineId
   AND TitleCode = @TitleCode
 ORDER BY VersionNo DESC, Id DESC;";

                cmd.Parameters.Add("@TimelineId", SqlDbType.BigInt).Value = timelineId;
                cmd.Parameters.Add("@TitleCode", SqlDbType.NVarChar, 10).Value = NormalizeTitleCode(titleCode);

                cn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return null;
                    }

                    return ReadArtifact(reader);
                }
            }
        }

        /// <summary>
        /// Обновляет XML и технические поля последнего неподписанного артефакта.
        /// </summary>
        /// <param name="artifactId">Идентификатор обновляемого артефакта.</param>
        /// <param name="artifact">Новый черновой артефакт этапа.</param>
        /// <remarks>
        /// Обновление используется только для неподписанных записей, чтобы не размножать версии
        /// при повторной генерации XML до фактического подписания и отправки этапа.
        /// </remarks>
        private void UpdateDraftArtifact(long artifactId, KonturTitleArtifact artifact)
        {
            using (var cn = new SqlConnection(ConnectionString))
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"
UPDATE Perdoc.dbo.TEpdTitleArtifact
   SET XmlFileName = @XmlFileName,
       TitleXml = @TitleXml,
       SignatureFileName = @SignatureFileName,
       TitleSgn = @TitleSgn,
       Thumbprint = @Thumbprint,
       SignerRole = @SignerRole,
       SignedAt = @SignedAt
 WHERE Id = @Id;";

                AddCommonParameters(cmd, artifact);
                cmd.Parameters.Add("@Id", SqlDbType.BigInt).Value = artifactId;

                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Обновляет поля подписи в существующем артефакте этапа.
        /// </summary>
        /// <param name="artifactId">Идентификатор обновляемого артефакта.</param>
        /// <param name="signatureFileName">Имя файла подписи.</param>
        /// <param name="titleSgn">Байты detached-подписи.</param>
        /// <param name="thumbprint">Отпечаток сертификата.</param>
        /// <param name="signerRole">Роль подписанта этапа.</param>
        /// <param name="signedAt">Момент подписи.</param>
        /// <remarks>Метод не трогает XML титула, потому что подпись должна дополнять уже зафиксированный артефакт этапа.</remarks>
        private void UpdateSignature(long artifactId, string signatureFileName, byte[] titleSgn, string thumbprint, string signerRole, DateTime? signedAt)
        {
            using (var cn = new SqlConnection(ConnectionString))
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"
UPDATE Perdoc.dbo.TEpdTitleArtifact
   SET SignatureFileName = @SignatureFileName,
       TitleSgn = @TitleSgn,
       Thumbprint = @Thumbprint,
       SignerRole = @SignerRole,
       SignedAt = @SignedAt
 WHERE Id = @Id;";

                cmd.Parameters.Add("@SignatureFileName", SqlDbType.NVarChar, 260).Value = ToDbString(signatureFileName);
                cmd.Parameters.Add("@TitleSgn", SqlDbType.VarBinary, -1).Value = ToDbBytes(titleSgn);
                cmd.Parameters.Add("@Thumbprint", SqlDbType.NVarChar, 100).Value = ToDbString(thumbprint);
                cmd.Parameters.Add("@SignerRole", SqlDbType.NVarChar, 50).Value = ToDbString(signerRole);
                cmd.Parameters.Add("@SignedAt", SqlDbType.DateTime).Value = signedAt.HasValue ? (object)signedAt.Value : DBNull.Value;
                cmd.Parameters.Add("@Id", SqlDbType.BigInt).Value = artifactId;

                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Добавляет параметры сохранения артефакта к SQL-команде.
        /// </summary>
        /// <param name="cmd">SQL-команда вставки новой версии.</param>
        /// <param name="artifact">Артефакт титула ЭТрН.</param>
        /// <remarks>Метод централизует DBNull-маппинг, чтобы не размазывать правила SQL по репозиторию.</remarks>
        private void AddCommonParameters(SqlCommand cmd, KonturTitleArtifact artifact)
        {
            cmd.Parameters.Add("@TimelineId", SqlDbType.BigInt).Value = artifact.TimelineId;
            cmd.Parameters.Add("@TitleCode", SqlDbType.NVarChar, 10).Value = NormalizeTitleCode(artifact.TitleCode);
            cmd.Parameters.Add("@XmlFileName", SqlDbType.NVarChar, 260).Value = ToDbString(artifact.XmlFileName);
            cmd.Parameters.Add("@TitleXml", SqlDbType.VarBinary, -1).Value = ToDbBytes(artifact.TitleXml);
            cmd.Parameters.Add("@SignatureFileName", SqlDbType.NVarChar, 260).Value = ToDbString(artifact.SignatureFileName);
            cmd.Parameters.Add("@TitleSgn", SqlDbType.VarBinary, -1).Value = ToDbBytes(artifact.TitleSgn);
            cmd.Parameters.Add("@Thumbprint", SqlDbType.NVarChar, 100).Value = ToDbString(artifact.Thumbprint);
            cmd.Parameters.Add("@SignerRole", SqlDbType.NVarChar, 50).Value = ToDbString(artifact.SignerRole);
            cmd.Parameters.Add("@SignedAt", SqlDbType.DateTime).Value = artifact.SignedAt.HasValue ? (object)artifact.SignedAt.Value : DBNull.Value;
        }

        /// <summary>
        /// Читает артефакт из текущей строки SQL reader.
        /// </summary>
        /// <param name="reader">Reader, установленный на строку артефакта.</param>
        /// <returns>Заполненная модель артефакта.</returns>
        /// <remarks>Метод не двигает reader дальше текущей строки.</remarks>
        private KonturTitleArtifact ReadArtifact(SqlDataReader reader)
        {
            return new KonturTitleArtifact
            {
                Id = reader.GetInt64(0),
                TimelineId = reader.GetInt64(1),
                TitleCode = reader.GetString(2),
                XmlFileName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                TitleXml = reader.IsDBNull(4) ? null : (byte[])reader[4],
                SignatureFileName = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                TitleSgn = reader.IsDBNull(6) ? null : (byte[])reader[6],
                Thumbprint = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                SignerRole = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                SignedAt = reader.IsDBNull(9) ? (DateTime?)null : reader.GetDateTime(9)
            };
        }

        /// <summary>
        /// Нормализует код титула для ключа хранения.
        /// </summary>
        /// <param name="titleCode">Исходный код титула.</param>
        /// <returns>Код титула в верхнем регистре или UNKNOWN.</returns>
        /// <remarks>Нормализация исключает расхождение ключей T3/t3 при повторных запусках.</remarks>
        private string NormalizeTitleCode(string titleCode)
        {
            return string.IsNullOrEmpty(titleCode) ? "UNKNOWN" : titleCode.Trim().ToUpperInvariant();
        }

        /// <summary>
        /// Преобразует строку в значение SQL-параметра.
        /// </summary>
        /// <param name="value">Исходная строка.</param>
        /// <returns>Строка или DBNull.Value.</returns>
        /// <remarks>Пустые строки в технических полях хранятся как NULL для упрощения фильтрации.</remarks>
        private object ToDbString(string value)
        {
            return string.IsNullOrEmpty(value) ? (object)DBNull.Value : value;
        }

        /// <summary>
        /// Преобразует байтовый массив в значение SQL-параметра.
        /// </summary>
        /// <param name="value">Исходные байты артефакта.</param>
        /// <returns>Байты или DBNull.Value.</returns>
        /// <remarks>Пустой массив не сохраняется как содержательный артефакт.</remarks>
        private object ToDbBytes(byte[] value)
        {
            return value == null || value.Length == 0 ? (object)DBNull.Value : value;
        }
    }
}