using Lagerverwaltung.Models;
using Lagerverwaltung.Services;
using Lagerverwaltung.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Lagerverwaltung.Controllers
{
    /// <summary>
    /// Verwaltung der Labore. Hauptlager und Umbuchungslager gibt es genau einmal und werden hier nicht angelegt.
    /// </summary>
    [Authorize(Roles = Rollen.Admin)]
    public class RaumController : Controller
    {
        private readonly LagerverwaltungssystemContext _context;

        public RaumController(LagerverwaltungssystemContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Show a list of all laboratories. The list can be filtered by room number or responsible user.
        /// </summary>
        public async Task<IActionResult> Index(RaumListeViewModel vm)
        {
            var q = _context.Raums.Where(r => r.RaumTypNavigation.Name == RaumTypen.Labor);
            if (!string.IsNullOrWhiteSpace(vm.Suche))
            {
                var s = vm.Suche.Trim();
                q = q.Where(r => r.RaumNum.ToString().Contains(s) || r.BenutzerNavigation.Username.Contains(s));
            }

            vm.Raeume = await q.OrderBy(r => r.RaumNum)
                .Select(r => new RaumZeile
                {
                    ID = r.ID,
                    RaumNum = r.RaumNum,
                    Verantwortlich = r.BenutzerNavigation.Username,
                    Positionen = r.RaumArtikels.Count(),
                    Stueck = r.RaumArtikels.Sum(ra => (int?)ra.Menge) ?? 0
                })
                .ToListAsync();
            return View(vm);
        }

        /// <summary>
        /// Show the form to create a new laboratory.
        /// </summary>
        public async Task<IActionResult> Create()
        {
            var vm = new RaumFormViewModel();
            await FuelleListenAsync(vm);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RaumFormViewModel vm)
        {
            await ValidiereAsync(vm);
            if (!ModelState.IsValid)
            {
                await FuelleListenAsync(vm);
                return View(vm);
            }

            var laborTyp = await _context.RaumTyps.FirstAsync(t => t.Name == RaumTypen.Labor);
            var raum = new Raum
            {
                RaumTyp = laborTyp.ID,
                RaumNum = vm.RaumNum,
                Benutzer = vm.BenutzerId!.Value
            };
            _context.Raums.Add(raum);
            await _context.SaveChangesAsync();

            TempData["Meldung"] = $"Raum {raum.RaumNum} wurde angelegt.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Show the form to edit room number and responsible user of a laboratory.
        /// </summary>
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var raum = await LaborAsync(id.Value);
            if (raum == null) return NotFound();

            var vm = new RaumFormViewModel { ID = raum.ID, RaumNum = raum.RaumNum, BenutzerId = raum.Benutzer };
            await FuelleListenAsync(vm);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, RaumFormViewModel vm)
        {
            if (id != vm.ID) return NotFound();
            var raum = await LaborAsync(id);
            if (raum == null) return NotFound();

            await ValidiereAsync(vm);
            if (!ModelState.IsValid)
            {
                await FuelleListenAsync(vm);
                return View(vm);
            }

            raum.RaumNum = vm.RaumNum;
            raum.Benutzer = vm.BenutzerId!.Value;
            await _context.SaveChangesAsync();

            TempData["Meldung"] = $"Raum {raum.RaumNum} wurde gespeichert.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Show the confirmation page to delete a laboratory. Only empty laboratories without movements can be deleted.
        /// </summary>
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var raum = await LaborAsync(id.Value);
            if (raum == null) return NotFound();

            return View(new RaumDetailsViewModel
            {
                Raum = raum,
                Positionen = await _context.RaumArtikels.CountAsync(ra => ra.Raum == raum.ID),
                HatBewegungen = await HatBewegungenAsync(raum.ID)
            });
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var raum = await LaborAsync(id);
            if (raum == null) return NotFound();

            if (await _context.RaumArtikels.AnyAsync(ra => ra.Raum == id) || await HatBewegungenAsync(id))
            {
                TempData["Fehler"] = $"Raum {raum.RaumNum} hat Bestand oder Lagerbewegungen und kann nicht gelöscht werden.";
                return RedirectToAction(nameof(Index));
            }

            _context.Raums.Remove(raum);
            await _context.SaveChangesAsync();
            TempData["Meldung"] = $"Raum {raum.RaumNum} wurde gelöscht.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Checks that the room number is unique among laboratories and that the responsible user has no other laboratory.
        /// </summary>
        private async Task ValidiereAsync(RaumFormViewModel vm)
        {
            if (vm.RaumNum != null &&
                await _context.Raums.AnyAsync(r => r.RaumTypNavigation.Name == RaumTypen.Labor && r.RaumNum == vm.RaumNum && r.ID != vm.ID))
                ModelState.AddModelError(nameof(vm.RaumNum), "Diese Raumnummer ist bereits vergeben.");

            if (vm.BenutzerId != null)
            {
                var benutzer = await _context.Benutzers.Include(b => b.RolleNavigation).FirstOrDefaultAsync(b => b.ID == vm.BenutzerId);
                if (benutzer == null)
                    ModelState.AddModelError(nameof(vm.BenutzerId), "Benutzer nicht gefunden.");
                else if (await _context.Raums.AnyAsync(r => r.RaumTypNavigation.Name == RaumTypen.Labor && r.Benutzer == vm.BenutzerId && r.ID != vm.ID))
                    ModelState.AddModelError(nameof(vm.BenutzerId), $"{benutzer.Username} ist bereits für ein anderes Labor verantwortlich.");
            }
        }

        /// <summary>
        /// Fills the dropdown with users that can be responsible: LehrerInnen without a laboratory and the logged-in administrator.
        /// The current responsible user of the edited laboratory is always included.
        /// </summary>
        private async Task FuelleListenAsync(RaumFormViewModel vm)
        {
            var adminId = User.GetBenutzerId();
            vm.Verantwortliche = await _context.Benutzers
                .Where(b => b.RolleNavigation.Name == Rollen.Lehrer || b.ID == adminId || b.ID == vm.BenutzerId)
                .Where(b => !b.Raums.Any(r => r.RaumTypNavigation.Name == RaumTypen.Labor && r.ID != vm.ID) || b.ID == adminId)
                .OrderBy(b => b.RolleNavigation.Name != Rollen.Lehrer).ThenBy(b => b.Username)
                .Select(b => new SelectListItem(
                    b.RolleNavigation.Name == Rollen.Lehrer ? b.Username : b.Username + " (Administrator, vorläufig)",
                    b.ID.ToString()))
                .ToListAsync();
        }

        private Task<Raum> LaborAsync(int id) =>
            _context.Raums
                .Include(r => r.BenutzerNavigation)
                .Include(r => r.RaumTypNavigation)
                .FirstOrDefaultAsync(r => r.ID == id && r.RaumTypNavigation.Name == RaumTypen.Labor);

        private Task<bool> HatBewegungenAsync(int raumId) =>
            _context.Lagerbewegungens.AnyAsync(b => b.VonRaum == raumId || b.ZuRaum == raumId);
    }
}
