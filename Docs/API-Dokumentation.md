# API-Dokumentation – Lagerverwaltung REST-API

Diese Dokumentation beschreibt alle 37 Endpunkte der REST-API `Lagerverwaltung.Api` (Block 2).
Alle Beispiele stammen aus einem echten Testlauf der Datei `Lagerverwaltung.Api/Lagerverwaltung.http`
gegen eine frisch aufgesetzte Datenbank (siehe [Testdokumentation.md](Testdokumentation.md)).

- **Basis-URL (Entwicklung):** `https://localhost:7154` (HTTP `http://localhost:5148` leitet auf HTTPS um)
- **Datenformat:** JSON (Request und Response), Feldnamen in camelCase
- **Authentifizierung:** `Authorization: Bearer <token>` (Opaque Token, kein JWT)

---

## 1. Überblick aller Endpunkte

| # | Methode | Route | Berechtigung | Funktion |
|---|---|---|---|---|
| 1 | POST | `/api/auth/register` | öffentlich | Benutzer registrieren (immer Rolle *LehrerIn*) |
| 2 | POST | `/api/auth/login` | öffentlich | Anmelden, Bearer-Token erhalten |
| 3 | POST | `/api/auth/logout` | angemeldet | Eigenes Token widerrufen |
| 4 | GET | `/api/users/me` | angemeldet | Eigene Daten |
| 5 | PUT | `/api/users/me` | angemeldet | Eigenen Benutzernamen / eigenes Kennwort ändern |
| 6 | DELETE | `/api/users/me` | angemeldet | Eigenes Konto löschen (wenn fachlich zulässig) |
| 7 | GET | `/api/users` | Administrator | Benutzerliste |
| 8 | PUT | `/api/users/{id}/role` | Administrator | Rolle ändern |
| 9 | DELETE | `/api/users/{id}` | Administrator | Benutzer löschen (wenn fachlich zulässig) |
| 10 | GET | `/api/categories` | angemeldet | Kategorien anzeigen |
| 11 | POST | `/api/categories` | Administrator | Kategorie erstellen |
| 12 | PUT | `/api/categories/{id}` | Administrator | Kategorie ändern |
| 13 | DELETE | `/api/categories/{id}` | Administrator | Kategorie löschen (nur wenn unbenutzt) |
| 14 | GET | `/api/articles` | angemeldet | Artikel suchen und filtern |
| 15 | GET | `/api/articles/{id}` | angemeldet | Artikeldetails |
| 16 | POST | `/api/articles` | Administrator | Artikel erstellen |
| 17 | PUT | `/api/articles/{id}` | Administrator | Artikel ändern |
| 18 | DELETE | `/api/articles/{id}` | Administrator | Artikel löschen (nur ohne Bestand und Historie) |
| 19 | POST | `/api/articles/{id}/stock` | Administrator | **Zugang**: neue Stück ins Hauptlager buchen |
| 20 | GET | `/api/locations` | Administrator | Alle Lager und Labore |
| 21 | GET | `/api/locations/mine` | angemeldet | Eigene Standorte (für LehrerIn: das eigene Labor) |
| 22 | POST | `/api/locations` | Administrator | Lager / Labor erstellen |
| 23 | PUT | `/api/locations/{id}` | Administrator | Raumnummer ändern |
| 24 | PUT | `/api/locations/{id}/teacher` | Administrator | Zuständige LehrerIn einem Labor zuordnen |
| 25 | DELETE | `/api/locations/{id}` | Administrator | Standort löschen (nur leer und ohne Historie) |
| 26 | GET | `/api/locations/{id}/inventory` | Administrator oder zuständige LehrerIn | Bestand eines Standorts |
| 27 | POST | `/api/transfers` | Administrator | Ausgabe Hauptlager → Labor anlegen |
| 28 | GET | `/api/transfers/pending` | Administrator (alle) / LehrerIn (eigenes Labor) | Offene Übergaben |
| 29 | POST | `/api/transfers/{id}/confirm` | zuständige LehrerIn | Digitale Übernahme bestätigen |
| 30 | POST | `/api/returns` | zuständige LehrerIn | Rückgabe Labor → Umbuchungslager |
| 31 | GET | `/api/returns/pending` | Administrator | Offene Rückgaben |
| 32 | POST | `/api/returns/{id}/accept` | Administrator | Rückgabe annehmen: Umbuchungslager → Hauptlager |
| 33 | GET | `/api/movements` | Administrator (alle) / LehrerIn (eigenes Labor) | Bewegungshistorie |
| 34 | GET | `/api/manufacturers` | angemeldet | Hersteller anzeigen |
| 35 | POST | `/api/manufacturers` | Administrator | Hersteller erstellen |
| 36 | PUT | `/api/manufacturers/{id}` | Administrator | Hersteller ändern |
| 37 | DELETE | `/api/manufacturers/{id}` | Administrator | Hersteller löschen (nur wenn unbenutzt) |

Zusätzlich zur Aufgabenstellung gibt es `POST /api/auth/logout`, `GET /api/locations/mine`,
`PUT /api/locations/{id}/teacher` (die geforderte Zuordnungsfunktion) und `POST /api/articles/{id}/stock`
(ohne Zugang gäbe es keinen Bestand, den man ausgeben könnte) sowie die Hersteller-Verwaltung `/api/manufacturers`
(Artikel verweisen auf einen Hersteller, also muss man Hersteller auch anlegen und pflegen können).

---

## 2. Authentifizierung

### 2.1 Ablauf

1. `POST /api/auth/login` mit Benutzername und Kennwort.
2. Die API prüft das Kennwort (PBKDF2 + Salt + Pepper) und erzeugt ein **zufälliges Token** (32 Byte, Base64Url).
3. In der Tabelle `ApiTokens` wird **nur der SHA-256-Hash** des Tokens gespeichert, mit Ablaufzeit (60 Minuten).
4. Das Token wird **einmal** im Klartext zurückgegeben.
5. Bei jeder weiteren Anfrage sendet der Client den Header `Authorization: Bearer <token>`.
6. Die API berechnet SHA-256 des Tokens und übergibt den Hash an die Stored Procedure.
   Die Prozedur `spAuthorize` prüft: Token vorhanden, nicht widerrufen, nicht abgelaufen, Rolle passt.
7. `POST /api/auth/logout` setzt `RevokedAt` – das Token ist sofort ungültig.

Das Token enthält **keine** Benutzerdaten und keine Rolle. Wer dazu gehört, weiß nur die Datenbank.

### 2.2 Token in Visual Studio verwenden

In `Lagerverwaltung.http` wird das Token nicht abgeschrieben, sondern aus der Login-Antwort gelesen:

```http
# @name loginAdmin
POST {{baseUrl}}/api/auth/login
Content-Type: application/json

{ "username": "ADMIN", "password": "Admin123!" }

###
GET {{baseUrl}}/api/users
Authorization: Bearer {{loginAdmin.response.body.$.token}}
```

Zuerst die Login-Anfrage senden, danach funktionieren alle Anfragen, die `{{loginAdmin...}}` verwenden.

### 2.3 Rollen und Laborberechtigung

| Rolle | Darf |
|---|---|
| **Administrator** | Alles: Benutzer, Kategorien, Artikel, Standorte, Ausgaben anlegen, Rückgaben annehmen, gesamte Historie |
| **LehrerIn** | Kategorien und Artikel lesen, **nur das eigene Labor** sehen (Bestand, Übergaben, Historie), Übergaben an das eigene Labor bestätigen, aus dem eigenen Labor zurückgeben |

- Die Rolle wird **nie** vom Client übernommen. `register` erstellt immer eine *LehrerIn*, auch wenn
  der Body `"role": "Administrator"` enthält (Test 1.3).
- Ein Labor gehört genau einer LehrerIn (`Raum.Benutzer`). Zugriff auf ein fremdes Labor ergibt **403**.

---

## 3. Fehlerbehandlung und Statuscodes

Alle Fehler haben dasselbe Format:

```json
{ "error": "Kein Zugriff auf diesen Standort." }
```

Ausnahme: Validierungsfehler (Attribute in den DTOs) liefern das ASP.NET-Standardformat mit der Liste der Felder:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "Menge": [ "The field Menge must be between 1 and 100000." ] }
}
```

| Status | Bedeutung | Entsteht durch |
|---|---|---|
| 200 OK | Daten werden zurückgegeben | GET, Login, Zugang |
| 201 Created | Neuer Datensatz, Body `{ "id": … }` | POST (Erstellen) |
| 204 No Content | Erfolgreich, kein Body | PUT, DELETE, confirm, accept, logout |
| 400 Bad Request | Ungültige Eingabe | DTO-Validierung oder `THROW 50400` in der Prozedur |
| 401 Unauthorized | Kein / ungültiges / abgelaufenes / widerrufenes Token, falsches Login | `THROW 50401` (spAuthorize), Login |
| 403 Forbidden | Angemeldet, aber keine Berechtigung (Rolle oder fremdes Labor) | `THROW 50403` |
| 404 Not Found | Datensatz existiert nicht | `THROW 50404` |
| 409 Conflict | Fachliche Regel verletzt (zu wenig Bestand, doppelt bestätigt, Löschen nicht erlaubt, Name schon vergeben) | `THROW 50409` |
| 500 Internal Server Error | Unerwarteter Datenbankfehler (wird geloggt, keine Details nach außen) | SqlExceptionFilter |

**So wird aus einem SQL-Fehler ein Statuscode:** Die Prozeduren melden Fehler mit `THROW 504xx`.
`ApiControllerBase.Run()` fängt diese `SqlException` und antwortet mit `ex.Number - 50000`,
z. B. `THROW 50403` → HTTP 403. Andere SQL-Fehler gehen an den `SqlExceptionFilter` (500).

**Login:** Falscher Benutzername und falsches Kennwort geben **dieselbe** Antwort
(`401 "Benutzername oder Kennwort falsch."`), damit niemand testen kann, welche Benutzernamen existieren.

---

## 4. Endpunkte im Detail

### 4.1 Authentifizierung – `/api/auth`

#### POST /api/auth/register  (öffentlich)

| Feld | Typ | Regeln |
|---|---|---|
| `username` | string | Pflicht, 3–20 Zeichen, eindeutig |
| `password` | string | Pflicht, 8–100 Zeichen |

```json
{ "username": "testlehrer", "password": "TESTPASSWORT_123" }
```

`201 Created` (Location: `/api/users/me`)

```json
{ "id": 6, "username": "testlehrer", "role": "LehrerIn" }
```

Fehler: `400` (Validierung), `409` „Benutzername ist bereits vergeben.“

#### POST /api/auth/login  (öffentlich)

```json
{ "username": "ADMIN", "password": "Admin123!" }
```

`200 OK`

```json
{
  "token": "<TOKEN>",
  "expiresAtUtc": "2026-11-19T12:43:30.3564944Z",
  "userId": 1,
  "username": "ADMIN",
  "role": "Administrator"
}
```

Fehler: `400` (leere Felder), `401` „Benutzername oder Kennwort falsch.“

Benutzer aus Block 1 haben noch das alte Hash-Format (ASP.NET PasswordHasher). Beim ersten
erfolgreichen Login wird automatisch ein neuer PBKDF2-Hash mit neuem Salt gespeichert.

#### POST /api/auth/logout  (angemeldet)

Kein Body. `204 No Content`. Danach liefert dasselbe Token `401`.

### 4.2 Benutzer – `/api/users`

#### GET /api/users/me  (angemeldet)

`200 OK`

```json
{ "id": 2, "username": "Dennis", "roleName": "LehrerIn" }
```

#### PUT /api/users/me  (angemeldet)

| Feld | Typ | Regeln |
|---|---|---|
| `currentPassword` | string | Pflicht – ohne aktuelles Kennwort keine Änderung |
| `newUsername` | string? | optional, 3–20 Zeichen |
| `newPassword` | string? | optional, 8–100 Zeichen, bekommt ein **neues Salt** |

```json
{ "currentPassword": "TESTPASSWORT_123", "newPassword": "NeuesKennwort_456" }
```

`204 No Content`. Nach einer Kennwortänderung werden alle **anderen** Tokens des Benutzers widerrufen.
Fehler: `400` „Aktuelles Kennwort ist falsch.“ / „Keine Änderung angegeben.“, `409` Benutzername vergeben.

#### DELETE /api/users/me  (angemeldet)

`204 No Content`. `409`, wenn der Benutzer Buchungen in der Historie hat oder für einen Raum zuständig ist
(die Historie darf nicht verloren gehen).

#### GET /api/users  (Administrator)

`200 OK`

```json
[
  { "id": 1, "username": "ADMIN",  "roleName": "Administrator" },
  { "id": 2, "username": "Dennis", "roleName": "LehrerIn" }
]
```

#### PUT /api/users/{id}/role  (Administrator)

```json
{ "role": "LehrerIn" }
```

Erlaubt: `Administrator`, `LehrerIn`. `204 No Content`.
Fehler: `400` „Unbekannte Rolle. Erlaubt: Administrator, LehrerIn.“, `404` Benutzer nicht gefunden,
`409` „Der letzte Administrator kann nicht herabgestuft werden.“ / Benutzer ist noch für ein Labor zuständig.

#### DELETE /api/users/{id}  (Administrator)

`204 No Content`. `409` bei Historie, zugeordnetem Raum oder wenn es der letzte Administrator ist.

### 4.3 Kategorien – `/api/categories`

#### GET /api/categories  (angemeldet)

```json
[ { "id": 5, "name": "Beamer", "modelNum": null }, { "id": 2, "name": "Notebooks", "modelNum": null } ]
```

#### POST /api/categories  (Administrator)  ·  PUT /api/categories/{id}  (Administrator)

| Feld | Typ | Regeln |
|---|---|---|
| `name` | string | Pflicht, 1–100 Zeichen, eindeutig |
| `modelNum` | int? | optional |

POST → `201 { "id": 9 }`, PUT → `204`.
Fehler: `400`, `404` „Kategorie nicht gefunden.“, `409` „Eine Kategorie mit diesem Namen existiert bereits.“

#### DELETE /api/categories/{id}  (Administrator)

`204`, oder `409` „Kategorie wird noch von Artikeln verwendet.“

### 4.4 Hersteller – `/api/manufacturers`

#### GET /api/manufacturers  (angemeldet)

```json
[ { "id": 4, "name": "Cisco" }, { "id": 1, "name": "Dell" }, { "id": 6, "name": "Epson" } ]
```

#### POST /api/manufacturers  ·  PUT /api/manufacturers/{id}  (Administrator)

| Feld | Typ | Regeln |
|---|---|---|
| `name` | string | Pflicht, 1–50 Zeichen (wie die Spalte `Hersteller.Name`), eindeutig |

```json
{ "name": "Testhersteller" }
```

POST → `201 { "id": 10 }`, PUT → `204`.
Fehler: `400` (leerer Name), `403` für eine LehrerIn, `404` „Hersteller nicht gefunden.“,
`409` „Ein Hersteller mit diesem Namen existiert bereits.“

#### DELETE /api/manufacturers/{id}  (Administrator)

`204`, oder `409` „Hersteller wird noch von Artikeln verwendet.“ – so verliert kein Artikel seinen Hersteller.

### 4.5 Artikel – `/api/articles`

Ein Artikel **mit Seriennummer** ist genau **ein Gerät** (Menge immer 1).
Ein Artikel **ohne Seriennummer** wird **mengenmäßig** verwaltet (z. B. 40 Kabel).

#### GET /api/articles  (angemeldet)

Alle Query-Parameter sind optional und kombinierbar:

| Parameter | Bedeutung |
|---|---|
| `name` | Teil des Namens (LIKE) |
| `serienNum` | exakte Seriennummer |
| `kategorieId` | Kategorie |
| `herstellerId` | Hersteller |
| `raumId` | nur Artikel, die in diesem Standort Bestand haben |

Beispiel: `GET /api/articles?name=a`

```json
[
  {
    "id": 21, "name": "HDMI-Kabel 3 m", "serienNum": null,
    "kategorieId": 7, "kategorieName": "Kabel & Adapter",
    "herstellerId": null, "herstellerName": null,
    "bestand": 12
  }
]
```

`bestand` beachtet die Berechtigung: Der Administrator sieht den Gesamtbestand, eine LehrerIn nur den Bestand
in ihrem eigenen Labor. `raumId` eines fremden Labors ergibt `403`.

#### GET /api/articles/{id}  (angemeldet)

Gleiche Felder wie oben, ein Objekt. `404` „Artikel nicht gefunden.“

#### POST /api/articles  ·  PUT /api/articles/{id}  (Administrator)

| Feld | Typ | Regeln |
|---|---|---|
| `name` | string | Pflicht, 1–100 Zeichen |
| `serienNum` | int? | optional, ≥ 1, eindeutig. `null` = Mengenartikel |
| `kategorieId` | int | Pflicht, muss existieren |
| `herstellerId` | int? | optional, muss existieren |

```json
{ "name": "Test-Notebook", "serienNum": 627413, "kategorieId": 9 }
```

POST → `201 { "id": 25 }` (Bestand 0), PUT → `204`.
Fehler: `400` (Kategorie/Hersteller existiert nicht), `404`, `409` „Diese Seriennummer ist bereits vergeben.“ /
„Von diesem Artikel gibt es mehr als 1 Stück, eine Seriennummer ist nicht möglich.“

#### DELETE /api/articles/{id}  (Administrator)

`204`, oder `409` „Artikel hat noch Bestand …“ / „Artikel kommt in der Bewegungshistorie vor …“

#### POST /api/articles/{id}/stock  (Administrator) – Zugang

```json
{ "menge": 10 }
```

`200 { "movementId": 56 }`. Bucht die Stück ins **Hauptlager** und schreibt eine Bewegung „Zugang“.
Bei einem Seriennummern-Artikel nur `menge = 1` und nur, solange das Gerät noch nicht im Bestand ist
(`409` „Dieses Gerät (Seriennummer) ist bereits im Bestand.“).

### 4.6 Standorte – `/api/locations`

Standorte sind die Zeilen der Tabelle `Raum` mit dem Typ `Hauptlager`, `Umbuchungslager` oder `Labor`.
Hauptlager und Umbuchungslager gibt es jeweils genau einmal.

Antwortformat (GET `/api/locations` und `/api/locations/mine`), sortiert: Hauptlager, Umbuchungslager, Labore:

```json
[
  { "id": 1, "raumTyp": "Hauptlager",      "raumNum": null, "anzeigename": "Hauptlager",      "benutzerId": 1, "username": "ADMIN" },
  { "id": 2, "raumTyp": "Umbuchungslager", "raumNum": null, "anzeigename": "Umbuchungslager", "benutzerId": 1, "username": "ADMIN" },
  { "id": 4, "raumTyp": "Labor",           "raumNum": 309,  "anzeigename": "Raum 309",        "benutzerId": 2, "username": "Dennis" }
]
```

#### POST /api/locations  (Administrator)

| Feld | Typ | Regeln |
|---|---|---|
| `raumTyp` | string | `Hauptlager`, `Umbuchungslager` oder `Labor` |
| `benutzerId` | int | zuständiger Benutzer; bei einem Labor eine LehrerIn ohne anderes Labor |
| `raumNum` | int? | Raumnummer, eindeutig |

```json
{ "raumTyp": "Labor", "benutzerId": 6, "raumNum": 639 }
```

`201 { "id": 7 }`. Fehler: `400` (unbekannter Typ, Benutzer ist keine LehrerIn), `409` (Lagertyp gibt es schon,
Raumnummer vergeben, LehrerIn hat schon ein Labor).

#### PUT /api/locations/{id}  (Administrator)

```json
{ "raumNum": 883 }
```

`204`. `409` „Diese Raumnummer ist bereits vergeben.“

#### PUT /api/locations/{id}/teacher  (Administrator) – Zuordnung LehrerIn ↔ Labor

```json
{ "benutzerId": 6 }
```

`204`. `409` „Diese LehrerIn ist bereits einem anderen Labor zugeordnet.“, `400` wenn der Standort kein Labor ist.

#### DELETE /api/locations/{id}  (Administrator)

`204`. `409` für Hauptlager/Umbuchungslager, wenn noch Bestand vorhanden ist oder der Standort in der Historie vorkommt.

#### GET /api/locations/{id}/inventory  (Administrator oder zuständige LehrerIn)

```json
[
  { "artikelId": 24, "artikelName": "Test-Kabel HDMI 3m", "serienNum": null,
    "kategorieName": "Testkategorie", "herstellerName": null, "menge": 4 }
]
```

`403` „Kein Zugriff auf diesen Standort.“ für ein fremdes Labor, `404` wenn es den Standort nicht gibt.

### 4.7 Übergaben – `/api/transfers`

#### POST /api/transfers  (Administrator) – Ausgabe

| Feld | Typ | Regeln |
|---|---|---|
| `laborId` | int | Ziel-Labor |
| `artikelId` | int | Artikel |
| `menge` | int | 1–100000; bei Seriennummer genau 1 |

Quelle (Hauptlager) und Bewegungsart (Ausgabe) legt die Prozedur fest, nicht der Client.

```json
{ "laborId": 4, "artikelId": 24, "menge": 4 }
```

`201 { "id": 58 }`. Der Bestand wird **sofort im Hauptlager abgezogen** (reserviert), die Übergabe ist „offen“.
Fehler: `400` (Menge, Seriennummer > 1), `404` Artikel/Labor, `409` „Nicht genügend Bestand vorhanden.“

#### GET /api/transfers/pending  (Administrator: alle, LehrerIn: eigenes Labor)

```json
[
  { "id": 58, "datum": "2026-11-19T00:00:00", "artikelId": 24, "artikel": "Test-Kabel HDMI 3m",
    "serienNum": null, "menge": 4, "laborId": 4, "labor": "Raum 309",
    "zustaendigeLehrerIn": "Dennis", "angelegtVon": "ADMIN" }
]
```

#### POST /api/transfers/{id}/confirm  (zuständige LehrerIn) – digitale Übernahme

Kein Body. `204`. Der Bestand wird **im Labor gutgeschrieben**, die Ausgabe wird abgeschlossen und
eine Bewegung „Übernahme“ geschrieben.
Fehler: `403` „Nur die zuständige LehrerIn kann diese Übergabe bestätigen.“, `404`,
`409` „Diese Übergabe wurde bereits bestätigt.“

### 4.8 Rückgaben – `/api/returns`

#### POST /api/returns  (zuständige LehrerIn)

Gleicher Body wie bei der Ausgabe (`laborId`, `artikelId`, `menge`). Das Ziel ist **immer das Umbuchungslager** –
eine LehrerIn kann nie direkt ins Hauptlager buchen.

`201 { "id": 60 }`. Bestand: Labor −Menge, Umbuchungslager +Menge. Die Rückgabe ist „offen“.
Fehler: `403` „Nur die zuständige LehrerIn kann aus diesem Labor zurückgeben.“, `409` zu wenig Bestand im Labor.

#### GET /api/returns/pending  (Administrator)

```json
[
  { "id": 60, "datum": "2026-11-19T00:00:00", "artikelId": 24, "artikel": "Test-Kabel HDMI 3m",
    "serienNum": null, "menge": 3, "vonLaborId": 4, "vonLabor": "Raum 309", "gebuchtVon": "Dennis" }
]
```

#### POST /api/returns/{id}/accept  (Administrator)

Kein Body. `204`. Bestand: Umbuchungslager −Menge, Hauptlager +Menge. Die Rückgabe wird abgeschlossen,
eine Bewegung „Einbuchung“ wird geschrieben. `409` „Diese Rückgabe wurde bereits abgeschlossen.“

### 4.9 Bewegungshistorie – `/api/movements`

#### GET /api/movements?raumId=&artikelId=  (angemeldet)

Administrator: alle Bewegungen. LehrerIn: nur Bewegungen von oder zu ihrem Labor
(`raumId` eines fremden Labors → `403`). Neueste zuerst.

```json
[
  { "id": 59, "datum": "2026-11-19T00:00:00", "bewegungArt": "Übernahme",
    "artikelId": 24, "artikel": "Test-Kabel HDMI 3m", "serienNum": null, "menge": 4,
    "status": "abgeschlossen", "gebuchtVon": "Dennis",
    "vonRaumId": 1, "vonRaum": "Hauptlager", "zuRaumId": 4, "zuRaum": "Raum 309" }
]
```

Es gibt **keinen** Endpunkt zum Ändern oder Löschen von Bewegungen: Die Historie bleibt vollständig.

---

## 5. Wann wird Bestand reserviert, abgezogen und gutgeschrieben?

| Schritt | Endpunkt | Hauptlager | Labor | Umbuchungslager | Bewegung (Status) |
|---|---|---|---|---|---|
| Zugang | `POST /api/articles/{id}/stock` | **+M** | – | – | Zugang (abgeschlossen) |
| Ausgabe | `POST /api/transfers` | **−M** (reserviert) | – | – | Ausgabe (offen) |
| Übernahme | `POST /api/transfers/{id}/confirm` | – | **+M** | – | Ausgabe → abgeschlossen, neue Zeile Übernahme |
| Rückgabe | `POST /api/returns` | – | **−M** | **+M** | Rückgabe (offen) |
| Einbuchung | `POST /api/returns/{id}/accept` | **+M** | – | **−M** | Rückgabe → abgeschlossen, neue Zeile Einbuchung |

Während eine Ausgabe offen ist, ist die Menge weder im Hauptlager noch im Labor gezählt, sondern nur in der
offenen Ausgabe. So kann nichts doppelt gezählt oder doppelt ausgegeben werden.
Jeder Schritt läuft in **einer Transaktion**; der Bestand kann nie negativ werden
(`UPDATE … WHERE Menge >= @Menge` und `CHECK (Menge >= 0)`).
