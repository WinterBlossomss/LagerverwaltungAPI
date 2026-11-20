# Lagerverwaltung – BS EVITA

Lager- und Inventarverwaltung der BS EVITA.

- **Block 1:** MVC-Webanwendung (`Lagerverwaltung/`), seit Block 2 als Client der REST-API (kein direkter Datenbankzugriff mehr)
- **Block 2:** eigenständige REST-API (`Lagerverwaltung.Api/`) mit ASP.NET Core (.NET 10),
  Stored Procedures und eigener Bearer-Token-Anmeldung (Opaque Tokens, kein JWT)

## Inhalt

| Pfad | Inhalt |
|---|---|
| `Lagerverwaltung/` | MVC-Anwendung aus Block 1, ruft jetzt die REST-API per `HttpClient` auf (`Services/Api/`) |
| `Lagerverwaltung.Api/` | REST-API: `Controllers/`, `DTOs/`, `Services/`, `Models/`, `Helpers/`, `Program.cs` |
| `Lagerverwaltung.Api/Lagerverwaltung.http` | API-Tests für Visual Studio (alle 14 Pflichtfälle) |
| `Lagerverwaltung.Api/Database/script.sql` | Datenbank und Tabellen |
| `Database/Schemaanpassungen.sql` | begründete Schemaänderungen für Block 2 (einmal ausführen) |
| `Database/StoredProcedures.sql` | alle Stored Procedures der API |
| `Docs/Tutorial.md` | Einrichtung, Architektur, Datenmodell, Stored Procedures, Authentifizierung, Ausgabe/Übernahme im Detail |
| `Docs/API-Dokumentation.md` | alle Endpunkte mit Berechtigungen, Bodies, Antworten und Statuscodes |
| `Docs/Testdokumentation.md` | Testfälle und vollständiger Ablauf mit echten API-Antworten |
| `Docs/ER-Diagramm.pdf` | ER-Diagramm |
| `Docs/*.docx` | dieselben drei Dokumente als Word-Dateien (Layout wie `Lagerverwaltung/Docs/Lagerverwaltung_Doku.docx`) |

## Schnellstart API

1. In SSMS (verbunden mit `(localdb)\MSSQLLocalDB`) ausführen:
   `script.sql` → Testdaten (Datenbank aus Block 1; die Web-App legt seit Block 2 keine Testdaten mehr an) → `Database/Schemaanpassungen.sql` → `Database/StoredProcedures.sql`
2. Pepper setzen (nie committen):
   ```powershell
   cd Lagerverwaltung.Api
   dotnet user-secrets set "Security:Pepper" "<geheimer Wert>"
   ```
3. API mit dem Profil **https** starten → `https://localhost:7154`
4. `Lagerverwaltung.Api/Lagerverwaltung.http` öffnen und die Anfragen von oben nach unten senden.

Details: [Docs/Tutorial.md](Docs/Tutorial.md)

## Web-App (MVC) starten

Die Web-App greift nicht mehr selbst auf die Datenbank zu, sondern nur über die REST-API.

1. Zuerst die API starten (siehe oben, `https://localhost:7154`).
2. Die Adresse der API steht in `Lagerverwaltung/appsettings.json` unter `Api:BaseUrl`.
3. `Lagerverwaltung` mit dem Profil **https** starten und anmelden. Der Login geht an `POST /api/auth/login`;
   das Token liegt im verschlüsselten, HttpOnly-Anmeldecookie und läuft zusammen mit dem Token ab.

Tipp: In Visual Studio unter *Startprojekte konfigurieren* beide Projekte gleichzeitig starten.
