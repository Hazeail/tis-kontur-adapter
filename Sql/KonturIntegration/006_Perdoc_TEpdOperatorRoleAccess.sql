/*
  SQL: 006_Perdoc_TEpdOperatorRoleAccess.sql
  НАЗНАЧЕНИЕ: Ролевой реестр доступа Контур для маршрутизации этапов ЭТрН по организациям/ящикам.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  13.05.2026 - Первичное создание таблицы ролевого доступа.
*/

IF OBJECT_ID(N'Perdoc.dbo.TEpdOperatorRoleAccess', N'U') IS NULL
BEGIN
    CREATE TABLE Perdoc.dbo.TEpdOperatorRoleAccess
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TEpdOperatorRoleAccess PRIMARY KEY,
        OperatorCode NVARCHAR(32) NOT NULL,
        TitleCode NVARCHAR(16) NULL,
        SenderRole NVARCHAR(32) NULL,
        Inn NVARCHAR(12) NULL,
        Kpp NVARCHAR(9) NULL,
        DiadocBoxId NVARCHAR(64) NOT NULL,
        ApiKey NVARCHAR(256) NULL,
        Priority INT NOT NULL CONSTRAINT DF_TEpdOperatorRoleAccess_Priority DEFAULT (0),
        IsActive BIT NOT NULL CONSTRAINT DF_TEpdOperatorRoleAccess_IsActive DEFAULT (1),
        CreatedAt DATETIME NOT NULL CONSTRAINT DF_TEpdOperatorRoleAccess_CreatedAt DEFAULT (GETDATE()),
        UpdatedAt DATETIME NOT NULL CONSTRAINT DF_TEpdOperatorRoleAccess_UpdatedAt DEFAULT (GETDATE())
    );
END

IF NOT EXISTS
(
    SELECT 1
      FROM sys.indexes
     WHERE name = N'IX_TEpdOperatorRoleAccess_Operator_Active_TitleRole_Priority'
       AND object_id = OBJECT_ID(N'Perdoc.dbo.TEpdOperatorRoleAccess')
)
BEGIN
    CREATE INDEX IX_TEpdOperatorRoleAccess_Operator_Active_TitleRole_Priority
        ON Perdoc.dbo.TEpdOperatorRoleAccess (OperatorCode, IsActive, TitleCode, SenderRole, Priority DESC, Id DESC);
END
