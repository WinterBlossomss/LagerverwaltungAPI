using Lagerverwaltung.Services;
using Lagerverwaltung.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lagerverwaltung.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly LagerService _lager;

        public DashboardController(LagerService lager)
        {
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
                var hauptBestand = haupt == null ? new() : await _lager.BestandAsync(haupt);
                var labore = await _lager.GetLaboreAsync();

                var vm = new DashboardAdminViewModel
                {
                    StueckHauptlager = hauptBestand.Sum(r => r.Menge),
                    PositionenHauptlager = hauptBestand.Count,
                    AnzahlLabore = labore.Count,
                    AnzahlLehrer = labore.Select(r => r.Benutzer).Distinct().Count(),
                    OffeneAusgaben = (await _lager.OffeneAusgabenAsync()).OrderBy(b => b.Datum).ToList(),
                    OffeneRueckgaben = (await _lager.OffeneRueckgabenAsync()).OrderBy(b => b.Datum).ToList(),
                    LetzteBewegungen = (await _lager.BewegungenAsync()).Take(8).ToList()
                };
                return View("Admin", vm);
            }

            // Lehrer-Dashboard anzeigen (the API only returns her own labs)
            var eigeneLabore = await _lager.GetLaboreAsync();
            var ids = eigeneLabore.Select(l => l.ID).ToList();

            var vmL = new DashboardLehrerViewModel
            {
                Name = User.Identity?.Name,
                Labore = await _lager.UebersichtAsync(eigeneLabore),
                OffeneUebergaben = (await _lager.OffeneAusgabenAsync()).Where(b => ids.Contains(b.ZuRaum)).OrderBy(b => b.Datum).ToList(),
                RueckgabenUnterwegs = (await _lager.OffeneRueckgabenAsync()).Count(b => ids.Contains(b.VonRaum))
            };
            return View("Lehrer", vmL);
        }
    }
}
