using Lagerverwaltung.Models;
using Lagerverwaltung.Services;
using Lagerverwaltung.Services.Api;
using Lagerverwaltung.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Lagerverwaltung.Controllers
{
    /// <summary>
    /// Verwaltung der Labore. Hauptlager und Umbuchungslager gibt es genau einmal und werden hier nicht angelegt.
    /// </summary>
    [Authorize(Roles = Rollen.Admin)]
    public class RaumController : Controller
    {
        private readonly ApiClient _api;
        private readonly LagerService _lager;

        public RaumController(ApiClient api, LagerService lager)
        {
            _api = api;
            _lager = lager;
        }

        /// <summary>
        /// Show a list of all laboratories. The list can be filtered by room number or responsible user.
        /// </summary>
        public async Task<IActionResult> Index(RaumListeViewModel vm)
        {
            IEnumerable<Raum> labore = await _lager.GetLaboreAsync();
            if (!string.IsNullOrWhiteSpace(vm.Suche))
            {
                var s = vm.Suche.Trim();
                labore = labore.Where(r => (r.RaumNum?.ToString().Contains(s) ?? false)
                    || r.BenutzerNavigation.Username.Contains(s, StringComparison.OrdinalIgnoreCase));
            }

            var liste = labore.OrderBy(r => r.RaumNum).ToList();
            var bestand = await _lager.BestandAsync(liste);

            vm.Raeume = liste
                .Select(r => new RaumZeile
                {
                    ID = r.ID,
                    RaumNum = r.RaumNum,
                    Verantwortlich = r.BenutzerNavigation.Username,
                    Positionen = bestand.Count(b => b.Raum == r.ID),
                    Stueck = bestand.Where(b => b.Raum == r.ID).Sum(b => b.Menge)
                })
                .ToList();
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

        /// <summary>POST /api/locations with RaumTyp "Labor".</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RaumFormViewModel vm)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    await _api.CreateRoomAsync(RaumTypen.Labor, vm.BenutzerId!.Value, vm.RaumNum);
                    TempData["Meldung"] = $"Raum {vm.RaumNum} wurde angelegt.";
                    return RedirectToAction(nameof(Index));
                }
                catch (ApiException ex) when (ex.IsUserError)
                {
                    ModelState.AddModelError(FeldFuer(ex), ex.Message);
                }
            }

            await FuelleListenAsync(vm);
            return View(vm);
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

        /// <summary>
        /// The API changes the room number (PUT /api/locations/{id}) and the responsible LehrerIn
        /// (PUT /api/locations/{id}/teacher) separately, so only what changed is sent.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, RaumFormViewModel vm)
        {
            if (id != vm.ID) return NotFound();
            var raum = await LaborAsync(id);
            if (raum == null) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    if (vm.RaumNum != raum.RaumNum)
                        await _api.UpdateRoomAsync(id, vm.RaumNum);
                    if (vm.BenutzerId != raum.Benutzer)
                        await _api.AssignTeacherAsync(id, vm.BenutzerId!.Value);

                    TempData["Meldung"] = $"Raum {vm.RaumNum} wurde gespeichert.";
                    return RedirectToAction(nameof(Index));
                }
                catch (ApiException ex) when (ex.IsUserError && !ex.IsNotFound)
                {
                    ModelState.AddModelError(FeldFuer(ex), ex.Message);
                }
            }

            await FuelleListenAsync(vm);
            return View(vm);
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
                Positionen = (await _lager.BestandAsync(raum)).Count,
                HatBewegungen = (await _lager.BewegungenAsync(raumId: raum.ID)).Any()
            });
        }

        /// <summary>DELETE /api/locations/{id}. 409 if the room has stock or history.</summary>
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var raum = await LaborAsync(id);
            if (raum == null) return NotFound();

            try
            {
                await _api.DeleteRoomAsync(id);
            }
            catch (ApiException ex) when (ex.IsConflict)
            {
                TempData["Fehler"] = $"Raum {raum.RaumNum}: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }

            TempData["Meldung"] = $"Raum {raum.RaumNum} wurde gelöscht.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>Puts the API message next to the form field it is about.</summary>
        private static string FeldFuer(ApiException ex) =>
            ex.Message.Contains("Raumnummer") ? nameof(RaumFormViewModel.RaumNum)
            : ex.Message.Contains("LehrerIn") || ex.Message.Contains("Benutzer") ? nameof(RaumFormViewModel.BenutzerId)
            : "";

        /// <summary>
        /// Fills the dropdown with users that can be responsible: LehrerInnen without a laboratory
        /// (the API only allows a LehrerIn, and only one lab per LehrerIn).
        /// The current responsible user of the edited laboratory is always included.
        /// </summary>
        private async Task FuelleListenAsync(RaumFormViewModel vm)
        {
            var labore = await _lager.GetLaboreAsync();
            vm.Verantwortliche = (await _api.GetUsersAsync())
                .Where(b => b.RoleName == Rollen.Lehrer || b.Id == vm.BenutzerId)
                .Where(b => !labore.Any(r => r.Benutzer == b.Id && r.ID != vm.ID))
                .OrderBy(b => b.RoleName != Rollen.Lehrer).ThenBy(b => b.Username)
                .Select(b => new SelectListItem(
                    b.RoleName == Rollen.Lehrer ? b.Username : $"{b.Username} ({b.RoleName})",
                    b.Id.ToString()))
                .ToList();
        }

        private async Task<Raum?> LaborAsync(int id) =>
            (await _lager.GetLaboreAsync()).FirstOrDefault(r => r.ID == id);
    }
}
