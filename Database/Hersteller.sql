/* =====================================================================
   Hersteller.sql  -  Stored Procedures für Hersteller (Block 2)

   Can be run on its own in SSMS. Requirements:
   - Database Lagerverwaltungssystem with table dbo.Hersteller (script.sql)
   - dbo.spAuthorize already exists (from StoredProcedures.sql)

   The same four procedures are also part of StoredProcedures.sql, so
   running both files is fine (CREATE OR ALTER).
   ===================================================================== */

USE Lagerverwaltungssystem;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
/* GET /api/manufacturers  (any logged-in user) */
CREATE OR ALTER PROCEDURE dbo.spGetAllManufacturers
    @TokenHash VARBINARY(32)
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.spAuthorize @TokenHash;

    SELECT ID, Name FROM dbo.Hersteller ORDER BY Name;
END
GO

/* POST /api/manufacturers  (Administrator) */
CREATE OR ALTER PROCEDURE dbo.spCreateManufacturer
    @TokenHash VARBINARY(32),
    @Name      NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.spAuthorize @TokenHash, N'Administrator';

    IF @Name IS NULL OR LTRIM(RTRIM(@Name)) = N''
        THROW 50400, N'Name darf nicht leer sein.', 1;
    IF EXISTS (SELECT 1 FROM dbo.Hersteller WHERE Name = @Name)
        THROW 50409, N'Ein Hersteller mit diesem Namen existiert bereits.', 1;

    INSERT INTO dbo.Hersteller (Name) VALUES (@Name);
    SELECT CAST(SCOPE_IDENTITY() AS INT) AS NewId;
END
GO

/* PUT /api/manufacturers/{id}  (Administrator) */
CREATE OR ALTER PROCEDURE dbo.spAlterManufacturer
    @TokenHash VARBINARY(32),
    @Id        INT,
    @Name      NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.spAuthorize @TokenHash, N'Administrator';

    IF NOT EXISTS (SELECT 1 FROM dbo.Hersteller WHERE ID = @Id)
        THROW 50404, N'Hersteller nicht gefunden.', 1;
    IF @Name IS NULL OR LTRIM(RTRIM(@Name)) = N''
        THROW 50400, N'Name darf nicht leer sein.', 1;
    IF EXISTS (SELECT 1 FROM dbo.Hersteller WHERE Name = @Name AND ID <> @Id)
        THROW 50409, N'Ein Hersteller mit diesem Namen existiert bereits.', 1;

    UPDATE dbo.Hersteller SET Name = @Name WHERE ID = @Id;
END
GO

/* DELETE /api/manufacturers/{id}  (Administrator): refused while articles use it,
   so no article loses its manufacturer. */
CREATE OR ALTER PROCEDURE dbo.spDeleteManufacturer
    @TokenHash VARBINARY(32),
    @Id        INT
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.spAuthorize @TokenHash, N'Administrator';

    IF NOT EXISTS (SELECT 1 FROM dbo.Hersteller WHERE ID = @Id)
        THROW 50404, N'Hersteller nicht gefunden.', 1;
    IF EXISTS (SELECT 1 FROM dbo.Artikel WHERE Hersteller = @Id)
        THROW 50409, N'Hersteller wird noch von Artikeln verwendet.', 1;

    DELETE FROM dbo.Hersteller WHERE ID = @Id;
END
GO

