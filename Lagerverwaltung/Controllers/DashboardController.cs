using Lagerverwaltung.Models;
using Lagerverwaltung.Services;
using Lagerverwaltung.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lagerverwaltung.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly LagerverwaltungssystemContext _db;
        private readonly LagerService _lager;

        public DashboardController(LagerverwaltungssystemContext db, LagerService lager)
        {
            _db = db;
            _lager = lager;
        }

        /// <summary>
        /// Dashboard für Admins und Lehrer anzeigen
        /// </summary>
        /// <returns></returns>
        public async Task<IActionResult> Index()
        {
            // Admin-Dashboard anzeigen
            if (User.IsInRole(Rollen.Admin))
            {
                var haupt = await _lager.GetLagerAsync(RaumTypen.Hauptlager);
                var hauptBestand = _db.RaumArtikels.Where(r => r.Raum == haupt.ID);

                var vm = new DashboardAdminViewModel
                {
                    StueckHauptlager = await hauptBestand.SumAsync(r => r.Menge),
                    PositionenHauptlager = await hauptBestand.CountAsync(),
                    AnzahlLabore = await _lager.GetLabore().CountAsync(),
                    AnzahlLehrer = await _lager.GetLabore().Select(r => r.Benutzer).Distinct().CountAsync(),
                    OffeneAusgaben = await _lager.OffeneAusgaben().OrderBy(b => b.Datum).ToListAsync(),
                    OffeneRueckgaben = await _lager.OffeneRueckgaben().OrderBy(b => b.Datum).ToListAsync(),
                    LetzteBewegungen = await _db.Lagerbewegungens.MitDetails()
                        .OrderByDescending(b => b.Datum).ThenByDescending(b => b.ID).Take(8).ToListAsync()
                };
                return View("Admin", vm);
            }

            // Lehrer-Dashboard anzeigen
            var uid = User.GetBenutzerId();
            var labore = await _lager.GetLabore().Where(r => r.Benutzer == uid).ToListAsync();
            var ids = labore.Select(l => l.ID).ToList();

            var vmL = new DashboardLehrerViewModel
            {
                Name = User.Identity?.Name,
                Labore = await _lager.UebersichtAsync(labore),
                OffeneUebergaben = await _lager.OffeneAusgaben().Where(b => ids.Contains(b.ZuRaum)).OrderBy(b => b.Datum).ToListAsync(),
                RueckgabenUnterwegs = await _lager.OffeneRueckgaben().CountAsync(b => ids.Contains(b.VonRaum))
            };
            return View("Lehrer", vmL);
        }
    }
}