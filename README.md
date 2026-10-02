# Lagerverwaltung

Webapplikation zur Verwaltung von Hauptlager, Umbuchungslager und Laboren an der BS EVITA. Administratoren geben Gegenstände aus dem Hauptlager an Labore aus, LehrerInnen bestätigen die Übernahme digital und geben Gegenstände über das Umbuchungslager zurück. Jede Bewegung wird lückenlos protokolliert.

**ASP.NET Core MVC · .NET 10 · Entity Framework Core 10 · SQL Server LocalDB**

## Funktionen

- **Hauptlager → Labor:** Ausgabe durch den Admin, digitale Übernahme durch die LehrerIn
- **Labor → Umbuchungslager → Hauptlager:** Rückgabe durch die LehrerIn (stornierbar), Entgegennahme und Einbuchung durch den Admin
- **Inventar:** Gesamtbestand aller Räume inkl. offener Übergaben, filterbar nach Bezeichnung, Seriennummer, Kategorie, Raum und LehrerIn
- **Bewegungshistorie:** vollständige Kette pro Gegenstand (Zugang, Ausgabe, Übernahme, Rückgabe, Einbuchung, Stornieren)
- **Stammdaten:** Artikel, Kategorien, Hersteller und Benutzer (CRUD)
- Artikel **mit Seriennummer** (Einzelgeräte) und **ohne Seriennummer** (Mengenartikel)

## Rollen

| Rolle | Darf |
|---|---|
| Administrator | Stammdaten, Benutzer, Inventar, Ausgabe, Umbuchungslager, Bewegungen |
| LehrerIn | eigenes Labor einsehen, Übergaben bestätigen, Rückgaben anlegen und stornieren |

Eine LehrerIn ist für genau ein Labor zuständig und bucht nie direkt ins Hauptlager.

## Voraussetzungen

- Visual Studio 2022 (aktuelle Version) mit .NET 10 SDK
- SQL Server LocalDB (`(localdb)\MSSQLLocalDB`)

## Einrichtung

1. Repository klonen und `Lagerverwaltung.csproj` in Visual Studio öffnen.
2. In LocalDB die Datenbank `Lagerverwaltungssystem` mit den Tabellen laut ER-Modell (`Docs/ER_Modell.drawio.png`) anlegen.
3. Pepper für das Passwort-Hashing in den User Secrets setzen:
   ```bash
   dotnet user-secrets set "Security:Pepper" "<beliebiger geheimer Wert>"
   ```
   Ohne diesen Wert bricht die Anwendung beim Start mit „Pepper is not configured.“ ab.
4. Anwendung mit dem Profil `https` starten (F5) → `https://localhost:7006`.
   Beim ersten Start befüllt der `SeedService` die Datenbank mit Stamm- und Testdaten.
5. Einmalig `Docs/EinLaborProLehrer.sql` ausführen (Unique-Index: ein Labor pro LehrerIn).

## Testzugänge

| Benutzer | Passwort | Rolle |
|---|---|---|
| ADMIN | Admin123! | Administrator |
| Dennis | 123 | LehrerIn (Raum 309) |
| Emily | 123 | LehrerIn (Raum 310) |
| Michael | 123 | LehrerIn (Raum 311) |
| Sarah | 123 | LehrerIn (Raum 312) |

## Datenbank zurücksetzen

`Docs/AlleDatenLoeschen.sql` löscht alle Daten und setzt die IDENTITY-Zähler zurück; das Schema bleibt erhalten. Beim nächsten Start legt der `SeedService` die Testdaten neu an.

## Projektstruktur

```
Lagerverwaltung/
├── Controllers/   Account, Artikel, Benutzer, Bewegungen, Dashboard, Hersteller, Home,
│                  Inventar, Kategorie, Labor, Lager, Rueckgabe, Uebergabe, Umbuchung
├── Docs/          ER-Modell, SQL-Skripte, Dokumentation
├── Models/        DbContext und Entitäten (Database First, EF Core Power Tools)
├── Services/      LagerService, PepperedPasswordHasher, SeedService
├── ViewModels/
├── Views/
├── wwwroot/
├── appsettings.json
└── Program.cs
```

## Architektur in Kürze

- **Database First:** Models wurden mit EF Core Power Tools aus der bestehenden Datenbank erzeugt; eigene Ergänzungen liegen in `*_Partial.cs`.
- **LagerService:** einzige Stelle, an der Bestände (`RaumArtikel`) und Lagerbewegungen gemeinsam geschrieben werden. Regelverstöße werfen eine `LagerException`.
- **Sicherheit:** Cookie-Authentifizierung, Rollenprüfung per `[Authorize]`, zusätzliche Zuständigkeitsprüfung in Controller und Service, Anti-Forgery-Token auf allen ändernden Aktionen, gesalzene und gepepperte PBKDF2-Hashes.
- **Historie:** Bewegungen werden nie gelöscht, nur `IsDone` wird gesetzt.

Ausführliche Beschreibung: `Docs/LagerverwaltungDokumentation.docx`.
