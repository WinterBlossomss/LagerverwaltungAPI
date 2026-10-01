using Lagerverwaltung.Models;
using Lagerverwaltung.Services;
using Lagerverwaltung.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Lagerverwaltung.Controllers
{
    public class BenutzerController : Controller
    {
        private readonly LagerverwaltungssystemContext _context;
        private readonly LagerService _lager;

        public BenutzerController(LagerverwaltungssystemContext context, LagerService lager)
        {
            _context = context;
            _lager = lager;
        }

        public async Task<IActionResult> Index(BenutzerListeViewModel vm)
        {
            var q = _context.Benutzers.Include(b => b.Raums).Include(b => b.RolleNavigation).AsQueryable();

            if (!string.IsNullOrWhiteSpace(vm.Suche))
            {
                var s = vm.Suche.Trim();
                q = q.Where(a => a.Username.Contains(s) || a.RolleNavigation.Name.Contains(s) || a.Raums.Any(r => r.RaumNum.ToString().Contains(s)));
            }
            if (int.TryParse(vm.Rolle, out var rolleId))
            {
                q = q.Where(a => a.Rolle == rolleId);
            }
            if (int.TryParse(vm.Labor, out var laborId))
            {
                q = q.Where(a => a.Raums.Any(r => r.ID == laborId));
            }

            vm.Benutzer = await q.OrderBy(a => a.Username).ToListAsync();
            vm.Rollen = await RollenAsync();
            vm.Labore = await RaumsAsync();
            return View(vm);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var benutzer = await _context.Benutzers.Include(b => b.Raums).Include(b => b.RolleNavigation).FirstOrDefaultAsync(b => b.ID == id);
            if (benutzer == null)
            {
                return NotFound();
            }
            var vm = new BenutzerDetailsViewModel
            {
                Benutzer = benutzer,
                Labor = string.Join(", ", benutzer.Raums
                    .Where(r => r.RaumNum != null)
                    .OrderBy(r => r.RaumNum)
                    .Select(r => r.RaumNum)),
                Bewegungen = await _context.Lagerbewegungens.MitDetails().Where(b => b.Benutzer == id).OrderByDescending(b => b.Datum).ThenByDescending(b => b.ID).Take(20).ToListAsync(),
                KannGeloeschtWerden = !await _context.Lagerbewegungens.AnyAsync(b => b.Benutzer == id) && !await _context.Raums.AnyAsync(r => r.Benutzer == id)
            };
            return View(vm);
        }

        public async Task<IActionResult> Create()
        {
            return View(new BenutzerFormViewModel
            {
                Rollen = await RollenAsync(),
                Labore = await RaumsAsync()
            });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(BenutzerFormViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                vm.Rollen = await RollenAsync();
                vm.Labore = await RaumsAsync();
                return View(vm);
            }
            var benutzer = new Benutzer
            {
                Username = vm.Username,
                Rolle = int.Parse(vm.Rolle),
                Raums = new List<Raum>()
            };
            if (vm.LaborId.HasValue)
            {
                var labor = await _context.Raums.FindAsync(vm.LaborId.Value);
                if (labor != null)
                {
                    benutzer.Raums.Add(labor);
                }
            }
            _context.Benutzers.Add(benutzer);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Get the user with the specified ID from the database, including their associated rooms,
        /// and return a view model for editing the user's information.
        /// </summary>
        /// <param name="id">The ID of the user to edit.</param>
        /// <returns>The action result.</returns>
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var benutzer = await _context.Benutzers.Include(b => b.Raums).FirstOrDefaultAsync(b => b.ID == id);
            if (benutzer == null)
            {
                return NotFound();
            }
            var vm = new BenutzerFormViewModel
            {
                ID = benutzer.ID,
                Username = benutzer.Username,
                Rolle = benutzer.Rolle.ToString(),
                LaborId = benutzer.Raums.FirstOrDefault()?.ID,
                Rollen = await RollenAsync(),
                Labore = await RaumsAsync()
            };
            return View(vm);
        }

        public async Task<IActionResult> Edit(int id, BenutzerFormViewModel vm)
        {
            if (id != vm.ID)
            {
                return NotFound();
            }
            if (!ModelState.IsValid)
            {
                vm.Rollen = await RollenAsync();
                vm.Labore = await RaumsAsync();
                return View(vm);
            }
            var benutzer = await _context.Benutzers.Include(b => b.Raums).FirstOrDefaultAsync(b => b.ID == id);
            if (benutzer == null)
            {
                return NotFound();
            }
            benutzer.Username = vm.Username;
            benutzer.Rolle = int.Parse(vm.Rolle);
            benutzer.Raums.Clear();
            if (vm.LaborId.HasValue)
            {
                var labor = await _context.Raums.FindAsync(vm.LaborId.Value);
                if (labor != null)
                {
                    benutzer.Raums.Add(labor);
                }
            }
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Get the user with the specified ID from the database and return a view for confirming deletion of the user.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }
            return await Details(id);
        }

        /// <summary>
        /// Delete the user with the specified ID from the database if they have no associated warehouse movements or rooms, and redirect to the index page.
        /// If the user cannot be deleted, return a bad request response with an error message.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var benutzer = await _context.Benutzers.Include(b => b.Raums).FirstOrDefaultAsync(b => b.ID == id);
            if (benutzer == null)
            {
                return NotFound();
            }
            var hatLagerbewegungen = await _context.Lagerbewegungens.AnyAsync(b => b.Benutzer == id);
            var hatRaum = await _context.Raums.AnyAsync(r => r.Benutzer == id);
            if (hatLagerbewegungen || hatRaum)
            {
                TempData["Fehler"] = $"Benutzer '{benutzer.Username}' kann nicht gelöscht werden, da er Lagerbewegungen oder Räume hat.";
                return RedirectToAction(nameof(Details), new { id });
            }

            _context.Benutzers.Remove(benutzer);
            await _context.SaveChangesAsync();
            TempData["Meldung"] = $"Benutzer '{benutzer.Username}' wurde gelöscht.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Get a list of roles from the database, ordered by name, and return them as a list of SelectListItem objects for use in a dropdown menu.
        /// </summary>
        /// <returns></returns>
        private async Task<IEnumerable<SelectListItem>> RollenAsync() =>
            await _context.Rolles.OrderBy(r => r.Name).Select(r => new SelectListItem(r.Name, r.ID.ToString())).ToListAsync();

        /// <summary>
        /// Get a list of rooms from the database, ordered by room number, and return them as a list of SelectListItem objects for use in a dropdown menu.
        /// </summary>
        /// <returns></returns>
        private async Task<IEnumerable<SelectListItem>> RaumsAsync() =>
            await _context.Raums
                .Where(r => r.RaumTypNavigation.Name == RaumTypen.Labor)
                .OrderBy(r => r.RaumNum)
                .Select(r => new SelectListItem("Raum " + r.RaumNum, r.ID.ToString()))
                .ToListAsync();
    }
}