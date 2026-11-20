namespace Lagerverwaltung.Services.Api
{
    // Answers and requests of the REST API (Lagerverwaltung.Api).
    // The API sends camelCase JSON; ApiClient reads it case-insensitively, so the names here
    // match the result classes of the stored procedures (spGet...Result) one to one.

    public record LoginResponse(string Token, DateTime ExpiresAtUtc, int UserId, string Username, string Role);

    public record CreatedResponse(int Id);

    public record UserDto(int Id, string Username, string RoleName);

    public record CategoryDto(int Id, string Name, int? ModelNum);

    public record ManufacturerDto(int Id, string Name);

    public record ArticleDto(
        int Id, string Name, int? SerienNum,
        int KategorieId, string KategorieName,
        int? HerstellerId, string? HerstellerName,
        int Bestand);

    /// <summary>Hauptlager, Umbuchungslager or Labor (GET /api/locations, /api/locations/mine).</summary>
    public record RoomDto(int Id, string RaumTyp, int? RaumNum, string? Anzeigename, int BenutzerId, string Username);

    /// <summary>One line of GET /api/locations/{id}/inventory.</summary>
    public record InventoryDto(
        int ArtikelId, string ArtikelName, int? SerienNum,
        string KategorieName, string? HerstellerName, int Menge);

    /// <summary>One line of GET /api/movements. Status is "offen" or "abgeschlossen".</summary>
    public record MovementDto(
        int Id, DateTime Datum, string BewegungArt,
        int ArtikelId, string Artikel, int? SerienNum, int Menge,
        string Status, string GebuchtVon,
        int VonRaumId, string? VonRaum, int ZuRaumId, string? ZuRaum);

    /// <summary>GET /api/transfers/pending: Ausgabe Hauptlager -> Labor, not confirmed yet.</summary>
    public record PendingTransferDto(
        int Id, DateTime Datum, int ArtikelId, string Artikel, int? SerienNum, int Menge,
        int LaborId, string? Labor, string ZustaendigeLehrerIn, string AngelegtVon);

    /// <summary>GET /api/returns/pending: Rückgabe Labor -> Umbuchungslager, not accepted yet.</summary>
    public record PendingReturnDto(
        int Id, DateTime Datum, int ArtikelId, string Artikel, int? SerienNum, int Menge,
        int VonLaborId, string? VonLabor, string GebuchtVon);
}
