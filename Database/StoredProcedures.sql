/* =====================================================================
   StoredProcedures.sql  -  Block 2 (REST-API)  -  Lagerverwaltung BS EVITA

   Run order:
     1. Schemaanpassungen.sql   (once)
     2. this file               (can be run again any time: CREATE OR ALTER)

   Save/open this file as UTF-8 (with BOM), otherwise the umlauts in
   'Übernahme' / 'Rückgabe' break and the movement types aren't found.

   ---------------------------------------------------------------------
   CONVENTIONS USED IN EVERY PROCEDURE
   ---------------------------------------------------------------------
   * Error numbers = HTTP status + 50000. ControllerHelper.HandleSqlException
     turns  THROW 50404  into HTTP 404, and so on:
        50400 invalid input        50401 not logged in / token invalid
        50403 no permission        50404 not found
        50409 conflict (stock too low, already confirmed, still in use...)

   * Every protected procedure takes @TokenHash (SHA-256 of the bearer
     token, 32 bytes) and calls dbo.spAuthorize FIRST. So the database
     itself checks who is calling. The C# code never passes a user ID
     or a role for the CALLER.

   * Passwords never reach the database in plain text. C# computes
     PBKDF2(password + pepper, salt) and only passes the finished hash.
     (The pepper is not allowed to be in the database.)

   * Procedures that change several rows use this pattern:
        SET XACT_ABORT ON;      -- any error -> whole transaction rolled back
        BEGIN TRY
            BEGIN TRAN; ... COMMIT;
        END TRY
        BEGIN CATCH
            IF @@TRANCOUNT > 0 ROLLBACK;
            THROW;              -- pass the error on to C#
        END CATCH
     So a movement is either booked completely or not at all.

   * All values come in as parameters. No procedure builds SQL text out
     of strings (no EXEC(@sql)), so SQL injection is impossible.

   ---------------------------------------------------------------------
   WHEN STOCK CHANGES (task section 5: "reserviert, abgezogen, gutgeschrieben")
   ---------------------------------------------------------------------
     spCreateTransfer  Hauptlager  - Menge   (reserved: open 'Ausgabe' row)
     spConfirmTransfer Labor       + Menge   ('Ausgabe' closed, 'Übernahme' logged)
     spCreateReturn    Labor       - Menge
                       Umbuchungsl.+ Menge   (open 'Rückgabe' row)
     spAcceptReturn    Umbuchungsl.- Menge
                       Hauptlager  + Menge   ('Rückgabe' closed, 'Einbuchung' logged)
   Total = Hauptlager + all Labore + Umbuchungslager + open transfers.
   Nothing is counted twice.
   ===================================================================== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* Old procedures that are removed (see review doc):
   - spLogin        compared  b.Password = @Password  (plain text!)
                    -> spGetUserForLogin + password check in C# + spCreateApiToken
   - spCreateToken  duplicate of spCreateApiToken
   - spGetUserInfo  took any username, so anyone could read other users
                    -> spGetOwnUser (token only) */
DROP PROCEDURE IF EXISTS dbo.spLogin;
DROP PROCEDURE IF EXISTS dbo.spCreateToken;
DROP PROCEDURE IF EXISTS dbo.spGetUserInfo;
GO


/* =====================================================================
   HELPERS  (used by other procedures, NOT called from C#)
   In EF Core Power Tools, untick vRaum and spBestandAendern.
   ===================================================================== */

/* View: every room with its type, responsible user and a readable name
   ("Raum 312", "Hauptlager", ...). Same logic as Raum.Anzeigename in
   the MVC app. Used so the procedures below don't repeat the joins. */
CREATE OR ALTER VIEW dbo.vRaum
AS
SELECT  r.ID,
        r.RaumTyp           AS RaumTypId,
        t.Name              AS RaumTyp,
        r.RaumNum,
        r.Benutzer          AS BenutzerId,
        b.Username,
        CASE WHEN t.Name = N'Labor'
             THEN COALESCE(N'Raum ' + CAST(r.RaumNum AS NVARCHAR(10)),
                           N'Labor ' + CAST(r.ID AS NVARCHAR(10)))
             ELSE t.Name
        END                 AS Anzeigename
FROM    dbo.Raum r
JOIN    dbo.RaumTyp  t ON t.ID = r.RaumTyp
JOIN    dbo.Benutzer b ON b.ID = r.Benutzer;
GO

/* Changes the stock of ONE article in ONE room by @Delta (+ or -).
   Must be called inside the caller's transaction.

   Why this is safe with two requests at the same time:
   - Taking stock away is ONE UPDATE with "Menge >= amount" in the WHERE.
     SQL Server locks the row while it updates, so a second request
     waits and then sees the reduced amount. If not enough is left,
     no row matches (@@ROWCOUNT = 0) and we refuse with 409.
     (Reading first and writing later would let two requests both read
     "5" and both take 5.)
   - Adding stock: UPDATE with UPDLOCK + SERIALIZABLE locks the row, or
     the empty "slot" if the row doesn't exist yet, so two requests
     can't both INSERT the same (Raum, Art) row.
   - Rows that reach 0 are deleted, like in Block 1 (LagerService). */
CREATE OR ALTER PROCEDURE dbo.spBestandAendern
    @RaumId    INT,
    @ArtikelId INT,
    @Delta     INT
AS
BEGIN
    SET NOCOUNT ON;

    IF @Delta < 0
    BEGIN
        UPDATE dbo.RaumArtikel
        SET    Menge = Menge + @Delta
        WHERE  Raum = @RaumId AND Art = @ArtikelId AND Menge >= -@Delta;

        IF @@ROWCOUNT = 0
            THROW 50409, N'Nicht genügend Bestand vorhanden.', 1;

        DELETE FROM dbo.RaumArtikel
        WHERE  Raum = @RaumId AND Art = @ArtikelId AND Menge = 0;
    END
    ELSE IF @Delta > 0
    BEGIN
        UPDATE dbo.RaumArtikel WITH (UPDLOCK, SERIALIZABLE)
        SET    Menge = Menge + @Delta
        WHERE  Raum = @RaumId AND Art = @ArtikelId;

        IF @@ROWCOUNT = 0
            INSERT INTO dbo.RaumArtikel (Raum, Art, Menge)
            VALUES (@RaumId, @ArtikelId, @Delta);
    END
END
GO


/* =====================================================================
   AUTHENTICATION
   ===================================================================== */

/* Central token check, called at the start of every protected procedure.
   A token is valid only if: it exists, it is not revoked, it is not expired.
   Returns the caller's ID and role name. If @RequiredRole is given and
   the caller doesn't have it -> 403.
   Changed vs. your version: also returns @RoleName, so other procedures
   can decide "admin sees everything, LehrerIn only her own labs". */
CREATE OR ALTER PROCEDURE dbo.spAuthorize
    @TokenHash    VARBINARY(32),
    @RequiredRole NVARCHAR(20) = NULL,
    @Id           INT          = NULL OUTPUT,
    @RoleName     NVARCHAR(20) = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @Id = NULL;
    SET @RoleName = NULL;

    SELECT  @Id = b.ID,
            @RoleName = r.Name
    FROM    dbo.ApiTokens t
    JOIN    dbo.Benutzer b ON b.ID = t.UserId
    JOIN    dbo.Rolle    r ON r.ID = b.Rolle
    WHERE   t.TokenHash = @TokenHash
      AND   t.RevokedAt IS NULL
      AND   t.ExpiresAt > SYSUTCDATETIME();

    IF @Id IS NULL
        THROW 50401, N'Nicht angemeldet oder Token ungültig/abgelaufen.', 1;

    -- @RequiredRole may be a list like 'Administrator,LehrerIn' (kept from your version).
    IF @RequiredRole IS NOT NULL
       AND @RoleName NOT IN (SELECT TRIM(value) FROM STRING_SPLIT(@RequiredRole, ','))
        THROW 50403, N'Keine Berechtigung für diese Aktion.', 1;
END
GO

/* Login step 1: C# fetches the stored hash and checks the password
   ITSELF (with salt + pepper). This procedure does NOT check anything.
   If no row comes back, C# answers 401 with the same message as for a
   wrong password, so nobody can find out which usernames exist. */
CREATE OR ALTER PROCEDURE dbo.spGetUserForLogin
    @Username NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT  b.ID        AS UserId,
            b.Username,
            b.Password  AS PasswordHash,
            r.Name      AS RoleName
    FROM    dbo.Benutzer b
    JOIN    dbo.Rolle    r ON r.ID = b.Rolle
    WHERE   b.Username = @Username;
END
GO

/* Login step 2: only called by C# AFTER the password was verified.
   Stores only the SHA-256 hash of the token, never the token itself. 
   Changed vs. your version: CreatedAt is set (it had no default), input is
   validated. Your INSERT of RevokedAt = NULL failed because the column was
   NOT NULL; Schemaanpassungen.sql fixes that. */
CREATE OR ALTER PROCEDURE dbo.spCreateApiToken
    @UserId    INT,
    @TokenHash VARBINARY(32),
    @ExpiresAt DATETIME2
AS
BEGIN
    SET NOCOUNT ON;

    IF @TokenHash IS NULL OR DATALENGTH(@TokenHash) <> 32
        THROW 50400, N'Ungültiger Token-Hash.', 1;
    IF @ExpiresAt IS NULL OR @ExpiresAt <= SYSUTCDATETIME()
        THROW 50400, N'Ablaufzeit muss in der Zukunft liegen (UTC).', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.Benutzer WHERE ID = @UserId)
        THROW 50404, N'Benutzer nicht gefunden.', 1;

    INSERT INTO dbo.ApiTokens (TokenHash, UserId, CreatedAt, ExpiresAt, RevokedAt)
    VALUES (@TokenHash, @UserId, SYSUTCDATETIME(), @ExpiresAt, NULL);
END
GO

/* POST /api/auth/logout: revokes exactly the token that was sent. */
CREATE OR ALTER PROCEDURE dbo.spLogout
    @TokenHash VARBINARY(32)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.ApiTokens
    SET    RevokedAt = SYSUTCDATETIME()
    WHERE  TokenHash = @TokenHash
      AND  RevokedAt IS NULL
      AND  ExpiresAt > SYSUTCDATETIME();

    IF @@ROWCOUNT = 0
        THROW 50401, N'Nicht angemeldet oder Token ungültig/abgelaufen.', 1;
END
GO

/* POST /api/auth/register (public).
   There is deliberately NO role parameter: every new user becomes
   'LehrerIn'. Only an administrator can change roles later
   (spAlterUserRole). @PasswordHash is computed in C#. 
   Changed vs. your version: it inserted Rolle = 1, and ID 1 is
   'Administrator' (the seed creates it first). So EVERY public
   registration became an admin. Now the ID is looked up by name. */
CREATE OR ALTER PROCEDURE dbo.spRegister
    @Username     NVARCHAR(20),
    @PasswordHash NVARCHAR(455)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        IF @Username IS NULL OR LTRIM(RTRIM(@Username)) = N''
            THROW 50400, N'Benutzername darf nicht leer sein.', 1;
        IF @PasswordHash IS NULL OR @PasswordHash = N''
            THROW 50400, N'Kennwort fehlt.', 1;
        IF EXISTS (SELECT 1 FROM dbo.Benutzer WHERE Username = @Username)
            THROW 50409, N'Benutzername ist bereits vergeben.', 1;

        DECLARE @RolleId INT = (SELECT ID FROM dbo.Rolle WHERE Name = N'LehrerIn');

        INSERT INTO dbo.Benutzer (Rolle, Username, Password)
        VALUES (@RolleId, @Username, @PasswordHash);

        SELECT CAST(SCOPE_IDENTITY() AS INT) AS NewId;
    END TRY
    BEGIN CATCH
        -- 2601/2627 = unique index violated: two registrations with the
        -- same name at the same moment. Turn it into a clean 409.
        IF ERROR_NUMBER() IN (2601, 2627)
            THROW 50409, N'Benutzername ist bereits vergeben.', 1;
        THROW;
    END CATCH
END
GO


/* =====================================================================
   USERS
   ===================================================================== */

/* GET /api/users/me: the token says who you are, no username needed. */
CREATE OR ALTER PROCEDURE dbo.spGetOwnUser
    @TokenHash VARBINARY(32)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT;
    EXEC dbo.spAuthorize @TokenHash, NULL, @Id = @UserId OUTPUT;

    SELECT  b.ID, b.Username, r.Name AS RoleName
    FROM    dbo.Benutzer b
    JOIN    dbo.Rolle    r ON r.ID = b.Rolle
    WHERE   b.ID = @UserId;
END
GO

/* PUT /api/users/me
   Pass NULL for what should stay unchanged.
   For a new password, C# must FIRST verify the current password
   (spGetUserForLogin), then hash the new one with a NEW salt.
   After a password change, all OTHER tokens of the user are revoked,
   so a stolen session stops working. 
   Changed vs. your version: it compared  Password = @CurrentPassword
   (plain text). The current password is now checked in C#. */
CREATE OR ALTER PROCEDURE dbo.spUpdateOwnProfile
    @TokenHash       VARBINARY(32),
    @NewUsername     NVARCHAR(20)  = NULL,
    @NewPasswordHash NVARCHAR(455) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        DECLARE @UserId INT;
        EXEC dbo.spAuthorize @TokenHash, NULL, @Id = @UserId OUTPUT;

        IF @NewUsername IS NULL AND @NewPasswordHash IS NULL
            THROW 50400, N'Keine Änderung angegeben.', 1;
        IF @NewUsername IS NOT NULL AND LTRIM(RTRIM(@NewUsername)) = N''
            THROW 50400, N'Benutzername darf nicht leer sein.', 1;
        IF @NewUsername IS NOT NULL
           AND EXISTS (SELECT 1 FROM dbo.Benutzer WHERE Username = @NewUsername AND ID <> @UserId)
            THROW 50409, N'Benutzername ist bereits vergeben.', 1;

        BEGIN TRAN;

        UPDATE dbo.Benutzer
        SET    Username = COALESCE(@NewUsername, Username),
               Password = COALESCE(@NewPasswordHash, Password)
        WHERE  ID = @UserId;

        IF @NewPasswordHash IS NOT NULL
            UPDATE dbo.ApiTokens
            SET    RevokedAt = SYSUTCDATETIME()
            WHERE  UserId = @UserId AND TokenHash <> @TokenHash AND RevokedAt IS NULL;

        COMMIT;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        IF ERROR_NUMBER() IN (2601, 2627)
            THROW 50409, N'Benutzername ist bereits vergeben.', 1;
        THROW;
    END CATCH
END
GO

/* GET /api/users  (Administrator) */
CREATE OR ALTER PROCEDURE dbo.spGetAllUsers
    @TokenHash VARBINARY(32)
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.spAuthorize @TokenHash, N'Administrator';

    SELECT  b.ID, b.Username, r.Name AS RoleName
    FROM    dbo.Benutzer b
    JOIN    dbo.Rolle    r ON r.ID = b.Rolle
    ORDER BY b.Username;
END
GO

/* PUT /api/users/{id}/role  (Administrator)
   Rules: the last administrator can't be demoted (otherwise nobody can
   manage the system), and someone responsible for a Labor must stay
   'LehrerIn' (reassign the lab first). 
   Changed vs. your version: it had no @TokenHash at all, so the database
   didn't check that an admin was calling. */
CREATE OR ALTER PROCEDURE dbo.spAlterUserRole
    @TokenHash VARBINARY(32),
    @UserId    INT,
    @RoleName  NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.spAuthorize @TokenHash, N'Administrator';

    DECLARE @NewRolleId INT = (SELECT ID FROM dbo.Rolle WHERE Name = @RoleName);
    DECLARE @OldRole NVARCHAR(20) =
        (SELECT r.Name FROM dbo.Benutzer b JOIN dbo.Rolle r ON r.ID = b.Rolle WHERE b.ID = @UserId);

    IF @OldRole IS NULL
        THROW 50404, N'Benutzer nicht gefunden.', 1;
    IF @NewRolleId IS NULL
        THROW 50400, N'Unbekannte Rolle. Erlaubt: Administrator, LehrerIn.', 1;
    IF @OldRole = N'Administrator' AND @RoleName <> N'Administrator'
       AND (SELECT COUNT(*) FROM dbo.Benutzer b JOIN dbo.Rolle r ON r.ID = b.Rolle
            WHERE r.Name = N'Administrator') = 1
        THROW 50409, N'Der letzte Administrator kann nicht herabgestuft werden.', 1;
    IF @RoleName <> N'LehrerIn'
       AND EXISTS (SELECT 1 FROM dbo.vRaum WHERE BenutzerId = @UserId AND RaumTyp = N'Labor')
        THROW 50409, N'Benutzer ist noch für ein Labor zuständig. Zuerst Labor neu zuordnen.', 1;

    UPDATE dbo.Benutzer SET Rolle = @NewRolleId WHERE ID = @UserId;
END
GO

/* DELETE /api/users/me    -> @UserId = NULL  (own account)
   DELETE /api/users/{id}  -> @UserId = id    (Administrator only)

   A user can only be deleted if no history is lost (task section 3):
   not if they booked movements, are responsible for a room, or are the
   last administrator. Otherwise -> 409. 
   Changed vs. your version: deleted by username without any check, so a
   user with history failed with a foreign key error (HTTP 500). */
CREATE OR ALTER PROCEDURE dbo.spDeleteUser
    @TokenHash VARBINARY(32),
    @UserId    INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        DECLARE @CallerId INT, @CallerRole NVARCHAR(20);
        EXEC dbo.spAuthorize @TokenHash, NULL, @Id = @CallerId OUTPUT, @RoleName = @CallerRole OUTPUT;

        IF @UserId IS NULL
            SET @UserId = @CallerId;
        ELSE IF @UserId <> @CallerId AND @CallerRole <> N'Administrator'
            THROW 50403, N'Keine Berechtigung für diese Aktion.', 1;

        DECLARE @Role NVARCHAR(20) =
            (SELECT r.Name FROM dbo.Benutzer b JOIN dbo.Rolle r ON r.ID = b.Rolle WHERE b.ID = @UserId);

        IF @Role IS NULL
            THROW 50404, N'Benutzer nicht gefunden.', 1;
        IF EXISTS (SELECT 1 FROM dbo.Lagerbewegungen WHERE Benutzer = @UserId)
            THROW 50409, N'Benutzer hat Buchungen in der Historie und kann nicht gelöscht werden.', 1;
        IF EXISTS (SELECT 1 FROM dbo.Raum WHERE Benutzer = @UserId)
            THROW 50409, N'Benutzer ist noch für einen Raum zuständig.', 1;
        IF @Role = N'Administrator'
           AND (SELECT COUNT(*) FROM dbo.Benutzer b JOIN dbo.Rolle r ON r.ID = b.Rolle
                WHERE r.Name = N'Administrator') = 1
            THROW 50409, N'Der letzte Administrator kann nicht gelöscht werden.', 1;

        BEGIN TRAN;
        DELETE FROM dbo.ApiTokens WHERE UserId = @UserId;
        DELETE FROM dbo.Benutzer WHERE ID = @UserId;
        COMMIT;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END
GO


/* =====================================================================
   CATEGORIES
   ===================================================================== */

/* GET /api/categories  (any logged-in user) */
CREATE OR ALTER PROCEDURE dbo.spGetAllCategories
    @TokenHash VARBINARY(32)
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.spAuthorize @TokenHash;

    SELECT ID, Name, ModelNum FROM dbo.Kategorie ORDER BY Name;
END
GO

/* POST /api/categories  (Administrator) */
CREATE OR ALTER PROCEDURE dbo.spCreateCategory
    @TokenHash VARBINARY(32),
    @Name      NVARCHAR(100),
    @ModelNum  INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.spAuthorize @TokenHash, N'Administrator';

    IF @Name IS NULL OR LTRIM(RTRIM(@Name)) = N''
        THROW 50400, N'Name darf nicht leer sein.', 1;
    IF EXISTS (SELECT 1 FROM dbo.Kategorie WHERE Name = @Name)
        THROW 50409, N'Eine Kategorie mit diesem Namen existiert bereits.', 1;

    INSERT INTO dbo.Kategorie (Name, ModelNum) VALUES (@Name, @ModelNum);
    SELECT CAST(SCOPE_IDENTITY() AS INT) AS NewId;
END
GO

/* PUT /api/categories/{id}  (Administrator) */
CREATE OR ALTER PROCEDURE dbo.spAlterCategory
    @TokenHash VARBINARY(32),
    @Id        INT,
    @Name      NVARCHAR(100),
    @ModelNum  INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.spAuthorize @TokenHash, N'Administrator';

    IF NOT EXISTS (SELECT 1 FROM dbo.Kategorie WHERE ID = @Id)
        THROW 50404, N'Kategorie nicht gefunden.', 1;
    IF @Name IS NULL OR LTRIM(RTRIM(@Name)) = N''
        THROW 50400, N'Name darf nicht leer sein.', 1;
    IF EXISTS (SELECT 1 FROM dbo.Kategorie WHERE Name = @Name AND ID <> @Id)
        THROW 50409, N'Eine Kategorie mit diesem Namen existiert bereits.', 1;

    UPDATE dbo.Kategorie SET Name = @Name, ModelNum = @ModelNum WHERE ID = @Id;
END
GO

/* DELETE /api/categories/{id}  (Administrator): refused while articles use it. */
CREATE OR ALTER PROCEDURE dbo.spDeleteCategory
    @TokenHash VARBINARY(32),
    @Id        INT
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.spAuthorize @TokenHash, N'Administrator';

    IF NOT EXISTS (SELECT 1 FROM dbo.Kategorie WHERE ID = @Id)
        THROW 50404, N'Kategorie nicht gefunden.', 1;
    IF EXISTS (SELECT 1 FROM dbo.Artikel WHERE Kategorie = @Id)
        THROW 50409, N'Kategorie wird noch von Artikeln verwendet.', 1;

    DELETE FROM dbo.Kategorie WHERE ID = @Id;
END
GO


/* =====================================================================
   ARTICLES
   ===================================================================== */

/* GET /api/articles?name=&serienNum=&kategorieId=&herstellerId=&raumId=
   (any logged-in user)

   Five optional filters; NULL = "don't filter". The pattern
   "(@X IS NULL OR column = @X)" is still fully parameterized.
   The LIKE pattern is built from a PARAMETER, not by gluing SQL text,
   so it is safe.

   Permissions (task section 6: results respect the user's rights):
   - Bestand = admin: stock in ALL rooms; LehrerIn: only in OWN rooms.
   - Filter by @RaumId: a LehrerIn may only use one of her own rooms.
   OPTION (RECOMPILE) gives each call a query plan for the filters
   actually used. 
   Changed vs. your version: it required 'Administrator' (the task says any
   logged-in user), and returned no ID, so a client couldn't open details. */
CREATE OR ALTER PROCEDURE dbo.spGetAllArtikelFiltered
    @TokenHash    VARBINARY(32),
    @Name         NVARCHAR(100) = NULL,
    @SerienNum    INT           = NULL,
    @KategorieId  INT           = NULL,
    @HerstellerId INT           = NULL,
    @RaumId       INT           = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT, @Role NVARCHAR(20);
    EXEC dbo.spAuthorize @TokenHash, NULL, @Id = @UserId OUTPUT, @RoleName = @Role OUTPUT;

    IF @RaumId IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.Raum WHERE ID = @RaumId)
            THROW 50404, N'Standort nicht gefunden.', 1;
        IF @Role <> N'Administrator'
           AND NOT EXISTS (SELECT 1 FROM dbo.Raum WHERE ID = @RaumId AND Benutzer = @UserId)
            THROW 50403, N'Kein Zugriff auf diesen Standort.', 1;
    END

    SELECT  a.ID,
            a.Name,
            a.SerienNum,
            a.Kategorie  AS KategorieId,
            k.Name       AS KategorieName,
            a.Hersteller AS HerstellerId,
            h.Name       AS HerstellerName,
            ISNULL((SELECT SUM(ra.Menge)
                    FROM   dbo.RaumArtikel ra
                    JOIN   dbo.Raum r ON r.ID = ra.Raum
                    WHERE  ra.Art = a.ID
                      AND  (@RaumId IS NULL OR ra.Raum = @RaumId)
                      AND  (@Role = N'Administrator' OR r.Benutzer = @UserId)), 0) AS Bestand
    FROM    dbo.Artikel a
    JOIN    dbo.Kategorie  k ON k.ID = a.Kategorie
    LEFT JOIN dbo.Hersteller h ON h.ID = a.Hersteller
    WHERE   (@Name         IS NULL OR a.Name LIKE N'%' + @Name + N'%')
      AND   (@SerienNum    IS NULL OR a.SerienNum  = @SerienNum)
      AND   (@KategorieId  IS NULL OR a.Kategorie  = @KategorieId)
      AND   (@HerstellerId IS NULL OR a.Hersteller = @HerstellerId)
      AND   (@RaumId       IS NULL OR EXISTS (SELECT 1 FROM dbo.RaumArtikel ra
                                              WHERE ra.Art = a.ID AND ra.Raum = @RaumId))
    ORDER BY a.Name
    OPTION (RECOMPILE);
END
GO

/* GET /api/articles/{id}  (any logged-in user, same Bestand rule as above) */
CREATE OR ALTER PROCEDURE dbo.spGetArtikelById
    @TokenHash VARBINARY(32),
    @Id        INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT, @Role NVARCHAR(20);
    EXEC dbo.spAuthorize @TokenHash, NULL, @Id = @UserId OUTPUT, @RoleName = @Role OUTPUT;

    IF NOT EXISTS (SELECT 1 FROM dbo.Artikel WHERE ID = @Id)
        THROW 50404, N'Artikel nicht gefunden.', 1;

    SELECT  a.ID,
            a.Name,
            a.SerienNum,
            a.Kategorie  AS KategorieId,
            k.Name       AS KategorieName,
            a.Hersteller AS HerstellerId,
            h.Name       AS HerstellerName,
            ISNULL((SELECT SUM(ra.Menge)
                    FROM   dbo.RaumArtikel ra
                    JOIN   dbo.Raum r ON r.ID = ra.Raum
                    WHERE  ra.Art = a.ID
                      AND  (@Role = N'Administrator' OR r.Benutzer = @UserId)), 0) AS Bestand
    FROM    dbo.Artikel a
    JOIN    dbo.Kategorie  k ON k.ID = a.Kategorie
    LEFT JOIN dbo.Hersteller h ON h.ID = a.Hersteller
    WHERE   a.ID = @Id;
END
GO

/* POST /api/articles  (Administrator)
   Creates the article only (stock = 0). Put stock in with spAddStock.
   An article WITH SerienNum is exactly one device; WITHOUT it, the
   article is counted by quantity (RaumArtikel.Menge). */
CREATE OR ALTER PROCEDURE dbo.spCreateArtikel
    @TokenHash    VARBINARY(32),
    @Name         NVARCHAR(100),
    @SerienNum    INT = NULL,
    @KategorieId  INT,
    @HerstellerId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.spAuthorize @TokenHash, N'Administrator';

    IF @Name IS NULL OR LTRIM(RTRIM(@Name)) = N''
        THROW 50400, N'Name darf nicht leer sein.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.Kategorie WHERE ID = @KategorieId)
        THROW 50400, N'Kategorie existiert nicht.', 1;
    IF @HerstellerId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Hersteller WHERE ID = @HerstellerId)
        THROW 50400, N'Hersteller existiert nicht.', 1;
    IF @SerienNum IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.Artikel WHERE SerienNum = @SerienNum)
        THROW 50409, N'Diese Seriennummer ist bereits vergeben.', 1;

    INSERT INTO dbo.Artikel (Name, SerienNum, Kategorie, Hersteller)
    VALUES (@Name, @SerienNum, @KategorieId, @HerstellerId);

    SELECT CAST(SCOPE_IDENTITY() AS INT) AS NewId;
END
GO

/* PUT /api/articles/{id}  (Administrator)
   A serial number can only be added if at most 1 piece exists in total
   (a serial number = one single device). 
   Changed vs. your version: COALESCE made it impossible to REMOVE a serial
   number or manufacturer (NULL meant "keep"). PUT now sends all fields. */
CREATE OR ALTER PROCEDURE dbo.spAlterArtikel
    @TokenHash    VARBINARY(32),
    @Id           INT,
    @Name         NVARCHAR(100),
    @SerienNum    INT = NULL,
    @KategorieId  INT,
    @HerstellerId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.spAuthorize @TokenHash, N'Administrator';

    IF NOT EXISTS (SELECT 1 FROM dbo.Artikel WHERE ID = @Id)
        THROW 50404, N'Artikel nicht gefunden.', 1;
    IF @Name IS NULL OR LTRIM(RTRIM(@Name)) = N''
        THROW 50400, N'Name darf nicht leer sein.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.Kategorie WHERE ID = @KategorieId)
        THROW 50400, N'Kategorie existiert nicht.', 1;
    IF @HerstellerId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Hersteller WHERE ID = @HerstellerId)
        THROW 50400, N'Hersteller existiert nicht.', 1;
    IF @SerienNum IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.Artikel WHERE SerienNum = @SerienNum AND ID <> @Id)
        THROW 50409, N'Diese Seriennummer ist bereits vergeben.', 1;
    IF @SerienNum IS NOT NULL
       AND ISNULL((SELECT SUM(Menge) FROM dbo.RaumArtikel WHERE Art = @Id), 0)
         + ISNULL((SELECT SUM(ISNULL(b.ArtikelMenge, 1))
                   FROM dbo.Lagerbewegungen b JOIN dbo.BewegungArt ba ON ba.ID = b.Bewegung
                   WHERE b.Artikel = @Id AND ba.Name = N'Ausgabe' AND ISNULL(b.IsDone, 0) = 0), 0) > 1
        THROW 50409, N'Von diesem Artikel gibt es mehr als 1 Stück, eine Seriennummer ist nicht möglich.', 1;

    UPDATE dbo.Artikel
    SET    Name = @Name, SerienNum = @SerienNum, Kategorie = @KategorieId, Hersteller = @HerstellerId
    WHERE  ID = @Id;
END
GO

/* DELETE /api/articles/{id}  (Administrator)
   Only allowed if the article has no stock anywhere AND no history.
   Otherwise the history would lose information -> 409. */
CREATE OR ALTER PROCEDURE dbo.spDeleteArtikel
    @TokenHash VARBINARY(32),
    @Id        INT
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.spAuthorize @TokenHash, N'Administrator';

    IF NOT EXISTS (SELECT 1 FROM dbo.Artikel WHERE ID = @Id)
        THROW 50404, N'Artikel nicht gefunden.', 1;
    IF EXISTS (SELECT 1 FROM dbo.RaumArtikel WHERE Art = @Id)
        THROW 50409, N'Artikel hat noch Bestand und kann nicht gelöscht werden.', 1;
    IF EXISTS (SELECT 1 FROM dbo.Lagerbewegungen WHERE Artikel = @Id)
        THROW 50409, N'Artikel kommt in der Bewegungshistorie vor und kann nicht gelöscht werden.', 1;

    DELETE FROM dbo.Artikel WHERE ID = @Id;
END
GO

/* POST /api/articles/{id}/stock  (Administrator)  - "Zugang"
   New pieces arrive in the Hauptlager. Logged as movement 'Zugang'.
   For a serial-number article only 1 piece may exist in the whole
   system. The UPDLOCK on the Artikel row makes two simultaneous
   requests for the same article wait for each other. */
CREATE OR ALTER PROCEDURE dbo.spAddStock
    @TokenHash VARBINARY(32),
    @ArtikelId INT,
    @Menge     INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        DECLARE @UserId INT;
        EXEC dbo.spAuthorize @TokenHash, N'Administrator', @Id = @UserId OUTPUT;

        IF @Menge IS NULL OR @Menge < 1
            THROW 50400, N'Die Menge muss mindestens 1 sein.', 1;

        DECLARE @HauptlagerId INT = (SELECT ID FROM dbo.vRaum WHERE RaumTyp = N'Hauptlager');
        IF @HauptlagerId IS NULL
            THROW 50409, N'Es ist kein Hauptlager angelegt.', 1;

        BEGIN TRAN;

        DECLARE @SerienNum INT, @Found BIT = 0;
        SELECT @SerienNum = SerienNum, @Found = 1
        FROM   dbo.Artikel WITH (UPDLOCK, ROWLOCK)
        WHERE  ID = @ArtikelId;

        IF @Found = 0
            THROW 50404, N'Artikel nicht gefunden.', 1;

        IF @SerienNum IS NOT NULL
        BEGIN
            IF @Menge <> 1
                THROW 50400, N'Artikel mit Seriennummer können nur einzeln gebucht werden.', 1;
            IF EXISTS (SELECT 1 FROM dbo.RaumArtikel WHERE Art = @ArtikelId)
               OR EXISTS (SELECT 1 FROM dbo.Lagerbewegungen b JOIN dbo.BewegungArt ba ON ba.ID = b.Bewegung
                          WHERE b.Artikel = @ArtikelId AND ba.Name = N'Ausgabe' AND ISNULL(b.IsDone, 0) = 0)
                THROW 50409, N'Dieses Gerät (Seriennummer) ist bereits im Bestand.', 1;
        END

        EXEC dbo.spBestandAendern @HauptlagerId, @ArtikelId, @Menge;

        INSERT INTO dbo.Lagerbewegungen (Datum, Bewegung, Artikel, IsDone, ArtikelMenge, Benutzer, VonRaum, ZuRaum)
        VALUES (CAST(SYSDATETIME() AS DATE),
                (SELECT ID FROM dbo.BewegungArt WHERE Name = N'Zugang'),
                @ArtikelId, 1, @Menge, @UserId, @HauptlagerId, @HauptlagerId);

        DECLARE @NewId INT = CAST(SCOPE_IDENTITY() AS INT);
        COMMIT;

        SELECT @NewId AS NewId;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END
GO


/* =====================================================================
   LOCATIONS (Lager & Labore)
   ===================================================================== */

/* GET /api/locations  (Administrator) */
CREATE OR ALTER PROCEDURE dbo.spGetAllRooms
    @TokenHash VARBINARY(32)
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.spAuthorize @TokenHash, N'Administrator';

    SELECT ID, RaumTyp, RaumNum, Anzeigename, BenutzerId, Username
    FROM   dbo.vRaum
    ORDER BY RaumTypId, RaumNum;
END
GO

/* GET /api/locations/mine  (any logged-in user)
   A LehrerIn needs her lab ID for inventory and returns. */
CREATE OR ALTER PROCEDURE dbo.spGetOwnRooms
    @TokenHash VARBINARY(32)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT;
    EXEC dbo.spAuthorize @TokenHash, NULL, @Id = @UserId OUTPUT;

    SELECT ID, RaumTyp, RaumNum, Anzeigename, BenutzerId, Username
    FROM   dbo.vRaum
    WHERE  BenutzerId = @UserId
    ORDER BY RaumTypId, RaumNum;
END
GO

/* Shared rules for "who may be responsible for a room".
   Block 1 rule kept: one LehrerIn = one Labor (the MVC teacher views
   rely on it). Hauptlager and Umbuchungslager exist only once. */

/* POST /api/locations  (Administrator)
   @RaumTyp = 'Hauptlager' | 'Umbuchungslager' | 'Labor' */
CREATE OR ALTER PROCEDURE dbo.spCreateRoom
    @TokenHash  VARBINARY(32),
    @RaumTyp    NVARCHAR(20),
    @BenutzerId INT,
    @RaumNum    INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.spAuthorize @TokenHash, N'Administrator';

    DECLARE @RaumTypId INT = (SELECT ID FROM dbo.RaumTyp WHERE Name = @RaumTyp);
    DECLARE @UserRole NVARCHAR(20) =
        (SELECT r.Name FROM dbo.Benutzer b JOIN dbo.Rolle r ON r.ID = b.Rolle WHERE b.ID = @BenutzerId);

    IF @RaumTypId IS NULL
        THROW 50400, N'Unbekannter Raumtyp. Erlaubt: Hauptlager, Umbuchungslager, Labor.', 1;
    IF @UserRole IS NULL
        THROW 50400, N'Zuständiger Benutzer existiert nicht.', 1;
    IF @RaumTyp <> N'Labor' AND EXISTS (SELECT 1 FROM dbo.Raum WHERE RaumTyp = @RaumTypId)
        THROW 50409, N'Diesen Lagertyp gibt es nur einmal.', 1;
    IF @RaumTyp = N'Labor' AND @UserRole <> N'LehrerIn'
        THROW 50400, N'Ein Labor muss einer LehrerIn zugeordnet werden.', 1;
    IF @RaumTyp = N'Labor' AND EXISTS (SELECT 1 FROM dbo.vRaum WHERE BenutzerId = @BenutzerId AND RaumTyp = N'Labor')
        THROW 50409, N'Diese LehrerIn ist bereits einem Labor zugeordnet.', 1;
    IF @RaumNum IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.Raum WHERE RaumNum = @RaumNum)
        THROW 50409, N'Diese Raumnummer ist bereits vergeben.', 1;

    INSERT INTO dbo.Raum (RaumTyp, Benutzer, RaumNum) VALUES (@RaumTypId, @BenutzerId, @RaumNum);
    SELECT CAST(SCOPE_IDENTITY() AS INT) AS NewId;
END
GO

/* PUT /api/locations/{id}  (Administrator)
   Only the room number can be changed. The type can't change (a Labor
   with stock can't suddenly become the Hauptlager), and the
   responsible person changes via spAssignTeacherToLab. */
CREATE OR ALTER PROCEDURE dbo.spAlterRoom
    @TokenHash VARBINARY(32),
    @Id        INT,
    @RaumNum   INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.spAuthorize @TokenHash, N'Administrator';

    IF NOT EXISTS (SELECT 1 FROM dbo.Raum WHERE ID = @Id)
        THROW 50404, N'Standort nicht gefunden.', 1;
    IF @RaumNum IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.Raum WHERE RaumNum = @RaumNum AND ID <> @Id)
        THROW 50409, N'Diese Raumnummer ist bereits vergeben.', 1;

    UPDATE dbo.Raum SET RaumNum = @RaumNum WHERE ID = @Id;
END
GO

/* PUT /api/locations/{id}/teacher  (Administrator)
   The admin assignment "LehrerIn is responsible for Labor" required by
   the task. Open transfers to this lab then belong to the new LehrerIn. */
CREATE OR ALTER PROCEDURE dbo.spAssignTeacherToLab
    @TokenHash  VARBINARY(32),
    @RaumId     INT,
    @BenutzerId INT
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.spAuthorize @TokenHash, N'Administrator';

    DECLARE @RaumTyp NVARCHAR(20) = (SELECT RaumTyp FROM dbo.vRaum WHERE ID = @RaumId);
    DECLARE @UserRole NVARCHAR(20) =
        (SELECT r.Name FROM dbo.Benutzer b JOIN dbo.Rolle r ON r.ID = b.Rolle WHERE b.ID = @BenutzerId);

    IF @RaumTyp IS NULL
        THROW 50404, N'Standort nicht gefunden.', 1;
    IF @RaumTyp <> N'Labor'
        THROW 50400, N'LehrerInnen können nur Laboren zugeordnet werden.', 1;
    IF @UserRole IS NULL
        THROW 50404, N'Benutzer nicht gefunden.', 1;
    IF @UserRole <> N'LehrerIn'
        THROW 50400, N'Nur Benutzer mit der Rolle LehrerIn können zugeordnet werden.', 1;
    IF EXISTS (SELECT 1 FROM dbo.vRaum WHERE BenutzerId = @BenutzerId AND RaumTyp = N'Labor' AND ID <> @RaumId)
        THROW 50409, N'Diese LehrerIn ist bereits einem anderen Labor zugeordnet.', 1;

    UPDATE dbo.Raum SET Benutzer = @BenutzerId WHERE ID = @RaumId;
END
GO

/* DELETE /api/locations/{id}  (Administrator)
   Refused for Hauptlager/Umbuchungslager, and for rooms that still have
   stock or appear in the history. 
   Changed vs. your version: rooms with stock failed with a foreign key
   error (HTTP 500); Hauptlager/Umbuchungslager could be deleted. */
CREATE OR ALTER PROCEDURE dbo.spDeleteRoom
    @TokenHash VARBINARY(32),
    @Id        INT
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.spAuthorize @TokenHash, N'Administrator';

    DECLARE @RaumTyp NVARCHAR(20) = (SELECT RaumTyp FROM dbo.vRaum WHERE ID = @Id);

    IF @RaumTyp IS NULL
        THROW 50404, N'Standort nicht gefunden.', 1;
    IF @RaumTyp <> N'Labor'
        THROW 50409, N'Hauptlager und Umbuchungslager können nicht gelöscht werden.', 1;
    IF EXISTS (SELECT 1 FROM dbo.RaumArtikel WHERE Raum = @Id)
        THROW 50409, N'Im Standort ist noch Bestand vorhanden.', 1;
    IF EXISTS (SELECT 1 FROM dbo.Lagerbewegungen WHERE VonRaum = @Id OR ZuRaum = @Id)
        THROW 50409, N'Standort kommt in der Bewegungshistorie vor und kann nicht gelöscht werden.', 1;

    DELETE FROM dbo.Raum WHERE ID = @Id;
END
GO

/* GET /api/locations/{id}/inventory
   Administrator: any room. LehrerIn: only rooms she is responsible for,
   otherwise 403 (test case 5 of the task). */
CREATE OR ALTER PROCEDURE dbo.spGetLocationInventory
    @TokenHash VARBINARY(32),
    @RaumId    INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT, @Role NVARCHAR(20);
    EXEC dbo.spAuthorize @TokenHash, NULL, @Id = @UserId OUTPUT, @RoleName = @Role OUTPUT;

    DECLARE @OwnerId INT = (SELECT Benutzer FROM dbo.Raum WHERE ID = @RaumId);
    IF @OwnerId IS NULL
        THROW 50404, N'Standort nicht gefunden.', 1;
    IF @Role <> N'Administrator' AND @OwnerId <> @UserId
        THROW 50403, N'Kein Zugriff auf diesen Standort.', 1;

    SELECT  a.ID        AS ArtikelId,
            a.Name      AS ArtikelName,
            a.SerienNum,
            k.Name      AS KategorieName,
            h.Name      AS HerstellerName,
            ra.Menge
    FROM    dbo.RaumArtikel ra
    JOIN    dbo.Artikel   a ON a.ID = ra.Art
    JOIN    dbo.Kategorie k ON k.ID = a.Kategorie
    LEFT JOIN dbo.Hersteller h ON h.ID = a.Hersteller
    WHERE   ra.Raum = @RaumId
    ORDER BY a.Name;
END
GO


/* =====================================================================
   TRANSFERS  (Ausgabe Hauptlager -> Labor, digitale Übernahme)
   ===================================================================== */

/* POST /api/transfers  (Administrator)
   The caller only chooses lab, article and amount. Source (Hauptlager)
   and movement type ('Ausgabe') are fixed here, so nobody can book
   from or to a wrong room.
   Stock leaves the Hauptlager IMMEDIATELY (= reserved), so two
   transfers can never hand out the same pieces. 
   Changed vs. your version: it took @VonRaumId, @ZuRaumId and @BewegungId
   from the caller and did NOT touch the stock. Stock was only taken at
   confirmation, without checking it, so two open transfers could hand out
   the same pieces and the Hauptlager could go negative. */
CREATE OR ALTER PROCEDURE dbo.spCreateTransfer
    @TokenHash VARBINARY(32),
    @LaborId   INT,
    @ArtikelId INT,
    @Menge     INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        DECLARE @UserId INT;
        EXEC dbo.spAuthorize @TokenHash, N'Administrator', @Id = @UserId OUTPUT;

        IF @Menge IS NULL OR @Menge < 1
            THROW 50400, N'Die Menge muss mindestens 1 sein.', 1;

        DECLARE @SerienNum INT, @Found BIT = 0;
        SELECT @SerienNum = SerienNum, @Found = 1 FROM dbo.Artikel WHERE ID = @ArtikelId;
        IF @Found = 0
            THROW 50404, N'Artikel nicht gefunden.', 1;
        IF @SerienNum IS NOT NULL AND @Menge <> 1
            THROW 50400, N'Artikel mit Seriennummer können nur einzeln bewegt werden.', 1;

        IF NOT EXISTS (SELECT 1 FROM dbo.vRaum WHERE ID = @LaborId AND RaumTyp = N'Labor')
            THROW 50404, N'Labor nicht gefunden.', 1;

        DECLARE @HauptlagerId INT = (SELECT ID FROM dbo.vRaum WHERE RaumTyp = N'Hauptlager');
        IF @HauptlagerId IS NULL
            THROW 50409, N'Es ist kein Hauptlager angelegt.', 1;

        BEGIN TRAN;

        -- Stock leaves the Hauptlager now. Throws 50409 if not enough is there.
        DECLARE @Minus INT = -@Menge;
        EXEC dbo.spBestandAendern @HauptlagerId, @ArtikelId, @Minus;

        -- Open transfer: IsDone = 0 until the LehrerIn confirms.
        INSERT INTO dbo.Lagerbewegungen (Datum, Bewegung, Artikel, IsDone, ArtikelMenge, Benutzer, VonRaum, ZuRaum)
        VALUES (CAST(SYSDATETIME() AS DATE),
                (SELECT ID FROM dbo.BewegungArt WHERE Name = N'Ausgabe'),
                @ArtikelId, 0, @Menge, @UserId, @HauptlagerId, @LaborId);

        DECLARE @NewId INT = CAST(SCOPE_IDENTITY() AS INT);
        COMMIT;

        SELECT @NewId AS NewId;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END
GO

/* GET /api/transfers/pending
   Administrator: all open transfers. LehrerIn: only those for her labs. */
CREATE OR ALTER PROCEDURE dbo.spGetPendingTransfers
    @TokenHash VARBINARY(32)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT, @Role NVARCHAR(20);
    EXEC dbo.spAuthorize @TokenHash, NULL, @Id = @UserId OUTPUT, @RoleName = @Role OUTPUT;

    SELECT  b.ID,
            b.Datum,
            b.Artikel                AS ArtikelId,
            a.Name                   AS Artikel,
            a.SerienNum,
            ISNULL(b.ArtikelMenge, 1) AS Menge,
            b.ZuRaum                 AS LaborId,
            z.Anzeigename            AS Labor,
            z.Username               AS ZustaendigeLehrerIn,
            u.Username               AS AngelegtVon
    FROM    dbo.Lagerbewegungen b
    JOIN    dbo.BewegungArt ba ON ba.ID = b.Bewegung
    JOIN    dbo.Artikel     a  ON a.ID  = b.Artikel
    JOIN    dbo.vRaum       z  ON z.ID  = b.ZuRaum
    JOIN    dbo.Benutzer    u  ON u.ID  = b.Benutzer
    WHERE   ba.Name = N'Ausgabe'
      AND   ISNULL(b.IsDone, 0) = 0
      AND   (@Role = N'Administrator' OR z.BenutzerId = @UserId)
    ORDER BY b.ID;
END
GO

/* POST /api/transfers/{id}/confirm  (only the responsible LehrerIn)

   Protection against confirming twice:
   WITH (UPDLOCK) locks the transfer row until COMMIT. If two requests
   arrive at the same time, the second one waits at this SELECT, and
   when it continues it already sees IsDone = 1 -> 409.

   The stock is credited to the Labor now, and a 'Übernahme' row records
   who confirmed it and when. 
   Changed vs. your version: no transaction and no lock, so two confirms at
   the same moment could both pass the IsDone = 0 check and book twice.
   It also gave 403 for "already confirmed"; now 404 / 403 / 409 are separate. */
CREATE OR ALTER PROCEDURE dbo.spConfirmTransfer
    @TokenHash  VARBINARY(32),
    @TransferId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        DECLARE @UserId INT;
        EXEC dbo.spAuthorize @TokenHash, NULL, @Id = @UserId OUTPUT;

        BEGIN TRAN;

        DECLARE @ArtikelId INT, @Menge INT, @VonRaum INT, @LaborId INT,
                @IsDone BIT, @Art NVARCHAR(20), @OwnerId INT;

        SELECT  @ArtikelId = b.Artikel,
                @Menge     = ISNULL(b.ArtikelMenge, 1),
                @VonRaum   = b.VonRaum,
                @LaborId   = b.ZuRaum,
                @IsDone    = ISNULL(b.IsDone, 0),
                @Art       = ba.Name,
                @OwnerId   = r.Benutzer
        FROM    dbo.Lagerbewegungen b WITH (UPDLOCK, ROWLOCK)
        JOIN    dbo.BewegungArt ba ON ba.ID = b.Bewegung
        JOIN    dbo.Raum        r  ON r.ID  = b.ZuRaum
        WHERE   b.ID = @TransferId;

        IF @Art IS NULL OR @Art <> N'Ausgabe'
            THROW 50404, N'Übergabe nicht gefunden.', 1;
        IF @OwnerId <> @UserId
            THROW 50403, N'Nur die zuständige LehrerIn kann diese Übergabe bestätigen.', 1;
        IF @IsDone = 1
            THROW 50409, N'Diese Übergabe wurde bereits bestätigt.', 1;

        UPDATE dbo.Lagerbewegungen SET IsDone = 1 WHERE ID = @TransferId;

        EXEC dbo.spBestandAendern @LaborId, @ArtikelId, @Menge;

        INSERT INTO dbo.Lagerbewegungen (Datum, Bewegung, Artikel, IsDone, ArtikelMenge, Benutzer, VonRaum, ZuRaum)
        VALUES (CAST(SYSDATETIME() AS DATE),
                (SELECT ID FROM dbo.BewegungArt WHERE Name = N'Übernahme'),
                @ArtikelId, 1, @Menge, @UserId, @VonRaum, @LaborId);

        COMMIT;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END
GO


/* =====================================================================
   RETURNS  (Labor -> Umbuchungslager -> Hauptlager)
   ===================================================================== */

/* POST /api/returns  (only the responsible LehrerIn)
   The target is ALWAYS the Umbuchungslager; it is looked up here, not
   passed in. So a LehrerIn can never book directly into the Hauptlager. 
   Changed vs. your version: it only inserted the movement and never moved
   stock into the Umbuchungslager; the caller chose @ZuRaumId/@BewegungId. */
CREATE OR ALTER PROCEDURE dbo.spCreateReturn
    @TokenHash VARBINARY(32),
    @LaborId   INT,
    @ArtikelId INT,
    @Menge     INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        DECLARE @UserId INT;
        EXEC dbo.spAuthorize @TokenHash, NULL, @Id = @UserId OUTPUT;

        IF @Menge IS NULL OR @Menge < 1
            THROW 50400, N'Die Menge muss mindestens 1 sein.', 1;

        DECLARE @SerienNum INT, @Found BIT = 0;
        SELECT @SerienNum = SerienNum, @Found = 1 FROM dbo.Artikel WHERE ID = @ArtikelId;
        IF @Found = 0
            THROW 50404, N'Artikel nicht gefunden.', 1;
        IF @SerienNum IS NOT NULL AND @Menge <> 1
            THROW 50400, N'Artikel mit Seriennummer können nur einzeln bewegt werden.', 1;

        DECLARE @OwnerId INT = (SELECT BenutzerId FROM dbo.vRaum WHERE ID = @LaborId AND RaumTyp = N'Labor');
        IF @OwnerId IS NULL
            THROW 50404, N'Labor nicht gefunden.', 1;
        IF @OwnerId <> @UserId
            THROW 50403, N'Nur die zuständige LehrerIn kann aus diesem Labor zurückgeben.', 1;

        DECLARE @UmbuchungId INT = (SELECT ID FROM dbo.vRaum WHERE RaumTyp = N'Umbuchungslager');
        IF @UmbuchungId IS NULL
            THROW 50409, N'Es ist kein Umbuchungslager angelegt.', 1;

        BEGIN TRAN;

        DECLARE @Minus INT = -@Menge;
        EXEC dbo.spBestandAendern @LaborId, @ArtikelId, @Minus;       -- 50409 if the lab doesn't have it
        EXEC dbo.spBestandAendern @UmbuchungId, @ArtikelId, @Menge;

        INSERT INTO dbo.Lagerbewegungen (Datum, Bewegung, Artikel, IsDone, ArtikelMenge, Benutzer, VonRaum, ZuRaum)
        VALUES (CAST(SYSDATETIME() AS DATE),
                (SELECT ID FROM dbo.BewegungArt WHERE Name = N'Rückgabe'),
                @ArtikelId, 0, @Menge, @UserId, @LaborId, @UmbuchungId);

        DECLARE @NewId INT = CAST(SCOPE_IDENTITY() AS INT);
        COMMIT;

        SELECT @NewId AS NewId;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END
GO

/* GET /api/returns/pending  (Administrator) */
CREATE OR ALTER PROCEDURE dbo.spGetPendingReturns
    @TokenHash VARBINARY(32)
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.spAuthorize @TokenHash, N'Administrator';

    SELECT  b.ID,
            b.Datum,
            b.Artikel                AS ArtikelId,
            a.Name                   AS Artikel,
            a.SerienNum,
            ISNULL(b.ArtikelMenge, 1) AS Menge,
            b.VonRaum                AS VonLaborId,
            v.Anzeigename            AS VonLabor,
            u.Username               AS GebuchtVon
    FROM    dbo.Lagerbewegungen b
    JOIN    dbo.BewegungArt ba ON ba.ID = b.Bewegung
    JOIN    dbo.Artikel     a  ON a.ID  = b.Artikel
    JOIN    dbo.vRaum       v  ON v.ID  = b.VonRaum
    JOIN    dbo.Benutzer    u  ON u.ID  = b.Benutzer
    WHERE   ba.Name = N'Rückgabe'
      AND   ISNULL(b.IsDone, 0) = 0
    ORDER BY b.ID;
END
GO

/* POST /api/returns/{id}/accept  (Administrator)
   Moves the pieces Umbuchungslager -> Hauptlager, closes the return and
   logs an 'Einbuchung'. Same UPDLOCK protection as spConfirmTransfer,
   so a return can't be accepted twice. 
   Changed vs. your version: it OVERWROTE ZuRaum of the return with the
   Hauptlager (the history then said Labor -> Hauptlager, which is the
   forbidden direct return), took stock from the Labor instead of the
   Umbuchungslager, and had no transaction. */
CREATE OR ALTER PROCEDURE dbo.spAcceptReturn
    @TokenHash VARBINARY(32),
    @ReturnId  INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        DECLARE @UserId INT;
        EXEC dbo.spAuthorize @TokenHash, N'Administrator', @Id = @UserId OUTPUT;

        DECLARE @HauptlagerId INT = (SELECT ID FROM dbo.vRaum WHERE RaumTyp = N'Hauptlager');
        IF @HauptlagerId IS NULL
            THROW 50409, N'Es ist kein Hauptlager angelegt.', 1;

        BEGIN TRAN;

        DECLARE @ArtikelId INT, @Menge INT, @UmbuchungId INT, @IsDone BIT, @Art NVARCHAR(20);

        SELECT  @ArtikelId   = b.Artikel,
                @Menge       = ISNULL(b.ArtikelMenge, 1),
                @UmbuchungId = b.ZuRaum,
                @IsDone      = ISNULL(b.IsDone, 0),
                @Art         = ba.Name
        FROM    dbo.Lagerbewegungen b WITH (UPDLOCK, ROWLOCK)
        JOIN    dbo.BewegungArt ba ON ba.ID = b.Bewegung
        WHERE   b.ID = @ReturnId;

        IF @Art IS NULL OR @Art <> N'Rückgabe'
            THROW 50404, N'Rückgabe nicht gefunden.', 1;
        IF @IsDone = 1
            THROW 50409, N'Diese Rückgabe wurde bereits abgeschlossen.', 1;

        UPDATE dbo.Lagerbewegungen SET IsDone = 1 WHERE ID = @ReturnId;

        DECLARE @Minus INT = -@Menge;
        EXEC dbo.spBestandAendern @UmbuchungId, @ArtikelId, @Minus;
        EXEC dbo.spBestandAendern @HauptlagerId, @ArtikelId, @Menge;

        INSERT INTO dbo.Lagerbewegungen (Datum, Bewegung, Artikel, IsDone, ArtikelMenge, Benutzer, VonRaum, ZuRaum)
        VALUES (CAST(SYSDATETIME() AS DATE),
                (SELECT ID FROM dbo.BewegungArt WHERE Name = N'Einbuchung'),
                @ArtikelId, 1, @Menge, @UserId, @UmbuchungId, @HauptlagerId);

        COMMIT;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END
GO


/* =====================================================================
   HISTORY
   ===================================================================== */

/* GET /api/movements?raumId=&artikelId=
   Administrator: full history. LehrerIn: only movements from or to her
   own rooms (task section 2). Nothing here ever deletes a movement:
   there is deliberately no "delete movement" procedure. 
   Changed vs. your version: admin only; LehrerInnen can now see their own
   labs' movements (task section 2). Room names instead of only IDs. */
CREATE OR ALTER PROCEDURE dbo.spGetAllMovements
    @TokenHash VARBINARY(32),
    @RaumId    INT = NULL,
    @ArtikelId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UserId INT, @Role NVARCHAR(20);
    EXEC dbo.spAuthorize @TokenHash, NULL, @Id = @UserId OUTPUT, @RoleName = @Role OUTPUT;

    IF @RaumId IS NOT NULL AND @Role <> N'Administrator'
       AND NOT EXISTS (SELECT 1 FROM dbo.Raum WHERE ID = @RaumId AND Benutzer = @UserId)
        THROW 50403, N'Kein Zugriff auf diesen Standort.', 1;

    SELECT  b.ID,
            b.Datum,
            ba.Name                  AS BewegungArt,
            b.Artikel                AS ArtikelId,
            a.Name                   AS Artikel,
            a.SerienNum,
            ISNULL(b.ArtikelMenge, 1) AS Menge,
            CASE WHEN ISNULL(b.IsDone, 0) = 1 THEN N'abgeschlossen' ELSE N'offen' END AS Status,
            u.Username               AS GebuchtVon,
            b.VonRaum                AS VonRaumId,
            v.Anzeigename            AS VonRaum,
            b.ZuRaum                 AS ZuRaumId,
            z.Anzeigename            AS ZuRaum
    FROM    dbo.Lagerbewegungen b
    JOIN    dbo.BewegungArt ba ON ba.ID = b.Bewegung
    JOIN    dbo.Artikel     a  ON a.ID  = b.Artikel
    JOIN    dbo.Benutzer    u  ON u.ID  = b.Benutzer
    JOIN    dbo.vRaum       v  ON v.ID  = b.VonRaum
    JOIN    dbo.vRaum       z  ON z.ID  = b.ZuRaum
    WHERE   (@Role = N'Administrator' OR v.BenutzerId = @UserId OR z.BenutzerId = @UserId)
      AND   (@RaumId    IS NULL OR b.VonRaum = @RaumId OR b.ZuRaum = @RaumId)
      AND   (@ArtikelId IS NULL OR b.Artikel = @ArtikelId)
    ORDER BY b.ID DESC;
END
GO
