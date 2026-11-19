# Testdokumentation – Lagerverwaltung REST-API

Alle Tests stehen in `Lagerverwaltung.Api/Lagerverwaltung.http` (90 Anfragen, Abschnitte 1–10).
Die Antworten unten stammen aus einem echten Lauf vom **19.11.2026**.

---

## 1. Testumgebung

| | |
|---|---|
| Datenbank | frisch erstellt: `script.sql` → Testdaten der MVC-App (`SeedService`) → `Schemaanpassungen.sql` → `StoredProcedures.sql` |
| API | `Lagerverwaltung.Api`, Profil **https**, `https://localhost:7154`, Environment *Development* |
| Werkzeug | Visual Studio `.http`-Datei (Anfragen von oben nach unten senden) |
| Ergebnis | **90 von 90 Anfragen** lieferten den erwarteten Statuscode. Ein zweiter Lauf auf derselben Datenbank ebenfalls 90/90 (die Datei ist wiederholbar) |

### Testbenutzer

| Benutzer | Rolle | Labor | Herkunft |
|---|---|---|---|
| `ADMIN` | Administrator | – (zuständig für Hauptlager und Umbuchungslager) | Testdaten |
| `Dennis` | LehrerIn | Raum 309 (ID 4) | Testdaten |
| `Emily` | LehrerIn | Raum 310 (ID 5) | Testdaten |
| `testlehrer` | LehrerIn | – | wird in Test 1.1 registriert |

Die Kennwörter in der `.http`-Datei sind **Platzhalter für die lokalen Testdaten**, keine echten Zugangsdaten.
Tokens werden nie in die Datei geschrieben, sondern aus der Login-Antwort gelesen:

```http
# @name loginAdmin
POST {{baseUrl}}/api/auth/login
...
Authorization: Bearer {{loginAdmin.response.body.$.token}}
```

In den Antworten unten ist das Token durch `<TOKEN>` ersetzt.

---

## 2. Pflicht-Testfälle

| Fall | Beschreibung | Test(s) in `.http` | Erwartet | Ergebnis |
|---|---|---|---|---|
| 1 | Erfolgreiche Registrierung und Anmeldung | 1.1, 1.4 | 201, 200 + Token | ✅ 201, 200 |
| 2 | Anmeldung mit falschem Kennwort | 1.5 (und 1.6 unbekannter Benutzer) | 401 | ✅ 401 (gleiche Meldung) |
| 3 | Geschützter Endpunkt ohne Token | 2.1 (2.2 ungültiges Token) | 401 | ✅ 401 |
| 4 | LehrerIn ruft Administratorfunktion auf | 2.4, 2.5, 9.2 | 403 | ✅ 403 |
| 5 | LehrerIn greift auf fremdes Labor zu | 2.10, 2.11 (6.3) | 403 | ✅ 403 |
| 6 | Administrator erstellt und ändert einen Artikel | 4.2, 4.3, 4.4 | 201, 204, 200 | ✅ |
| 7 | Artikel mit Seriennummer | 4.6, 4.7, 4.8, 4.9 | 201, 200, 409, 400 | ✅ |
| 8 | Artikel ohne Seriennummer (Menge) | 4.2, 4.5 | 201, 200 | ✅ |
| 9 | Ausgabe anlegen und digital bestätigen | 5.4, 5.5, 5.7 | 201, 200, 204 | ✅ |
| 10 | Rückgabe über Umbuchungslager ins Hauptlager | 5.12, 5.14, 5.16 | 201, 200, 204 | ✅ |
| 11 | Buchung bei zu geringem Bestand | 5.2, 5.10 | 409 | ✅ 409 |
| 12 | Bestätigte Übergabe erneut bestätigen | 5.8 | 409 | ✅ 409 |
| 13 | Ungültige Eingaben | 1.2, 1.7, 3.4, 4.10, 5.3, 9.5 | 400 | ✅ 400 |
| 14 | Bewegungshistorie enthält die Aktionen | 6.1, 6.2 | 200 | ✅ |

### Ausgewählte Antworten

**Fall 1 – Registrierung (1.1)**

```http
POST /api/auth/register
{ "username": "testlehrer", "password": "TESTPASSWORT_123" }
```
```json
201 Created
{ "id": 6, "username": "testlehrer", "role": "LehrerIn" }
```

Test 1.3 schickt zusätzlich `"role": "Administrator", "rolle": 1` mit – die Antwort ist trotzdem
`"role": "LehrerIn"`. Die Registrierung kann nie einen Administrator erzeugen.

**Fall 1 – Anmeldung (1.4)**

```json
200 OK
{ "token": "<TOKEN>", "expiresAtUtc": "2026-11-19T12:43:30.0947347Z",
  "userId": 6, "username": "testlehrer", "role": "LehrerIn" }
```

**Fall 2 – falsches Kennwort (1.5) und unbekannter Benutzer (1.6)**

```json
401 Unauthorized
{ "error": "Benutzername oder Kennwort falsch." }
```

Beide Fälle liefern dieselbe Meldung, damit man nicht herausfinden kann, welche Benutzernamen existieren.

**Fall 3 – ohne Token (2.1)**

```http
GET /api/users/me
```
```json
401 Unauthorized
{ "error": "Nicht angemeldet oder Token ungültig/abgelaufen." }
```

**Fall 4 – LehrerIn Dennis ruft die Benutzerliste auf (2.4)**

```json
403 Forbidden
{ "error": "Keine Berechtigung für diese Aktion." }
```

**Fall 5 – Dennis öffnet den Bestand von Emilys Labor (2.10)**

```http
GET /api/locations/5/inventory
```
```json
403 Forbidden
{ "error": "Kein Zugriff auf diesen Standort." }
```

Sein eigenes Labor (`/api/locations/4/inventory`, Test 2.9) liefert `200` mit dem Bestand.

**Fall 7 – Seriennummer: dasselbe Gerät zweimal einbuchen (4.8)**

```json
409 Conflict
{ "error": "Dieses Gerät (Seriennummer) ist bereits im Bestand." }
```

**Fall 7 – Seriennummer: 2 Stück ausgeben (4.9)**

```json
400 Bad Request
{ "error": "Artikel mit Seriennummer können nur einzeln bewegt werden." }
```

**Fall 11 – mehr ausgeben als vorhanden (5.2)**

```http
POST /api/transfers
{ "laborId": 4, "artikelId": 24, "menge": 999 }
```
```json
409 Conflict
{ "error": "Nicht genügend Bestand vorhanden." }
```

**Fall 12 – dieselbe Übergabe noch einmal bestätigen (5.8)**

```json
409 Conflict
{ "error": "Diese Übergabe wurde bereits bestätigt." }
```

**Fall 13 – Menge 0 (5.3)**

```json
400 Bad Request
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "Menge": [ "The field Menge must be between 1 and 100000." ] }
}
```

---

## 3. Vollständiger Ablauf

**Hauptlager → Ausgabe → digitale Übernahme → Labor → Rückgabe → Umbuchungslager → Hauptlager**

Testartikel: **„Test-Kabel HDMI 3m“ (ID 24)**, ein Mengenartikel ohne Seriennummer, angelegt in Test 4.2.
Labor: **Raum 309 (ID 4)**, zuständig ist **Dennis**.

### Bestandsübersicht

| Schritt | Test | Aktion | Hauptlager | Labor 309 | Umbuchungslager | Gesamt |
|---|---|---|---|---|---|---|
| 0 | 4.5 | Zugang: 10 Stück | **10** | 0 | 0 | 10 |
| 1 | 5.4 | Ausgabe 4 an Labor 309 (offen) | **6** | 0 | 0 | 6 + 4 in offener Ausgabe |
| 2 | 5.7 | Dennis bestätigt | 6 | **4** | 0 | 10 |
| 3 | 5.12 | Dennis gibt 3 zurück | 6 | **1** | **3** | 10 |
| 4 | 5.16 | Admin nimmt Rückgabe an | **9** | 1 | **0** | 10 |

Die Gesamtmenge bleibt immer 10 – nichts wird doppelt gezählt.

### Schritt 0 – Zugang ins Hauptlager (4.5)

```http
POST /api/articles/24/stock
Authorization: Bearer <TOKEN ADMIN>

{ "menge": 10 }
```
```json
200 OK
{ "movementId": 56 }
```

Bestand Hauptlager (5.1, `GET /api/locations/1/inventory`, Ausschnitt):

```json
{ "artikelId": 24, "artikelName": "Test-Kabel HDMI 3m", "serienNum": null, "menge": 10 }
```

### Schritt 1 – Ausgabe aus dem Hauptlager (5.4)

```http
POST /api/transfers
Authorization: Bearer <TOKEN ADMIN>

{ "laborId": 4, "artikelId": 24, "menge": 4 }
```
```json
201 Created
{ "id": 58 }
```

Hauptlager 10 → **6**. Die 4 Stück sind reserviert und stehen in der offenen Ausgabe.

Dennis sieht die offene Übergabe (5.5, `GET /api/transfers/pending`, Ausschnitt):

```json
{ "id": 58, "datum": "2026-11-19T00:00:00", "artikelId": 24, "artikel": "Test-Kabel HDMI 3m",
  "serienNum": null, "menge": 4, "laborId": 4, "labor": "Raum 309",
  "zustaendigeLehrerIn": "Dennis", "angelegtVon": "ADMIN" }
```

Emily (nicht zuständig) versucht zu bestätigen (5.6):

```json
403 Forbidden
{ "error": "Nur die zuständige LehrerIn kann diese Übergabe bestätigen." }
```

### Schritt 2 – Digitale Übernahme (5.7)

```http
POST /api/transfers/58/confirm
Authorization: Bearer <TOKEN DENNIS>
```
```
204 No Content
```

Labor 309: 0 → **4** (5.9, `GET /api/locations/4/inventory`, Ausschnitt):

```json
{ "artikelId": 24, "artikelName": "Test-Kabel HDMI 3m", "serienNum": null, "menge": 4 }
```

Zweite Bestätigung (5.8) → `409 "Diese Übergabe wurde bereits bestätigt."`

### Schritt 3 – Rückgabe ins Umbuchungslager (5.12)

Zuerst die Fehlerfälle: 50 Stück zurückgeben (5.10) → `409 "Nicht genügend Bestand vorhanden."`,
Emily gibt aus Dennis' Labor zurück (5.11) → `403 "Nur die zuständige LehrerIn kann aus diesem Labor zurückgeben."`

```http
POST /api/returns
Authorization: Bearer <TOKEN DENNIS>

{ "laborId": 4, "artikelId": 24, "menge": 3 }
```
```json
201 Created
{ "id": 60 }
```

Labor 309: 4 → **1**, Umbuchungslager: 0 → **3** (5.15, `GET /api/locations/2/inventory`, Ausschnitt):

```json
{ "artikelId": 24, "artikelName": "Test-Kabel HDMI 3m", "serienNum": null, "menge": 3 }
```

Dennis darf offene Rückgaben nicht sehen (5.13) → `403`. Der Administrator sieht sie (5.14):

```json
{ "id": 60, "datum": "2026-11-19T00:00:00", "artikelId": 24, "artikel": "Test-Kabel HDMI 3m",
  "serienNum": null, "menge": 3, "vonLaborId": 4, "vonLabor": "Raum 309", "gebuchtVon": "Dennis" }
```

### Schritt 4 – Einbuchung ins Hauptlager (5.16)

```http
POST /api/returns/60/accept
Authorization: Bearer <TOKEN ADMIN>
```
```
204 No Content
```

Umbuchungslager 3 → **0** (Zeile verschwindet), Hauptlager 6 → **9** (5.18, Ausschnitt):

```json
{ "artikelId": 24, "artikelName": "Test-Kabel HDMI 3m", "serienNum": null, "menge": 9 }
```

Zweites Annehmen (5.17) → `409 "Diese Rückgabe wurde bereits abgeschlossen."`

### Historie des Artikels (6.1, Fall 14)

```http
GET /api/movements?artikelId=24
Authorization: Bearer <TOKEN ADMIN>
```

| ID | Art | Menge | Status | Gebucht von | Von | Nach |
|---|---|---|---|---|---|---|
| 61 | Einbuchung | 3 | abgeschlossen | ADMIN | Umbuchungslager | Hauptlager |
| 60 | Rückgabe | 3 | abgeschlossen | Dennis | Raum 309 | Umbuchungslager |
| 59 | Übernahme | 4 | abgeschlossen | Dennis | Hauptlager | Raum 309 |
| 58 | Ausgabe | 4 | abgeschlossen | ADMIN | Hauptlager | Raum 309 |
| 56 | Zugang | 10 | abgeschlossen | ADMIN | Hauptlager | Hauptlager |

Erster Eintrag der echten Antwort:

```json
{ "id": 61, "datum": "2026-11-19T00:00:00", "bewegungArt": "Einbuchung",
  "artikelId": 24, "artikel": "Test-Kabel HDMI 3m", "serienNum": null, "menge": 3,
  "status": "abgeschlossen", "gebuchtVon": "ADMIN",
  "vonRaumId": 2, "vonRaum": "Umbuchungslager", "zuRaumId": 1, "zuRaum": "Hauptlager" }
```

Jeder Schritt des Ablaufs steht in der Historie. Die Ausgabe (58) war bis zur Übernahme „offen“, die
Rückgabe (60) bis zur Einbuchung. Dennis sieht mit `GET /api/movements` (6.2) nur Bewegungen seines
eigenen Labors; ein Filter auf Emilys Labor (6.3) ergibt `403`.

---

## 4. Weitere Tests

| Abschnitt | Inhalt | Ergebnis |
|---|---|---|
| 3 Kategorien | Anzeigen, erstellen, ändern, leerer Name (400), nicht vorhanden (404) | ✅ |
| 4.11, 4.12 | Artikel mit Bestand löschen (409), benutzte Kategorie löschen (409) | ✅ |
| 7 Standorte | Liste, Labor anlegen, Raumnummer ändern, LehrerIn mit zweitem Labor (409), Zuordnung, Hauptlager löschen (409), leeres Labor löschen (204) | ✅ |
| 8 Benutzer | Benutzerliste, unbekannte Rolle (400), Rolle ändern, Benutzer mit Historie löschen (409), falsches aktuelles Kennwort (400), Kennwort ändern, eigenes Konto löschen, Login danach (401) | ✅ |
| 9 Hersteller | Anzeigen (LehrerIn 200), LehrerIn erstellt (403), erstellen (201), doppelter Name (409), leerer Name (400), ändern (204), nicht vorhanden (404), Artikel mit Hersteller anlegen und danach filtern, benutzten Hersteller löschen (409), unbenutzten löschen (204), nochmals löschen (404) | ✅ |
| 10 Logout | Logout (204), Token danach ungültig (401), zweiter Logout (401) | ✅ |

### Gleichzeitige Bestätigungen

Zusätzlich zur `.http`-Datei wurde geprüft, dass eine Übergabe nicht doppelt gebucht werden kann, wenn
mehrere Anfragen **gleichzeitig** kommen: 5 parallele `POST /api/transfers/{id}/confirm` → genau **1 × 204**
und **4 × 409**. Der Laborbestand wurde nur einmal erhöht (Sperre `WITH (UPDLOCK, ROWLOCK)` in
`spConfirmTransfer`).

---

## 5. Tests selbst ausführen

1. Datenbank wie in [Tutorial.md](Tutorial.md#12-datenbank-einrichten) einrichten.
2. Pepper setzen: `dotnet user-secrets set "Security:Pepper" "<geheim>"` (im Ordner `Lagerverwaltung.Api`).
3. API mit dem Profil **https** starten.
4. `Lagerverwaltung.http` öffnen und die Anfragen **von oben nach unten** senden
   (die Logins in 1.8–1.10 liefern die Tokens für alle weiteren Anfragen).
5. Die Überschrift jeder Anfrage nennt den erwarteten Statuscode, z. B. `### 5.8 … -> 409`.
