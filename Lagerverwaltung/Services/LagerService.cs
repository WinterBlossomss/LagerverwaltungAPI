using Lagerverwaltung.Models;
using Microsoft.EntityFrameworkCore;

namespace Lagerverwaltung.Services
{
    public class LagerService
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
            /// <returns></returns>
            public Task<Raum> GetLagerAsync(string typ)
            {
                return _db.Raums.MitDetails().FirstAsync(r => r.RaumTypNavigation.Name == typ);
            }

            /// <summary>
            /// Returns all laboratories in the system
            /// </summary>
            /// <returns></returns>
            public IQueryable<Raum> Labore()
            {
                return _db.Raums.MitDetails().Where(r => r.RaumTypNavigation.Name == RaumTypen.Labor).OrderBy(r => r.RaumNum);
            }

            /// <summary>
            /// Returns all open handovers in the system
            /// </summary>
            /// <returns></returns>
            public IQueryable<Lagerbewegungen> OffeneAusgaben()
            {
                return _db.Lagerbewegungens.MitDetails().Where(b => b.BewegungArt == BewegungArten.Ausgabe && !b.IsDone);
            }

            /// <summary>
            /// Returns all open returns in the system
            /// </summary>
            /// <returns></returns>
            public IQueryable<Lagerbewegungen> OffeneRueckgaben()
            {
                return _db.Lagerbewegungens.MitDetails().Where(b => b.BewegungArt == BewegungArten.Rueckgabe && !b.IsDone);
            }

            /// <summary>
            /// Returns the number of open returns in the system
            /// </summary>
            /// <returns></returns>
            public Task<int> AnzahlOffeneRueckgabenAsync()
            {
                return OffeneRueckgaben().CountAsync();
            }

            /// <summary>
            /// Returns the number of open handovers for a specific teacher
            /// </summary>
            /// <param name="lehrerId">The ID of the teacher</param>
            /// <returns></returns>
            public Task<int> AnzahlOffeneUebergabenAsync(int lehrerId)
            {
                return OffeneAusgaben().CountAsync(b => b.ZuRaumNavigation.Benutzer == lehrerId);
            }

            /// <summary>
            /// Returns the stock of a specific article in a specific room
            /// </summary>
            /// <param name="raumId">The ID of the room</param>
            /// <param name="artikelId">The ID of the article</param>
            /// <returns></returns>
            public async Task<int> BestandAsync(int raumId, int artikelId)
            {
                return (await _db.RaumArtikels.FindAsync(raumId, artikelId))?.Menge ?? 0;
            }

            /// <summary>
            /// Returns an overview of the stock in the specified rooms
            /// </summary>
            /// <param name="raeume">The rooms for which to provide an overview</param>
            /// <returns></returns>
            public async Task<List<RaumUebersicht>> UebersichtAsync(List<Raum> raeume)
            {
                ar ids = raeume.Select(r => r.ID).ToList();
                var bestand = await _db.RaumArtikels.Where(r => ids.Contains(r.Raum)).ToListAsync();
                var ausgaben = await OffeneAusgaben().Select(b => b.ZuRaum).ToListAsync();
                var rueckgaben = await OffeneRueckgaben().Select(b => b.ZuRaum).ToListAsync();

                return raeume.Select(r => new StandortKachel
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
        }
    }