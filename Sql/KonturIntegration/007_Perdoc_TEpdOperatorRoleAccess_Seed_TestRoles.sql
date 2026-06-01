/*
  SQL: 007_Perdoc_TEpdOperatorRoleAccess_Seed_TestRoles.sql
  НАЗНАЧЕНИЕ: Начальное заполнение ролевого реестра доступа Контур для тестового цикла ЭТрН.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  13.05.2026 - Первичное заполнение ролей заказчика, перевозчика и грузополучателя.
*/

IF NOT EXISTS
(
    SELECT 1
      FROM Perdoc.dbo.TEpdOperatorRoleAccess
     WHERE OperatorCode = N'Kontur'
       AND TitleCode = N'T1'
       AND DiadocBoxId = N'3d63a359-42a5-4a19-97af-c63f139b9423'
)
BEGIN
    INSERT INTO Perdoc.dbo.TEpdOperatorRoleAccess
    (
        OperatorCode, TitleCode, SenderRole, Inn, Kpp, DiadocBoxId, ApiKey, Priority, IsActive
    )
    VALUES
    (
        N'Kontur', N'T1', N'Consignor', N'9069744171', N'466045102', N'3d63a359-42a5-4a19-97af-c63f139b9423', NULL, 100, 1
    );
END

IF NOT EXISTS
(
    SELECT 1
      FROM Perdoc.dbo.TEpdOperatorRoleAccess
     WHERE OperatorCode = N'Kontur'
       AND TitleCode = N'T2'
       AND DiadocBoxId = N'ef561ca3-122f-4fe3-8689-848ee3845acc'
)
BEGIN
    INSERT INTO Perdoc.dbo.TEpdOperatorRoleAccess
    (
        OperatorCode, TitleCode, SenderRole, Inn, Kpp, DiadocBoxId, ApiKey, Priority, IsActive
    )
    VALUES
    (
        N'Kontur', N'T2', N'Carrier', N'4656112890', N'854044788', N'ef561ca3-122f-4fe3-8689-848ee3845acc', NULL, 100, 1
    );
END

IF NOT EXISTS
(
    SELECT 1
      FROM Perdoc.dbo.TEpdOperatorRoleAccess
     WHERE OperatorCode = N'Kontur'
       AND TitleCode = N'T3'
       AND DiadocBoxId = N'843b9531-a716-46a7-b4af-3b89f261f693'
)
BEGIN
    INSERT INTO Perdoc.dbo.TEpdOperatorRoleAccess
    (
        OperatorCode, TitleCode, SenderRole, Inn, Kpp, DiadocBoxId, ApiKey, Priority, IsActive
    )
    VALUES
    (
        N'Kontur', N'T3', N'Consignee', N'4510779624', N'834301294', N'843b9531-a716-46a7-b4af-3b89f261f693', NULL, 100, 1
    );
END

IF NOT EXISTS
(
    SELECT 1
      FROM Perdoc.dbo.TEpdOperatorRoleAccess
     WHERE OperatorCode = N'Kontur'
       AND TitleCode = N'T4'
       AND DiadocBoxId = N'ef561ca3-122f-4fe3-8689-848ee3845acc'
)
BEGIN
    INSERT INTO Perdoc.dbo.TEpdOperatorRoleAccess
    (
        OperatorCode, TitleCode, SenderRole, Inn, Kpp, DiadocBoxId, ApiKey, Priority, IsActive
    )
    VALUES
    (
        N'Kontur', N'T4', N'Carrier', N'4656112890', N'854044788', N'ef561ca3-122f-4fe3-8689-848ee3845acc', NULL, 100, 1
    );
END
