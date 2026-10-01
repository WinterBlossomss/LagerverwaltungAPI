using Lagerverwaltung.Models;
using Lagerverwaltung.ViewModels;
using Microsoft.EntityFrameworkCore;
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
    }

    /// <summary>
    /// Custom exception class for LagerService-related errors.
    /// </summary>
    public class LagerException : Exception
    {
        public LagerException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Provides extension methods for the LagerService, including methods to retrieve user IDs and include related data in queries.
    /// </summary>
    public static class LagerExtensions
    {
        /// <summary>
        /// Retrieves the user ID from the ClaimsPrincipal object, which represents the currently authenticated user.
        /// </summary>
        /// <param name="user">The ClaimsPrincipal object representing the authenticated user.</param>
        /// <returns>The user ID.</returns>
        public static int GetBenutzerId(this ClaimsPrincipal user) =>
            int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

        /// <summary>
        /// Includes related data for Lagerbewegungen queries, such as Artikel, Bewegung, Benutzer, and Raum details.
        /// </summary>
        /// <param name="q">The IQueryable<Lagerbewegungen> to include details for.</param>
        /// <returns>The IQueryable<Lagerbewegungen> with included details.</returns>
        public static IQueryable<Lagerbewegungen> MitDetails(this IQueryable<Lagerbewegungen> q) => q
            .Include(b => b.ArtikelNavigation)
            .Include(b => b.BewegungNavigation)
            .Include(b => b.BenutzerNavigation)
            .Include(b => b.VonRaumNavigation).ThenInclude(r => r.RaumTypNavigation)
            .Include(b => b.ZuRaumNavigation).ThenInclude(r => r.RaumTypNavigation)
            .Include(b => b.ZuRaumNavigation).ThenInclude(r => r.BenutzerNavigation);

        /// <summary>
        /// Includes related data for Raum queries, such as RaumTyp and Benutzer details.
        /// </summary>
        /// <param name="q">The IQueryable<Raum> to include details for.</param>
        /// <returns>The IQueryable<Raum> with included details.</returns>
        public static IQueryable<Raum> MitDetails(this IQueryable<Raum> q) => q
            .Include(r => r.RaumTypNavigation)
            .Include(r => r.BenutzerNavigation);
    }

    public class LagerService
    {
        private readonly LagerverwaltungssystemContext _db;

        public LagerService(LagerverwaltungssystemContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Returns the main storage room in the system
        /// </summary>
        /// <param name="typ">The type of the room</param>
        /// <returns>The main storage room.</returns>
        public Task<Raum> GetLagerAsync(string typ)
        {
            return _db.Raums.MitDetails().FirstAsync(r => r.RaumTypNavigation.Name == typ);
        }

        /// <summary>
        /// Returns all laboratories in the system
        /// </summary>
        /// <returns>The laboratories.</returns>
        public IQueryable<Raum> GetLabore()
        {
            return _db.Raums.MitDetails().Where(r => r.RaumTypNavigation.Name == RaumTypen.Labor).OrderBy(r => r.RaumNum);
        }

        /// <summary>
        /// Returns all roles in the system
        /// </summary>
        /// <returns></returns>
        public IQueryable<Rolle> GetRollen()
        {
            return _db.Rolles.OrderBy(r => r.Name);
        }

        /// <summary>
        /// Returns the single laboratory of a teacher, or null if none is assigned
        /// </summary>
        public Task<Raum> LaborVonAsync(int lehrerId)
        {
            return GetLabore().FirstOrDefaultAsync(r => r.Benutzer == lehrerId);
        }

        /// <summary>
        /// Throws if the teacher is already assigned to another laboratory (one teacher = one lab).
        /// Call this before assigning a teacher to a lab.
        /// </summary>
        public async Task PruefeEinLaborProLehrerAsync(int lehrerId, int raumId)
        {
            var anderes = await GetLabore().FirstOrDefaultAsync(r => r.Benutzer == lehrerId && r.ID != raumId);
            if (anderes != null)
                throw new LagerException($"Diese Lehrkraft ist bereits {anderes.Anzeigename} zugeordnet. Eine Lehrkraft kann nur ein Labor haben.");
        }

        /// <summary>
        /// Returns all open handovers in the system
        /// </summary>
        /// <returns>The open handovers.</returns>
        public IQueryable<Lagerbewegungen> OffeneAusgaben()
        {
            return _db.Lagerbewegungens.MitDetails()
                .Where(b => b.BewegungNavigation.Name == BewegungArten.Ausgabe && b.IsDone != true);
        }

        /// <summary>
        /// Returns all open returns in the system
        /// </summary>
        /// <returns>The open returns.</returns>
        public IQueryable<Lagerbewegungen> OffeneRueckgaben()
        {
            return _db.Lagerbewegungens.MitDetails()
                .Where(b => b.BewegungNavigation.Name == BewegungArten.Rueckgabe && b.IsDone != true);
        }

        /// <summary>
        /// Returns the number of open returns in the system
        /// </summary>
        /// <returns>The number of open returns.</returns>
        public Task<int> AnzahlOffeneRueckgabenAsync()
        {
            return OffeneRueckgaben().CountAsync();
        }

        /// <summary>
        /// Returns the number of open handovers for a specific teacher
        /// </summary>
        /// <param name="lehrerId">The ID of the teacher</param>
        /// <returns>The number of open handovers for the specified teacher.</returns>
        public Task<int> AnzahlOffeneUebergabenAsync(int lehrerId)
        {
            return OffeneAusgaben().CountAsync(b => b.ZuRaumNavigation.Benutzer == lehrerId);
        }

        /// <summary>
        /// Returns the stock of a specific article in a specific room
        /// </summary>
        /// <param name="raumId">The ID of the room</param>
        /// <param name="artikelId">The ID of the article</param>
        /// <returns>The stock of the specified article in the specified room.</returns>
        public async Task<int> BestandAsync(int raumId, int artikelId)
        {
            return (await _db.RaumArtikels.FindAsync(raumId, artikelId))?.Menge ?? 0;
        }

        /// <summary>
        /// Returns an overview of the stock in the specified rooms
        /// </summary>
        /// <param name="raeume">The rooms for which to provide an overview</param>
        /// <returns>The list of room overviews.</returns>
        public async Task<List<RaumUebersicht>> UebersichtAsync(List<Raum> raeume)
        {
            var ids = raeume.Select(r => r.ID).ToList();
            var bestand = await _db.RaumArtikels.Where(r => ids.Contains(r.Raum)).ToListAsync();
            var ausgaben = await OffeneAusgaben().Select(b => b.ZuRaum).ToListAsync();
            var rueckgaben = await OffeneRueckgaben().Select(b => b.ZuRaum).ToListAsync();

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

        /// <summary>
        /// Processes the addition of new items to the main storage, updating the stock and recording the movement in the system.
        /// </summary>
        /// <param name="artikelId">The ID of the article being added.</param>
        /// <param name="menge">The quantity of items being added.</param>
        /// <param name="adminId">The ID of the administrator performing the action.</param>
        /// <returns></returns>
        public async Task ZugangAsync(int artikelId, int menge, int adminId)
        {
            var haupt = await GetLagerAsync(RaumTypen.Hauptlager);
            await AendereBestandAsync(haupt.ID, artikelId, menge);
            await BucheAsync(BewegungArten.Zugang, artikelId, menge, haupt.ID, haupt.ID, adminId, true);
        }

        /// <summary>
        /// Processes the issuance of items from the main storage to a laboratory, updating the stock and recording the movement in the system.
        /// </summary>
        /// <param name="artikelId">The ID of the article being issued.</param>
        /// <param name="menge">The quantity of items being issued.</param>
        /// <param name="laborId">The ID of the laboratory to which items are being issued.</param>
        /// <param name="adminId">The ID of the administrator performing the action.</param>
        /// <returns></returns>
        /// <exception cref="LagerException">Thrown when the laboratory is not found.</exception>
        public async Task AusgebenAsync(int artikelId, int menge, int laborId, int adminId)
        {
            await PruefeMengeAsync(artikelId, menge);
            var haupt = await GetLagerAsync(RaumTypen.Hauptlager);
            var labor = await GetLabore().FirstOrDefaultAsync(r => r.ID == laborId)
                ?? throw new LagerException("Labor nicht gefunden.");

            // Bestand verlässt das Hauptlager sofort, kommt aber erst nach der Bestätigung im Labor an.
            await AendereBestandAsync(haupt.ID, artikelId, -menge);
            await BucheAsync(BewegungArten.Ausgabe, artikelId, menge, haupt.ID, labor.ID, adminId, false);
        }

        /// <summary>
        /// Processes the confirmation of received items in a laboratory, updating the stock and recording the movement in the system.
        /// </summary>
        /// <param name="bewegungId">The ID of the movement to be confirmed.</param>
        /// <param name="lehrerId">The ID of the teacher confirming the movement.</param>
        /// <returns></returns>
        /// <exception cref="LagerException">Thrown when the movement is not found or already confirmed.</exception>
        public async Task BestaetigenAsync(int bewegungId, int lehrerId)
        {
            var b = await OffeneAusgaben().FirstOrDefaultAsync(x => x.ID == bewegungId)
                ?? throw new LagerException("Übergabe nicht gefunden oder bereits bestätigt.");
            if (b.ZuRaumNavigation.Benutzer != lehrerId)
                throw new LagerException("Sie sind für dieses Labor nicht zuständig.");

            var menge = b.ArtikelMenge ?? 1;
            await AendereBestandAsync(b.ZuRaum, b.Artikel, menge);
            b.IsDone = true;
            await BucheAsync(BewegungArten.Uebernahme, b.Artikel, menge, b.VonRaum, b.ZuRaum, lehrerId, true);
        }

        /// <summary>
        /// Processes the return of items from a laboratory to the main storage, moving them to the transfer storage first.
        /// </summary>
        /// <param name="laborId">The ID of the laboratory from which items are returned.</param>
        /// <param name="artikelId">The ID of the article being returned.</param>
        /// <param name="menge">The quantity of items being returned.</param>
        /// <param name="lehrerId">The ID of the teacher responsible for the return.</param>
        /// <returns></returns>
        /// <exception cref="LagerException">Thrown when the laboratory or teacher is not found, or when the teacher is not responsible for the laboratory.</exception>
        public async Task ZurueckgebenAsync(int laborId, int artikelId, int menge, int lehrerId)
        {
            var labor = await GetLabore().FirstOrDefaultAsync(r => r.ID == laborId)
                ?? throw new LagerException("Labor nicht gefunden.");
            if (labor.Benutzer != lehrerId)
                throw new LagerException("Sie sind für dieses Labor nicht zuständig.");
            await PruefeMengeAsync(artikelId, menge);

            // LehrerInnen buchen nie direkt ins Hauptlager, sondern immer ins Umbuchungslager.
            var umb = await GetLagerAsync(RaumTypen.Umbuchungslager);
            await AendereBestandAsync(labor.ID, artikelId, -menge);
            await AendereBestandAsync(umb.ID, artikelId, menge);
            await BucheAsync(BewegungArten.Rueckgabe, artikelId, menge, labor.ID, umb.ID, lehrerId, false);
        }

        /// <summary>
        /// Processes the acceptance of returned items from the transfer storage to the main storage, marking the return as completed.
        /// </summary>
        /// <param name="bewegungId">The ID of the movement to be accepted.</param>
        /// <returns></returns>
        /// <exception cref="LagerException">Thrown when the movement is not found or has already been accepted.</exception>
        public async Task EntgegennehmenAsync(int bewegungId)
        {
            var b = await OffeneRueckgaben().FirstOrDefaultAsync(x => x.ID == bewegungId)
                ?? throw new LagerException("Rückgabe nicht gefunden oder bereits entgegengenommen.");
            b.IsDone = true;
        }

        /// <summary>
        /// Returns the number of items that can be accepted for a specific article,
        /// taking into account the current stock in the transfer storage and any open returns.
        /// </summary>
        /// <param name="artikelId">The ID of the article for which to check availability.</param>
        /// <returns>The number of items that can be accepted.</returns>
        public async Task<int> EinbuchbarAsync(int artikelId)
        {
            var umb = await GetLagerAsync(RaumTypen.Umbuchungslager);
            var bestand = await BestandAsync(umb.ID, artikelId);
            var offen = await OffeneRueckgaben().Where(b => b.Artikel == artikelId).SumAsync(b => b.ArtikelMenge ?? 1);
            return Math.Max(0, bestand - offen);
        }

        /// <summary>
        /// Processes the booking of items from the transfer storage to the main storage,
        /// ensuring that the quantity being booked is valid and updating the stock accordingly.
        /// </summary>
        /// <param name="artikelId">The ID of the article to be booked.</param>
        /// <param name="menge">The quantity to be booked.</param>
        /// <param name="adminId">The ID of the administrator performing the booking.</param>
        /// <returns></returns>
        /// <exception cref="LagerException">Thrown when the booking quantity is invalid.</exception>
        public async Task EinbuchenAsync(int artikelId, int menge, int adminId)
        {
            if (menge < 1 || menge > await EinbuchbarAsync(artikelId))
                throw new LagerException("Diese Menge kann nicht eingebucht werden. Offene Rückgaben zuerst entgegennehmen.");

            var umb = await GetLagerAsync(RaumTypen.Umbuchungslager);
            var haupt = await GetLagerAsync(RaumTypen.Hauptlager);
            await AendereBestandAsync(umb.ID, artikelId, -menge);
            await AendereBestandAsync(haupt.ID, artikelId, menge);
            await BucheAsync(BewegungArten.Einbuchung, artikelId, menge, umb.ID, haupt.ID, adminId, true);
        }

        /// <summary>
        /// Saves all changes made in the context to the database asynchronously.
        /// </summary>
        /// <returns></returns>
        public Task SpeichernAsync() => _db.SaveChangesAsync();

        /// <summary>
        /// Changes the stock of a specific article in a specific room by a given delta (positive or negative).
        /// </summary>
        /// <param name="raumId">The ID of the room.</param>
        /// <param name="artikelId">The ID of the article.</param>
        /// <param name="delta">The delta to apply to the stock.</param>
        /// <returns></returns>
        /// <exception cref="LagerException">Thrown when there is not enough stock available.</exception>
        private async Task AendereBestandAsync(int raumId, int artikelId, int delta)
        {
            var ra = await _db.RaumArtikels.FindAsync(raumId, artikelId);
            var neu = (ra?.Menge ?? 0) + delta;
            if (neu < 0) throw new LagerException("Nicht genügend Bestand vorhanden.");

            if (ra == null) _db.RaumArtikels.Add(new RaumArtikel { Raum = raumId, Art = artikelId, Menge = neu });
            else if (neu == 0) _db.RaumArtikels.Remove(ra);
            else ra.Menge = neu;
        }

        /// <summary>
        /// Books a movement of items in the system, recording the details of the movement in the Lagerbewegungen table.
        /// </summary>
        /// <param name="art">The type of movement.</param>
        /// <param name="artikelId">The ID of the article.</param>
        /// <param name="menge">The quantity of items moved.</param>
        /// <param name="von">The ID of the source room.</param>
        /// <param name="zu">The ID of the destination room.</param>
        /// <param name="benutzerId">The ID of the user confirming the movement.</param>
        /// <param name="isDone">Indicates whether the movement is done.</param>
        /// <returns></returns>
        private async Task BucheAsync(string art, int artikelId, int menge, int von, int zu, int benutzerId, bool isDone)
        {
            var artId = await _db.BewegungArts.Where(a => a.Name == art).Select(a => a.ID).FirstAsync();
            _db.Lagerbewegungens.Add(new Lagerbewegungen
            {
                Datum = DateOnly.FromDateTime(DateTime.Now),
                Bewegung = artId,
                Artikel = artikelId,
                ArtikelMenge = menge,
                VonRaum = von,
                ZuRaum = zu,
                Benutzer = benutzerId,
                IsDone = isDone
            });
        }

        /// <summary>
        /// Checks the validity of the quantity for a specific article, ensuring that it meets the requirements for movement
        /// (e.g., minimum quantity, serial number constraints).
        /// </summary>
        /// <param name="artikelId">The ID of the article.</param>
        /// <param name="menge">The quantity to check.</param>
        /// <returns></returns>
        /// <exception cref="LagerException">Thrown when the quantity is invalid.</exception>
        private async Task PruefeMengeAsync(int artikelId, int menge)
        {
            var a = await _db.Artikels.FindAsync(artikelId) ?? throw new LagerException("Artikel nicht gefunden.");
            if (menge < 1) throw new LagerException("Die Menge muss mindestens 1 sein.");
            if (a.SerienNum != null && menge != 1)
                throw new LagerException($"{a.Name} hat eine Seriennummer und kann nur einzeln bewegt werden.");
        }
    }
}