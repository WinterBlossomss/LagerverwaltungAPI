using Lagerverwaltung.Models;
using Lagerverwaltung.Services;
using Lagerverwaltung.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lagerverwaltung.Controllers
{
    [Authorize(Roles = Rollen.Admin)]
    public class LagerController : Controller
    {
        private readonly LagerverwaltungssystemContext _db;
        private readonly LagerService _lager;

        public LagerController(LagerverwaltungssystemContext db, LagerService lager)
        {
            _db = db;
            _lager = lager;
        }

        /// <summary>
        /// Displays the overview of all storage rooms and laboratories.
        /// </summary>
        /// <returns></returns>
        public async Task<IActionResult> Index()
        {
            var raeume = await _db.Raums.MitDetails().OrderBy(r => r.RaumTyp).ThenBy(r => r.RaumNum).ToListAsync();
            var kacheln = await _lager.UebersichtAsync(raeume);

            return View(new LagerViewModel
            {
                Lager = kacheln.Where(k => k.Typ != RaumTypen.Labor).ToList(),
                Labore = kacheln.Where(k => k.Typ == RaumTypen.Labor).ToList()
            });
        }
    }
}