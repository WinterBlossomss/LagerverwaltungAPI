/* =====================================================================
   Schemaanpassungen.sql  -  Block 2 (REST-API)

   Small, documented changes to the Block 1 schema that the REST API
   needs. Run this ONCE on the database Lagerverwaltungssystem, BEFORE
   StoredProcedures.sql.

   Every change is explained, because the task says schema additions
   must be justified and documented (and shown in the ER diagram).

   Assumptions (taken from the EF models in the repo, please check):
   - Tables (from your script.sql): Benutzer, Rolle, Raum, RaumTyp, RaumArtikel, Artikel,
     Kategorie, Hersteller, Lagerbewegungen, BewegungArt, ApiTokens
   - Role names:      'Administrator', 'LehrerIn'
   - Room types:      'Hauptlager', 'Umbuchungslager', 'Labor'
   - Movement types:  'Zugang', 'Ausgabe', 'Übernahme', 'Rückgabe',
                      'Einbuchung', 'Stornieren'
   ===================================================================== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;   -- required for the filtered index below
SET XACT_ABORT ON;
BEGIN TRAN;

/* ---------------------------------------------------------------------
   1) ApiTokens: make it able to store a proper token

   Why:
   - RevokedAt was NOT NULL, so there was no way to say "this token is
     NOT revoked". NULL = still valid, a date = revoked at that time.
   - TokenHash, UserId and ExpiresAt must always be set, otherwise a
     token without owner or without expiry could exist.
   - Foreign key to Benutzer: a token always belongs to a real user.
   - UNIQUE index on TokenHash: every login lookup searches by hash, so
     it must be fast, and two identical hashes must never exist.

   Old tokens are deleted first. That is safe: tokens are temporary,
   users just log in again.
   --------------------------------------------------------------------- */
DELETE FROM dbo.ApiTokens;

ALTER TABLE dbo.ApiTokens ALTER COLUMN TokenHash VARBINARY(32) NOT NULL;
ALTER TABLE dbo.ApiTokens ALTER COLUMN UserId    INT           NOT NULL;
ALTER TABLE dbo.ApiTokens ALTER COLUMN ExpiresAt DATETIME2     NOT NULL;
ALTER TABLE dbo.ApiTokens ALTER COLUMN RevokedAt DATETIME2     NULL;

IF COL_LENGTH('dbo.ApiTokens', 'CreatedAt') IS NOT NULL
    ALTER TABLE dbo.ApiTokens ALTER COLUMN CreatedAt DATETIME2 NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_ApiTokens_CreatedAt')
    ALTER TABLE dbo.ApiTokens ADD CONSTRAINT DF_ApiTokens_CreatedAt DEFAULT SYSUTCDATETIME() FOR CreatedAt;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ApiTokens_Benutzer')
    ALTER TABLE dbo.ApiTokens ADD CONSTRAINT FK_ApiTokens_Benutzer
        FOREIGN KEY (UserId) REFERENCES dbo.Benutzer(ID);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_ApiTokens_TokenHash')
    CREATE UNIQUE INDEX UX_ApiTokens_TokenHash ON dbo.ApiTokens(TokenHash);

/* ---------------------------------------------------------------------
   2) RaumArtikel: stock can never be negative

   Why: the procedures already refuse negative stock. This CHECK is a
   safety net in the database itself: even a buggy procedure can't
   write a negative amount.
   --------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_RaumArtikel_Menge')
    ALTER TABLE dbo.RaumArtikel ADD CONSTRAINT CK_RaumArtikel_Menge CHECK (Menge >= 0);

/* ---------------------------------------------------------------------
   3) Lagerbewegungen.Artikel: foreign key to Artikel

   Why: the Block 1 model has no FK here. Without it, an article could
   be deleted although movements still point to it, and the history
   would lose information. With the FK the database refuses that.
   --------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Lagerbewegungen_Artikel')
    ALTER TABLE dbo.Lagerbewegungen ADD CONSTRAINT FK_Lagerbewegungen_Artikel
        FOREIGN KEY (Artikel) REFERENCES dbo.Artikel(ID);

/* ---------------------------------------------------------------------
   4) Unique usernames and unique serial numbers

   Why: login searches by username, so two users with the same name
   must be impossible. A serial number identifies exactly one device.
   The serial index is "filtered": it ignores NULL, because many
   articles have no serial number.

   If this step fails, your test data contains duplicates: fix those
   rows first.
   --------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Benutzer_Username')
    CREATE UNIQUE INDEX UX_Benutzer_Username ON dbo.Benutzer(Username);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Artikel_SerienNum')
    CREATE UNIQUE INDEX UX_Artikel_SerienNum ON dbo.Artikel(SerienNum)
        WHERE SerienNum IS NOT NULL;

COMMIT;
GO
