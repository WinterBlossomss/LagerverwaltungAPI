using Lagerverwaltung.Models;
using Lagerverwaltung.Services;
using Lagerverwaltung.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lagerverwaltung.Controllers
{
    [Authorize(Roles = Rollen.Admin)]
    public class UmbuchungController : Controller
    {
        private readonly LagerverwaltungssystemContext _db;
        private readonly LagerService _lager;

        public UmbuchungController(LagerverwaltungssystemContext db, LagerService lager)
        {
            _db = db;
            _lager = lager;
        }

        public async Task<IActionResult> Index()
        {
            var umb = await _lager.GetLagerAsync(RaumTypen.Umbuchungslager);
            var offen = await _lager.OffeneRueckgaben().OrderBy(b => b.Datum).ToListAsync();

            var bestand = await _db.RaumArtikels
                .Include(ra => ra.ArtNavigation)
                .Where(ra => ra.Raum == umb.ID)
                .ToListAsync();

            return View(new UmbuchungViewModel
            {
                OffeneRueckgaben = offen,
                Bestand = bestand
                    .Select(ra => new UmbuchungBestand
                    {
                        ArtikelId = ra.Art,
                        Name = ra.ArtNavigation.Name,
                        SerienNum = ra.ArtNavigation.SerienNum,
                        Menge = ra.Menge,
                        Einbuchbar = Math.Max(0, ra.Menge - offen.Where(o => o.Artikel == ra.Art).Sum(o => o.ArtikelMenge ?? 1))
                    })
                    .OrderByDescending(b => b.Einbuchbar).ThenBy(b => b.Name)
                    .ToList(),
                Eingebucht = await _db.Lagerbewegungens.MitDetails()
                    .Where(b => b.BewegungNavigation.Name == BewegungArten.Einbuchung)
                    .OrderByDescending(b => b.Datum).ThenByDescending(b => b.ID)
                    .Take(10)
                    .ToListAsync()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Entgegennehmen(int id)
        {
            try
            {
                await _lager.EntgegennehmenAsync(id);
                await _lager.SpeichernAsync();
                TempData["Meldung"] = "Rückgabe entgegengenommen. Sie kann jetzt ins Hauptlager gebucht werden.";
            }
            catch (LagerException ex)
            {
                TempData["Fehler"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Einbuchen(int artikelId, int menge)
        {
            try
            {
                await _lager.EinbuchenAsync(artikelId, menge, User.GetBenutzerId());
                await _lager.SpeichernAsync();
                TempData["Meldung"] = $"{menge} Stück ins Hauptlager gebucht.";
            }
            catch (LagerException ex)
            {
                TempData["Fehler"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }
    }
}