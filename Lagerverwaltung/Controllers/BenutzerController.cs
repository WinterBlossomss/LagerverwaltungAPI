using Lagerverwaltung.Models;
using Lagerverwaltung.Services;
using Lagerverwaltung.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Lagerverwaltung.Controllers
{
    [Authorize(Roles = Rollen.Admin)]
    public class BenutzerController : Controller
    {
        private readonly LagerverwaltungssystemContext _context;
        private readonly IPasswordHasher<Benutzer> _hasher;

        public BenutzerController(LagerverwaltungssystemContext context, IPasswordHasher<Benutzer> hasher)
        {
            _context = context;
            _hasher = hasher;
        }

        /// <summary>
        /// Shows a list of users with optional search and filtering by role and laboratory.
        /// </summary>
        /// <param name="vm">The view model for the user list.</param>
        /// <returns></returns>
        public async Task<IActionResult> Index(BenutzerListeViewModel vm)
        {
            var q = _context.Benutzers.AsQueryable();

            if (!string.IsNullOrWhiteSpace(vm.Suche))
            {
                var s = vm.Suche.Trim();
                q = q.Where(b => b.Username.Contains(s) || b.RolleNavigation.Name.Contains(s) || b.Raums.Any(r => r.RaumNum.ToString().Contains(s)));
            }
            if (int.TryParse(vm.Rolle, out var rolleId))
                q = q.Where(b => b.Rolle == rolleId);
            if (int.TryParse(vm.Labor, out var laborId))
                q = q.Where(b => b.Raums.Any(r => r.ID == laborId));

            var daten = await q
                .OrderBy(b => b.Username)
                .Select(b => new
                {
                    b.ID,
                    b.Username,
                    Rolle = b.RolleNavigation.Name,
                    Labore = b.Raums.Where(r => r.RaumNum != null).Select(r => r.RaumNum).ToList()
                })
                .ToListAsync();

            vm.Benutzer = daten.Select(d => new BenutzerZeile
            {
                ID = d.ID,
                Username = d.Username,
                Rolle = d.Rolle,
                Labor = LaborText(d.Labore)
            }).ToList();

            vm.Rollen = await RollenAsync();
            vm.Labore = await LaboreAsync();
            return View(vm);
        }

        /// <summary>
        /// Shows the details of a user, including the assigned laboratories and the latest inventory movements.
        /// </summary>
        /// <param name="id">The ID of the user.</param>
        /// <returns></returns>
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var benutzer = await _context.Benutzers
                .Include(b => b.Raums)
                .Include(b => b.RolleNavigation)
                .FirstOrDefaultAsync(b => b.ID == id);
            if (benutzer == null) return NotFound();

            var vm = new BenutzerDetailsViewModel
            {
                Benutzer = benutzer,
                Labor = LaborText(benutzer.Raums.Select(r => r.RaumNum)),
                Bewegungen = await _context.Lagerbewegungens.MitDetails()
                    .Where(b => b.Benutzer == id)
                    .OrderByDescending(b => b.Datum).ThenByDescending(b => b.ID)
                    .Take(20).ToListAsync(),
                KannGeloeschtWerden = !await _context.Lagerbewegungens.AnyAsync(b => b.Benutzer == id)
                                   && !await _context.Raums.AnyAsync(r => r.Benutzer == id)
            };
            return View(vm);
        }

        /// <summary>
        /// Displays the form to create a new user.
        /// </summary>
        /// <returns></returns>
        public async Task<IActionResult> Create()
        {
            var vm = new BenutzerFormViewModel();
            await FuelleListenAsync(vm);
            return View(vm);
        }

        /// <summary>
        /// Handles the submission of the form to create a new user, including validation and password hashing.
        /// </summary>
        /// <param name="vm"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BenutzerFormViewModel vm)
        {
            if (string.IsNullOrWhiteSpace(vm.Password))
                ModelState.AddModelError(nameof(vm.Password), "Bitte ein Passwort eingeben.");
            await ValidiereAsync(vm);

            if (!ModelState.IsValid)
            {
                await FuelleListenAsync(vm);
                return View(vm);
            }

            var benutzer = new Benutzer
            {
                Username = vm.Username.Trim(),
                Rolle = int.Parse(vm.Rolle)
            };
            benutzer.Password = _hasher.HashPassword(benutzer, vm.Password);
            _context.Benutzers.Add(benutzer);

            await LaborZuweisenAsync(benutzer, vm.LaborId);
            await _context.SaveChangesAsync();

            TempData["Meldung"] = $"Benutzer '{benutzer.Username}' wurde erfasst.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Displays the form to edit an existing user, pre-filling the fields with the current data.
        /// </summary>
        /// <param name="id">The ID of the user to edit.</param>
        /// <returns></returns>
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var benutzer = await _context.Benutzers
                .Include(b => b.Raums).ThenInclude(r => r.RaumTypNavigation)
                .FirstOrDefaultAsync(b => b.ID == id);
            if (benutzer == null) return NotFound();

            var vm = new BenutzerFormViewModel
            {
                ID = benutzer.ID,
                Username = benutzer.Username,
                Rolle = benutzer.Rolle.ToString(),
                LaborId = benutzer.Raums.FirstOrDefault(r => r.RaumTypNavigation.Name == RaumTypen.Labor)?.ID
            };
            await FuelleListenAsync(vm);
            return View(vm);
        }

        /// <summary>
        /// Handles the submission of the form to edit an existing user, including validation and password hashing if a new password is provided.
        /// </summary>
        /// <param name="id">The ID of the user to edit.</param>
        /// <param name="vm">The view model for the user form.</param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, BenutzerFormViewModel vm)
        {
            if (id != vm.ID) return NotFound();

            await ValidiereAsync(vm);
            if (!ModelState.IsValid)
            {
                await FuelleListenAsync(vm);
                return View(vm);
            }

            var benutzer = await _context.Benutzers.FindAsync(id);
            if (benutzer == null) return NotFound();

            benutzer.Username = vm.Username.Trim();
            benutzer.Rolle = int.Parse(vm.Rolle);
            if (!string.IsNullOrWhiteSpace(vm.Password))
                benutzer.Password = _hasher.HashPassword(benutzer, vm.Password);

            await LaborZuweisenAsync(benutzer, vm.LaborId);
            await _context.SaveChangesAsync();

            TempData["Meldung"] = $"Benutzer '{benutzer.Username}' wurde gespeichert.";
            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>
        /// Displays the confirmation page for deleting a user, showing details and checking if the user can be deleted.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public Task<IActionResult> Delete(int? id) => Details(id);

        /// <summary>
        /// Handles the deletion of a user after confirmation, checking for references in inventory movements and laboratories before allowing deletion.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var benutzer = await _context.Benutzers.FindAsync(id);
            if (benutzer == null) return NotFound();

            var referenziert = await _context.Lagerbewegungens.AnyAsync(b => b.Benutzer == id)
                            || await _context.Raums.AnyAsync(r => r.Benutzer == id);
            if (referenziert)
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
        /// Validates the user form view model, checking for unique usernames and ensuring that teachers have an assigned laboratory if required.
        /// </summary>
        /// <param name="vm"></param>
        /// <returns></returns>
        private async Task ValidiereAsync(BenutzerFormViewModel vm)
        {
            if (!string.IsNullOrWhiteSpace(vm.Username) &&
                await _context.Benutzers.AnyAsync(b => b.Username == vm.Username.Trim() && b.ID != vm.ID))
                ModelState.AddModelError(nameof(vm.Username), "Dieser Benutzername ist bereits vergeben.");

            if (int.TryParse(vm.Rolle, out var rolleId))
            {
                var rolle = await _context.Rolles.FindAsync(rolleId);
                if (rolle?.Name == Rollen.Lehrer && vm.LaborId == null)
                    ModelState.AddModelError(nameof(vm.LaborId), "LehrerInnen brauchen ein Labor.");
            }
        }

        /// <summary>
        /// Assigns a laboratory to a user by updating the navigation property of the laboratory entity.
        /// If the laboratory ID is null, no assignment is made.
        /// </summary>
        /// <param name="benutzer"></param>
        /// <param name="laborId"></param>
        /// <returns></returns>
        private async Task LaborZuweisenAsync(Benutzer benutzer, int? laborId)
        {
            if (laborId == null) return;
            var labor = await _context.Raums.FindAsync(laborId.Value);
            if (labor != null) labor.BenutzerNavigation = benutzer;
        }

        /// <summary>
        /// Fills the lists of roles and laboratories in the user form view model for use in dropdowns in the view.
        /// </summary>
        /// <param name="vm"></param>
        /// <returns></returns>
        private async Task FuelleListenAsync(BenutzerFormViewModel vm)
        {
            vm.Rollen = await RollenAsync();
            vm.Labore = await LaboreAsync();
        }

        /// <summary>
        /// Generates a string representation of the assigned laboratories for display in the user list or details view.
        /// </summary>
        /// <param name="raumNums"></param>
        /// <returns></returns>
        private static string LaborText(IEnumerable<int?> raumNums)
        {
            var liste = raumNums.Where(n => n != null).OrderBy(n => n).Select(n => $"Raum {n}").ToList();
            return liste.Any() ? string.Join(", ", liste) : "—";
        }

        /// <summary>
        /// Retrieves the list of roles from the database and returns them as a collection of SelectListItem for use in dropdowns in the view.
        /// </summary>
        /// <returns></returns>
        private async Task<IEnumerable<SelectListItem>> RollenAsync() =>
            await _context.Rolles.OrderBy(r => r.Name)
                .Select(r => new SelectListItem(r.Name, r.ID.ToString()))
                .ToListAsync();

        /// <summary>
        /// Retrieves the list of laboratories from the database and returns them as a collection of SelectListItem for use in dropdowns in the view.
        /// Only rooms with the type "Labor" are included, and they are ordered by room number.
        /// </summary>
        /// <returns></returns>
        private async Task<IEnumerable<SelectListItem>> LaboreAsync() =>
            await _context.Raums
                .Where(r => r.RaumTypNavigation.Name == RaumTypen.Labor)
                .OrderBy(r => r.RaumNum)
                .Select(r => new SelectListItem("Raum " + r.RaumNum + " · " + r.BenutzerNavigation.Username, r.ID.ToString()))
                .ToListAsync();
    }
}