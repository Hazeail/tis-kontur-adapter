/*
  ФАЙЛ: KonturStageSignerRepository.cs
  НАЗНАЧЕНИЕ: Хранилище контекста и выбора подписанта этапов ЭТрН Контур.
  Отделяет страницу KonturProbe от SQL-деталей получения кандидатов и сохранения выбора по timeline.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  22.05.2026 - Первичное создание репозитория выбора подписанта этапа.
*/

using System;
using System.Data;
using System.Data.SqlClient;
using Tis.KonturIntegration.Models;

namespace Tis.KonturIntegration.Storage
{
    /// <summary>
    /// Репозиторий работы с допустимыми подписантами и сохраненным выбором по этапам ЭТрН.
    /// </summary>
    public class KonturStageSignerRepository
    {
        /// <summary>
        /// Инициализирует репозиторий строкой подключения к ТИС.
        /// </summary>
        /// <param name="connectionString">Строка подключения к SQL Server со схемой Perdoc.</param>
        /// <remarks>Подключение открывается только на время отдельных операций чтения и записи.</remarks>
        public KonturStageSignerRepository(string connectionString)
        {
            ConnectionString = connectionString;
        }

        /// <summary>
        /// Получает строку подключения, используемую репозиторием.
        /// </summary>
        public string ConnectionString { get; private set; }

        /// <summary>
        /// Строит контекст выбора подписанта для указанного этапа.
        /// </summary>
        /// <param name="idzak">Идентификатор заявки ТИС.</param>
        /// <param name="titleCode">Код титула T1/T2/T3/T4.</param>
        /// <returns>Контекст роли этапа, организации и списка допустимых подписантов.</returns>
        /// <remarks>
        /// Контекст нужен, чтобы оператор видел именно тех кандидатов, которые могут подписывать
        /// выбранный этап за требуемую организацию без привязки к текущей сессии пользователя.
        /// </remarks>
        public KonturStageSignerContext GetContext(long idzak, string titleCode)
        {
            var normalizedTitleCode = NormalizeStageCode(titleCode);
            var context = new KonturStageSignerContext
            {
                TitleCode = normalizedTitleCode,
                RequiredRoleCode = ResolveRoleCode(normalizedTitleCode),
                RequiredRoleName = ResolveRoleName(normalizedTitleCode)
            };

            if (string.IsNullOrEmpty(context.RequiredRoleCode))
            {
                context.ErrorMessage = "Неизвестный код этапа для выбора подписанта: " + normalizedTitleCode;
                return context;
            }

            context.RequiredKontragentId = ResolveRequiredKontragentId(idzak, context.RequiredRoleCode);
            if (context.RequiredKontragentId <= 0)
            {
                context.ErrorMessage = string.Format(
                    "Не найдена организация роли {0} для заявки {1}.",
                    context.RequiredRoleName,
                    idzak);
                return context;
            }

            LoadRequiredKontragentInfo(context);
            LoadCandidates(context);
            return context;
        }

        /// <summary>
        /// Возвращает сохраненный выбор подписанта для этапа.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код этапа T1/T2/T3/T4.</param>
        /// <returns>Сохраненный выбор или null.</returns>
        /// <remarks>Используется для восстановления состояния страницы между postback и повторными заходами.</remarks>
        public KonturStageSignerSelection GetSelection(long timelineId, string stageCode)
        {
            const string sql = @"
SELECT TOP (1)
       Id,
       TimelineId,
       StageCode,
       SignerFizLicoId,
       UpdatedByUserId,
       UpdatedAt
  FROM Perdoc.dbo.TEpdStageSignerSelection
 WHERE TimelineId = @TimelineId
   AND StageCode = @StageCode;";

            using (var cn = new SqlConnection(ConnectionString))
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.Add("@TimelineId", SqlDbType.BigInt).Value = timelineId;
                cmd.Parameters.Add("@StageCode", SqlDbType.NVarChar, 16).Value = NormalizeStageCode(stageCode);
                cn.Open();

                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return null;
                    }

                    return new KonturStageSignerSelection
                    {
                        Id = reader.GetInt64(0),
                        TimelineId = reader.GetInt64(1),
                        StageCode = reader.GetString(2),
                        SignerFizLicoId = reader.GetInt64(3),
                        UpdatedByUserId = reader.IsDBNull(4) ? 0 : reader.GetInt64(4),
                        UpdatedAt = reader.GetDateTime(5)
                    };
                }
            }
        }

        /// <summary>
        /// Сохраняет выбор подписанта для этапа в Perdoc.
        /// </summary>
        /// <param name="selection">Выбор подписанта для сохранения.</param>
        /// <remarks>
        /// Для одного timeline и этапа хранится только один актуальный выбор, поэтому используется upsert.
        /// </remarks>
        public void SaveSelection(KonturStageSignerSelection selection)
        {
            if (selection == null)
            {
                throw new ArgumentNullException("selection");
            }

            const string sql = @"
MERGE Perdoc.dbo.TEpdStageSignerSelection AS target
USING
(
    SELECT
        @TimelineId AS TimelineId,
        @StageCode AS StageCode,
        @SignerFizLicoId AS SignerFizLicoId,
        @UpdatedByUserId AS UpdatedByUserId
) AS source
ON target.TimelineId = source.TimelineId
AND target.StageCode = source.StageCode
WHEN MATCHED THEN
    UPDATE SET
        SignerFizLicoId = source.SignerFizLicoId,
        UpdatedByUserId = source.UpdatedByUserId,
        UpdatedAt = GETDATE()
WHEN NOT MATCHED THEN
    INSERT (TimelineId, StageCode, SignerFizLicoId, UpdatedByUserId, UpdatedAt)
    VALUES (source.TimelineId, source.StageCode, source.SignerFizLicoId, source.UpdatedByUserId, GETDATE());";

            using (var cn = new SqlConnection(ConnectionString))
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.Add("@TimelineId", SqlDbType.BigInt).Value = selection.TimelineId;
                cmd.Parameters.Add("@StageCode", SqlDbType.NVarChar, 16).Value = NormalizeStageCode(selection.StageCode);
                cmd.Parameters.Add("@SignerFizLicoId", SqlDbType.BigInt).Value = selection.SignerFizLicoId;
                cmd.Parameters.Add("@UpdatedByUserId", SqlDbType.BigInt).Value = selection.UpdatedByUserId;
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Удаляет сохраненный выбор подписанта для этапа.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа.</param>
        /// <param name="stageCode">Код этапа T1/T2/T3/T4.</param>
        /// <remarks>Метод нужен для явного снятия выбора при возврате к пустому пункту списка.</remarks>
        public void DeleteSelection(long timelineId, string stageCode)
        {
            const string sql = @"
DELETE FROM Perdoc.dbo.TEpdStageSignerSelection
 WHERE TimelineId = @TimelineId
   AND StageCode = @StageCode;";

            using (var cn = new SqlConnection(ConnectionString))
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.Add("@TimelineId", SqlDbType.BigInt).Value = timelineId;
                cmd.Parameters.Add("@StageCode", SqlDbType.NVarChar, 16).Value = NormalizeStageCode(stageCode);
                cn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Загружает сведения об организации роли этапа.
        /// </summary>
        /// <param name="context">Контекст выбора подписанта, в который нужно записать данные организации.</param>
        /// <remarks>Организация нужна в UI, чтобы оператор видел, за кого именно будет подписываться этап.</remarks>
        private void LoadRequiredKontragentInfo(KonturStageSignerContext context)
        {
            const string sql = @"
SELECT TOP (1)
       RTRIM(ISNULL(name, '')) AS OrgName,
       RTRIM(ISNULL(inn,  '')) AS OrgInn
  FROM TKontragent
 WHERE id = @KontragentId;";

            using (var cn = new SqlConnection(ConnectionString))
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.Add("@KontragentId", SqlDbType.BigInt).Value = context.RequiredKontragentId;
                cn.Open();

                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return;
                    }

                    context.RequiredKontragentName = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                    context.RequiredKontragentInn = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                }
            }
        }

        /// <summary>
        /// Загружает допустимых подписантов для организации роли этапа.
        /// </summary>
        /// <param name="context">Контекст, который нужно заполнить кандидатами.</param>
        /// <remarks>
        /// В список попадают прямые полномочия из TRukAndUL и полномочия по МЧД из TMchdK.
        /// Кандидаты отдаются без фильтра по текущему пользователю, потому что оператор и подписант разделены.
        /// </remarks>
        private void LoadCandidates(KonturStageSignerContext context)
        {
            const string sql = @"
SET DATEFORMAT DMY;

SELECT
    src.AuthoritySource,
    src.AuthorityDescription,
    src.AuthorityDocType,
    src.AuthorityDocDate,
    src.MchdNumber,
    src.SignerFizLicoId,
    src.SignerFio,
    src.Position,
    src.SignerInnFl
FROM
(
    SELECT DISTINCT
           CAST(N'TRukAndUL' AS NVARCHAR(32)) AS AuthoritySource,
           CAST(CASE RL.zdolg
                    WHEN 0 THEN N'Руководитель'
                    WHEN 2 THEN N'Замещающий руководителя'
                    WHEN 7 THEN N'Подписант ЭТрН'
                    ELSE N'Уполномоченное лицо'
                END AS NVARCHAR(128)) AS AuthorityDescription,
           RTRIM(ISNULL(RL.tip, '')) AS AuthorityDocType,
           RL.datadoc AS AuthorityDocDate,
           CAST(NULL AS NVARCHAR(128)) AS MchdNumber,
           FL.id AS SignerFizLicoId,
           RTRIM(FL.fam) + N' ' + RTRIM(FL.name) + N' ' + RTRIM(FL.otch) AS SignerFio,
           RTRIM(ISNULL(TDlg.name, '')) AS Position,
           RTRIM(ISNULL(FL.img_contenttype, '')) AS SignerInnFl
      FROM TRukAndUL AS RL
      INNER JOIN TFizLico AS FL ON FL.id = RL.idFizL AND FL.del = 0
      LEFT JOIN TDlg ON TDlg.id = FL.iddlg
     WHERE RL.idvladelec = @KontragentId
       AND RL.del = 0
       AND RL.used = 1
       AND RL.zdolg IN (7, 0, 2)
       AND (RL.databegin IS NULL OR RL.databegin <= GETDATE())
       AND (RL.dataend IS NULL OR RL.dataend >= CAST(GETDATE() AS date))

    UNION ALL

    SELECT DISTINCT
           CAST(N'TMchdK' AS NVARCHAR(32)) AS AuthoritySource,
           CAST(N'МЧД' AS NVARCHAR(128)) AS AuthorityDescription,
           CAST(N'МЧД' AS NVARCHAR(128)) AS AuthorityDocType,
           CAST(NULL AS DATETIME) AS AuthorityDocDate,
           RTRIM(ISNULL(M.NumDoverUUID, '')) AS MchdNumber,
           FL.id AS SignerFizLicoId,
           RTRIM(FL.fam) + N' ' + RTRIM(FL.name) + N' ' + RTRIM(FL.otch) AS SignerFio,
           RTRIM(ISNULL(TDlg.name, '')) AS Position,
           RTRIM(ISNULL(M.innFL, '')) AS SignerInnFl
      FROM TMchdK AS M
      INNER JOIN TFizLico AS FL ON FL.id = M.idLico AND FL.del = 0
      LEFT JOIN TDlg ON TDlg.id = FL.iddlg
     WHERE M.idKontrag = @KontragentId
       AND M.del = 0
       AND (M.validDateTo IS NULL OR M.validDateTo >= GETDATE())
       AND (M.dateOtzyv IS NULL OR M.dateOtzyv > GETDATE())
) AS src
ORDER BY src.SignerFio,
         CASE WHEN src.AuthoritySource = N'TRukAndUL' THEN 0 ELSE 1 END,
         src.AuthorityDocDate DESC;";

            using (var cn = new SqlConnection(ConnectionString))
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.Add("@KontragentId", SqlDbType.BigInt).Value = context.RequiredKontragentId;
                cn.Open();

                using (var reader = cmd.ExecuteReader())
                {
                    var seenSignerIds = new System.Collections.Generic.HashSet<long>();
                    while (reader.Read())
                    {
                        var signerFizLicoId = reader.GetInt64(5);
                        if (seenSignerIds.Contains(signerFizLicoId))
                        {
                            continue;
                        }

                        seenSignerIds.Add(signerFizLicoId);
                        context.Candidates.Add(new KonturStageSignerCandidate
                        {
                            TitleCode = context.TitleCode,
                            RequiredRoleCode = context.RequiredRoleCode,
                            RequiredRoleName = context.RequiredRoleName,
                            RequiredKontragentId = context.RequiredKontragentId,
                            RequiredKontragentInn = context.RequiredKontragentInn,
                            RequiredKontragentName = context.RequiredKontragentName,
                            AuthoritySource = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                            AuthorityDescription = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                            AuthorityDocType = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                            AuthorityDocDate = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3),
                            MchdNumber = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                            SignerFizLicoId = signerFizLicoId,
                            SignerFio = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                            Position = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                            SignerInnFl = reader.IsDBNull(8) ? string.Empty : reader.GetString(8)
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Определяет идентификатор организации, от имени которой подписывается этап.
        /// </summary>
        /// <param name="idzak">Идентификатор заявки ТИС.</param>
        /// <param name="roleCode">Код роли этапа ГО/ТК/ГП.</param>
        /// <returns>Идентификатор TKontragent или 0, если организация не найдена.</returns>
        /// <remarks>
        /// Логика повторяет контур определения стороны из ETRNtituls, но вынесена в репозиторий,
        /// чтобы страница выбора подписанта не зависела от текущего пользователя сессии.
        /// </remarks>
        private long ResolveRequiredKontragentId(long idzak, string roleCode)
        {
            string sql;
            if (roleCode == "ГО")
            {
                sql = @"SELECT TZR.idOtpravitel FROM TZRekviz AS TZR WHERE TZR.idn = @Id;";
            }
            else if (roleCode == "ГП")
            {
                sql = @"SELECT TZR.idPoluchatel FROM TZRekviz AS TZR WHERE TZR.idn = @Id;";
            }
            else
            {
                sql = @"
SELECT Reis2.idIspolnitel
  FROM TZayavka AS Zay
  LEFT JOIN TZayavka AS Reis ON Reis.id = Zay.idzakaz
  LEFT JOIN TZayavka AS Reis2 ON Reis2.id =
      CASE WHEN Reis.idzakaz = 0 OR Reis.idzakaz IS NULL
           THEN Reis.id ELSE Reis.idzakaz END
 WHERE Zay.id = @Id;";
            }

            using (var cn = new SqlConnection(ConnectionString))
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.Parameters.Add("@Id", SqlDbType.BigInt).Value = idzak;
                cn.Open();
                var value = cmd.ExecuteScalar();
                return value == null || value == DBNull.Value ? 0 : Convert.ToInt64(value);
            }
        }

        /// <summary>
        /// Нормализует код этапа к форме T1/T2/T3/T4.
        /// </summary>
        /// <param name="stageCode">Исходный код этапа.</param>
        /// <returns>Нормализованный код этапа.</returns>
        /// <remarks>Нормализация исключает расхождения между T1 и T1_INITIAL при хранении выбора.</remarks>
        private string NormalizeStageCode(string stageCode)
        {
            var normalized = (stageCode ?? string.Empty).Trim().ToUpperInvariant();
            if (normalized == "T1_INITIAL" || normalized == "T1_DRAFT")
            {
                return "T1";
            }

            return normalized;
        }

        /// <summary>
        /// Возвращает сокращенный код роли этапа.
        /// </summary>
        /// <param name="titleCode">Код титула этапа.</param>
        /// <returns>Код роли ГО/ТК/ГП или пустая строка.</returns>
        private string ResolveRoleCode(string titleCode)
        {
            switch (NormalizeStageCode(titleCode))
            {
                case "T1": return "ГО";
                case "T2": return "ТК";
                case "T3": return "ГП";
                case "T4": return "ТК";
                default: return string.Empty;
            }
        }

        /// <summary>
        /// Возвращает человекочитаемое имя роли этапа.
        /// </summary>
        /// <param name="titleCode">Код титула этапа.</param>
        /// <returns>Имя роли для UI.</returns>
        private string ResolveRoleName(string titleCode)
        {
            switch (ResolveRoleCode(titleCode))
            {
                case "ГО": return "Грузоотправитель";
                case "ТК": return "Перевозчик";
                case "ГП": return "Грузополучатель";
                default: return string.Empty;
            }
        }
    }
}
