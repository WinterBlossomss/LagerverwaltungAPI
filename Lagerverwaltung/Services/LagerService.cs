using Lagerverwaltung.Models;
using Lagerverwaltung.Services.Api;
using Lagerverwaltung.ViewModels;
using System.Security.Claims;

namespace Lagerverwaltung.Services
{
    /// <summary>
    /// Defines the roles used in the inventory management system.
    /// </summary>
    public static class Rollen
    {
        public const string Admin = "Administrator";
        public const string Lehrer = "LehrerIn";

        public static readonly string[] Alle = { Admin, Lehrer };
    }

    /// <summary>
    /// Defines the types of rooms in the inventory system.
    /// </summary>
    public static class RaumTypen
    {
        public const string Hauptlager = "Hauptlager";
        public const string Umbuchungslager = "Umbuchungslager";
        public const string Labor = "Labor";
    }

    /// <summary>
    /// Defines the types of movements that can occur in the inventory system.
    /// </summary>
    public static class BewegungArten
    {
        public const string Zugang = "Zugang"; // Neuer Artikel ins Hauptlager
        public const string Ausgabe = "Ausgabe"; // Hauptlager -> Labor, IsDone = Übernahme bestätigt
        public const string Uebernahme = "Übernahme"; // Digitale Bestätigung durch die LehrerIn
        public const string Rueckgabe = "Rückgabe"; // Labor -> Umbuchungslager, IsDone = vom Admin entgegengenommen
        public const string Einbuchung = "Einbuchung"; // Umbuchungslager -> Hauptlager
        public const string Stornieren = "Stornieren"; // Storniert, z.B. bei Fehlern

        public static readonly string[] Alle = { Zugang, Ausgabe, Uebernahme, Rueckgabe, Einbuchung, Stornieren };
    }

    /// <summary>
    /// Provides extension methods to read the logged-in user from the authentication cookie.
    /// </summary>
    public static class LagerExtensions
    {
        /// <summary>
        /// Retrieves the user ID from the ClaimsPrincipal object, which represents the currently authenticated user.
        /// </summary>
        public static int GetBenutzerId(this ClaimsPrincipal user) =>
            int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }

    /// <summary>
    /// Reads warehouse data from the REST API and turns it into the model classes the views already use
    /// (Raum, RaumArtikel, Lagerbewegungen, ...). It never talks to the database.
    ///
    /// Every booking (Ausgabe, Übernahme, Rückgabe, Einbuchung, Zugang) is done by the API's stored procedures,
    /// so the rules and the stock checks live in one place. The controllers call ApiClient for those.
    ///
    /// Registered as scoped: rooms are loaded at most once per request.
    /// </summary>
    public class LagerService
    {
        private readonly ApiClient _api;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private List<Raum>? _raeume;

        public LagerService(ApiClient api, IHttpContextAccessor httpContextAccessor)
        {
            _api = api;
            _httpContextAccessor = httpContextAccessor;
        }

        private ClaimsPrincipal User => _httpContextAccessor.HttpContext!.User;
        private bool IstAdmin => User.IsInRole(Rollen.Admin);

        // ------------------------------------------------------------ Rooms

        /// <summary>
        /// The rooms the user may see: all rooms for an administrator, only the own rooms for a LehrerIn.
        /// Sorted Hauptlager, Umbuchungslager, Labore (by room number).
        /// </summary>
        public async Task<List<Raum>> GetRaeumeAsync()
        {
            if (_raeume == null)
            {
                var dtos = IstAdmin ? await _api.GetRoomsAsync() : await _api.GetOwnRoomsAsync();
                _raeume = dtos.Select(ToRaum)
                    .OrderBy(r => RaumSortierung(r.RaumTypNavigation.Name)).ThenBy(r => r.RaumNum)
                    .ToList();
            }
            return _raeume;
        }

        /// <summary>The laboratories the user may see (administrator: all, LehrerIn: her own).</summary>
        public async Task<List<Raum>> GetLaboreAsync() =>
            (await GetRaeumeAsync()).Where(r => r.RaumTypNavigation.Name == RaumTypen.Labor).ToList();

        /// <summary>Hauptlager or Umbuchungslager (administrator only).</summary>
        public async Task<Raum?> GetLagerAsync(string typ) =>
            (await GetRaeumeAsync()).FirstOrDefault(r => r.RaumTypNavigation.Name == typ);

        private async Task<Dictionary<int, Raum>> RaumLookupAsync() =>
            (await GetRaeumeAsync()).ToDictionary(r => r.ID);

        // ------------------------------------------------------------ Stock

        /// <summary>Stock of one room (GET /api/locations/{id}/inventory).</summary>
        public async Task<List<RaumArtikel>> BestandAsync(Raum raum) =>
            (await _api.GetInventoryAsync(raum.ID)).Select(i => ToRaumArtikel(i, raum)).ToList();

        /// <summary>Stock of several rooms. The API answers one room per request.</summary>
        public async Task<List<RaumArtikel>> BestandAsync(IEnumerable<Raum> raeume)
        {
            var result = new List<RaumArtikel>();
            foreach (var raum in raeume)
                result.AddRange(await BestandAsync(raum));
            return result;
        }

        // ------------------------------------------------------------ Movements

        /// <summary>Open Ausgaben (not confirmed yet). Administrator: all, LehrerIn: for her labs.</summary>
        public async Task<List<Lagerbewegungen>> OffeneAusgabenAsync()
        {
            var lookup = await RaumLookupAsync();
            return (await _api.GetPendingTransfersAsync()).Select(t => ToBewegung(t, lookup)).ToList();
        }

        /// <summary>
        /// Open Rückgaben (not accepted by the administrator yet).
        /// Administrator: GET /api/returns/pending. LehrerIn: the open returns in her own history.
        /// </summary>
        public async Task<List<Lagerbewegungen>> OffeneRueckgabenAsync()
        {
            if (IstAdmin)
            {
                var lookup = await RaumLookupAsync();
                return (await _api.GetPendingReturnsAsync()).Select(r => ToBewegung(r, lookup)).ToList();
            }

            return (await BewegungenAsync())
                .Where(b => b.BewegungNavigation.Name == BewegungArten.Rueckgabe && b.IsDone != true)
                .ToList();
        }

        /// <summary>
        /// Movement history, newest first. Administrator: everything; LehrerIn: only her rooms (the API filters).
        /// </summary>
        public async Task<List<Lagerbewegungen>> BewegungenAsync(int? raumId = null, int? artikelId = null)
        {
            var lookup = await RaumLookupAsync();
            return (await _api.GetMovementsAsync(raumId, artikelId))
                .Select(m => ToBewegung(m, lookup))
                .OrderByDescending(b => b.Datum).ThenByDescending(b => b.ID)
                .ToList();
        }

        // ------------------------------------------------------------ Navigation badges (_Layout)

        /// <summary>Number of open returns for the menu badge. 0 if the API can't be asked right now.</summary>
        public async Task<int> AnzahlOffeneRueckgabenAsync()
        {
            try { return (await _api.GetPendingReturnsAsync()).Count; }
            catch (Exception ex) when (ex is ApiException or HttpRequestException) { return 0; }
        }

        /// <summary>Number of open handovers of the logged-in LehrerIn for the menu badge.</summary>
        public async Task<int> AnzahlOffeneUebergabenAsync()
        {
            try { return (await _api.GetPendingTransfersAsync()).Count; }
            catch (Exception ex) when (ex is ApiException or HttpRequestException) { return 0; }
        }

        // ------------------------------------------------------------ Overview tiles

        /// <summary>
        /// Returns an overview of the stock in the specified rooms
        /// </summary>
        public async Task<List<RaumUebersicht>> UebersichtAsync(List<Raum> raeume)
        {
            var bestand = await BestandAsync(raeume);
            var ausgaben = (await OffeneAusgabenAsync()).Select(b => b.ZuRaum).ToList();
            // Open returns are counted at their target (the Umbuchungslager); only the administrator sees that room.
            var rueckgaben = IstAdmin ? (await OffeneRueckgabenAsync()).Select(b => b.ZuRaum).ToList() : new List<int>();

            return raeume.Select(r => new RaumUebersicht
            {
                RaumId = r.ID,
                Name = r.Anzeigename,
                Typ = r.RaumTypNavigation?.Name,
                Verantwortlich = r.BenutzerNavigation?.Username,
                Positionen = bestand.Count(b => b.Raum == r.ID),
                Stueck = bestand.Where(b => b.Raum == r.ID).Sum(b => b.Menge),
                OffeneUebergaben = ausgaben.Count(x => x == r.ID),
                OffeneRueckgaben = rueckgaben.Count(x => x == r.ID)
            }).ToList();
        }

        // ------------------------------------------------------------ Mapping API answers -> view models

        private static int RaumSortierung(string? typ) => typ switch
        {
            RaumTypen.Hauptlager => 1,
            RaumTypen.Umbuchungslager => 2,
            _ => 3
        };

        public static Raum ToRaum(RoomDto r) => new()
        {
            ID = r.Id,
            RaumNum = r.RaumNum,
            Benutzer = r.BenutzerId,
            RaumTypNavigation = new RaumTyp { Name = r.RaumTyp },
            BenutzerNavigation = new Benutzer { ID = r.BenutzerId, Username = r.Username }
        };

        /// <summary>
        /// A room the user can't load (e.g. the Hauptlager for a LehrerIn) is rebuilt from the
        /// display name in the API answer ("Hauptlager", "Umbuchungslager", "Raum 312").
        /// </summary>
        private static Raum RaumAus(int id, string? anzeigename, IReadOnlyDictionary<int, Raum> lookup, string? verantwortlich = null)
        {
            if (lookup.TryGetValue(id, out var raum))
                return raum;

            var name = anzeigename ?? $"Raum {id}";
            bool istLabor = name.StartsWith("Raum ") || name.StartsWith("Labor ");
            int? nummer = name.StartsWith("Raum ") && int.TryParse(name["Raum ".Length..], out var n) ? n : null;
            return new Raum
            {
                ID = id,
                RaumNum = nummer,
                RaumTypNavigation = new RaumTyp { Name = istLabor ? RaumTypen.Labor : name },
                BenutzerNavigation = verantwortlich == null ? null : new Benutzer { Username = verantwortlich }
            };
        }

        private static Raum? LagerAus(IReadOnlyDictionary<int, Raum> lookup, string typ) =>
            lookup.Values.FirstOrDefault(r => r.RaumTypNavigation.Name == typ);

        public static Artikel ToArtikel(ArticleDto a) => new()
        {
            ID = a.Id,
            Name = a.Name,
            SerienNum = a.SerienNum,
            Kategorie = a.KategorieId,
            Hersteller = a.HerstellerId,
            KategorieNavigation = new Kategorie { ID = a.KategorieId, Name = a.KategorieName },
            HerstellerNavigation = a.HerstellerId == null ? null : new Hersteller { ID = a.HerstellerId.Value, Name = a.HerstellerName! }
        };

        public static RaumArtikel ToRaumArtikel(InventoryDto i, Raum raum) => new()
        {
            Raum = raum.ID,
            Art = i.ArtikelId,
            Menge = i.Menge,
            RaumNavigation = raum,
            ArtNavigation = new Artikel
            {
                ID = i.ArtikelId,
                Name = i.ArtikelName,
                SerienNum = i.SerienNum,
                KategorieNavigation = new Kategorie { Name = i.KategorieName },
                HerstellerNavigation = i.HerstellerName == null ? null : new Hersteller { Name = i.HerstellerName }
            }
        };

        private static Lagerbewegungen ToBewegung(MovementDto m, IReadOnlyDictionary<int, Raum> lookup) => new()
        {
            ID = m.Id,
            Datum = DateOnly.FromDateTime(m.Datum),
            Artikel = m.ArtikelId,
            ArtikelMenge = m.Menge,
            IsDone = m.Status == "abgeschlossen",
            VonRaum = m.VonRaumId,
            ZuRaum = m.ZuRaumId,
            BewegungNavigation = new BewegungArt { Name = m.BewegungArt },
            ArtikelNavigation = new Artikel { ID = m.ArtikelId, Name = m.Artikel, SerienNum = m.SerienNum },
            BenutzerNavigation = new Benutzer { Username = m.GebuchtVon },
            VonRaumNavigation = RaumAus(m.VonRaumId, m.VonRaum, lookup),
            ZuRaumNavigation = RaumAus(m.ZuRaumId, m.ZuRaum, lookup)
        };

        private static Lagerbewegungen ToBewegung(PendingTransferDto t, IReadOnlyDictionary<int, Raum> lookup)
        {
            var haupt = LagerAus(lookup, RaumTypen.Hauptlager)
                ?? new Raum { RaumTypNavigation = new RaumTyp { Name = RaumTypen.Hauptlager } };
            return new Lagerbewegungen
            {
                ID = t.Id,
                Datum = DateOnly.FromDateTime(t.Datum),
                Artikel = t.ArtikelId,
                ArtikelMenge = t.Menge,
                IsDone = false,
                VonRaum = haupt.ID,
                ZuRaum = t.LaborId,
                BewegungNavigation = new BewegungArt { Name = BewegungArten.Ausgabe },
                ArtikelNavigation = new Artikel { ID = t.ArtikelId, Name = t.Artikel, SerienNum = t.SerienNum },
                BenutzerNavigation = new Benutzer { Username = t.AngelegtVon },
                VonRaumNavigation = haupt,
                ZuRaumNavigation = RaumAus(t.LaborId, t.Labor, lookup, t.ZustaendigeLehrerIn)
            };
        }

        private static Lagerbewegungen ToBewegung(PendingReturnDto r, IReadOnlyDictionary<int, Raum> lookup)
        {
            var umb = LagerAus(lookup, RaumTypen.Umbuchungslager)
                ?? new Raum { RaumTypNavigation = new RaumTyp { Name = RaumTypen.Umbuchungslager } };
            return new Lagerbewegungen
            {
                ID = r.Id,
                Datum = DateOnly.FromDateTime(r.Datum),
                Artikel = r.ArtikelId,
                ArtikelMenge = r.Menge,
                IsDone = false,
                VonRaum = r.VonLaborId,
                ZuRaum = umb.ID,
                BewegungNavigation = new BewegungArt { Name = BewegungArten.Rueckgabe },
                ArtikelNavigation = new Artikel { ID = r.ArtikelId, Name = r.Artikel, SerienNum = r.SerienNum },
                BenutzerNavigation = new Benutzer { Username = r.GebuchtVon },
                VonRaumNavigation = RaumAus(r.VonLaborId, r.VonLabor, lookup),
                ZuRaumNavigation = umb
            };
        }
    }
}
