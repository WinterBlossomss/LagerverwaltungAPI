using Lagerverwaltung.Models;
using Lagerverwaltung.Services;
using Lagerverwaltung.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Lagerverwaltung.Controllers
{
    [Authorize(Roles = Rollen.Admin)]
    public class InventarController : Controller
    {
        private readonly LagerverwaltungssystemContext _db;
        private readonly LagerService _lager;

        public InventarController(LagerverwaltungssystemContext db, LagerService lager)
        {
            _db = db;
            _lager = lager;
        }

        /// <summary>
        /// Zeigt das Inventar an, mit Filtermöglichkeiten nach Kategorie, Raum, Lehrer und Seriennummer.
        /// </summary>
        /// <param name="vm">The view model containing the filter criteria.</param>
        /// <returns>The action result.</returns>
        public async Task<IActionResult> Index(InventarViewModel vm)
        {
            // Trim the search string to avoid unnecessary whitespace issues
            var suche = vm.Suche?.Trim();

            // Local function to determine if an article matches the search criteria
            bool Passt(Artikel a) =>
                (string.IsNullOrEmpty(suche)
                    || a.Name.Contains(suche, StringComparison.OrdinalIgnoreCase)
                    || (a.SerienNum?.ToString().Contains(suche) ?? false))
                && (vm.KategorieId == null || a.Kategorie == vm.KategorieId)
                && (vm.Seriennummer != "mit" || a.SerienNum != null)
                && (vm.Seriennummer != "ohne" || a.SerienNum == null);

            // Bestand in allen Räumen
            var bestand = await _db.RaumArtikels
                .Include(ra => ra.ArtNavigation).ThenInclude(a => a.KategorieNavigation)
                .Include(ra => ra.ArtNavigation).ThenInclude(a => a.HerstellerNavigation)
                .Include(ra => ra.RaumNavigation).ThenInclude(r => r.RaumTypNavigation)
                .Include(ra => ra.RaumNavigation).ThenInclude(r => r.BenutzerNavigation)
                .ToListAsync();

            // Filter the inventory based on the search criteria and map to InventarZeile
            var zeilen = bestand
                .Where(ra => Passt(ra.ArtNavigation))
                .Where(ra => vm.RaumId == null || ra.Raum == vm.RaumId)
                .Where(ra => vm.LehrerId == null || (ra.RaumNavigation.RaumTypNavigation.Name == RaumTypen.Labor && ra.RaumNavigation.Benutzer == vm.LehrerId))
                .Select(ra => new InventarZeile
                {
                    ArtikelId = ra.Art,
                    Name = ra.ArtNavigation.Name,
                    Kategorie = ra.ArtNavigation.KategorieNavigation?.Name,
                    Hersteller = ra.ArtNavigation.HerstellerNavigation?.Name,
                    SerienNum = ra.ArtNavigation.SerienNum,
                    Standort = ra.RaumNavigation.Anzeigename,
                    RaumId = ra.Raum,
                    Verantwortlich = ra.RaumNavigation.RaumTypNavigation.Name == RaumTypen.Labor ? ra.RaumNavigation.BenutzerNavigation?.Username : null,
                    Menge = ra.Menge,
                    Status = ra.RaumNavigation.RaumTypNavigation.Name switch
                    {
                        RaumTypen.Hauptlager => "Verfügbar",
                        RaumTypen.Umbuchungslager => "Umbuchungslager",
                        _ => "Im Labor"
                    }
                }).ToList();

            // Gegenstände, die gerade unterwegs sind (Übergabe noch nicht bestätigt)
            var unterwegs = await _lager.OffeneAusgaben()
                .Include(b => b.ArtikelNavigation).ThenInclude(a => a.KategorieNavigation)
                .Include(b => b.ArtikelNavigation).ThenInclude(a => a.HerstellerNavigation)
                .ToListAsync();
            zeilen.AddRange(unterwegs
                .Where(b => Passt(b.ArtikelNavigation))
                .Where(b => vm.RaumId == null || b.ZuRaum == vm.RaumId)
                .Where(b => vm.LehrerId == null || b.ZuRaumNavigation.Benutzer == vm.LehrerId)
                .Select(b => new InventarZeile
                {
                    ArtikelId = b.Artikel,
                    Name = b.ArtikelNavigation.Name,
                    Kategorie = b.ArtikelNavigation.KategorieNavigation?.Name,
                    Hersteller = b.ArtikelNavigation.HerstellerNavigation?.Name,
                    SerienNum = b.ArtikelNavigation.SerienNum,
                    Standort = "→ " + b.ZuRaumNavigation.Anzeigename,
                    RaumId = b.ZuRaum,
                    Verantwortlich = b.ZuRaumNavigation.BenutzerNavigation?.Username,
                    Menge = b.ArtikelMenge ?? 1,
                    Status = "Übergabe offen"
                }));

            // Sort the inventory lines by Name, then Standort, then SerienNum
            vm.Zeilen = zeilen.OrderBy(z => z.Name).ThenBy(z => z.Standort).ThenBy(z => z.SerienNum).ToList();
            vm.GesamtPositionen = bestand.Count + unterwegs.Count;

            // Prepare the filter dropdowns for categories, rooms, and teachers
            vm.Kategorien = await _db.Kategories.OrderBy(k => k.Name)
                .Select(k => new SelectListItem(k.Name, k.ID.ToString())).ToListAsync();
            vm.Raeume = (await _db.Raums.MitDetails().OrderBy(r => r.RaumTyp).ThenBy(r => r.RaumNum).ToListAsync())
                .Select(r => new SelectListItem(r.Anzeigename, r.ID.ToString()));
            vm.Lehrer = await _db.Benutzers.Where(b => b.RolleNavigation.Name == Rollen.Lehrer).OrderBy(b => b.Username)
                .Select(b => new SelectListItem(b.Username, b.ID.ToString())).ToListAsync();

            return View(vm);
        }
    }
}