using Lagerverwaltung.Models;
using Lagerverwaltung.Services;
using Lagerverwaltung.Services.Api;
using Lagerverwaltung.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lagerverwaltung.Controllers
{
    [Authorize(Roles = Rollen.Admin)]
    public class HerstellerController : Controller
    {
        private readonly ApiClient _api;

        public HerstellerController(ApiClient api)
        {
            _api = api;
        }

        /// <summary>
        /// Show a list of all manufacturers. The list can be filtered by a search term.
        /// </summary>
        /// <param name="vm">The view model for the manufacturer list.</param>
        /// <returns>The view with the list of manufacturers.</returns>
        public async Task<IActionResult> Index(HerstellerListeViewModel vm)
        {
            var hersteller = await _api.GetManufacturersAsync();
            if (!string.IsNullOrWhiteSpace(vm.Suche))
            {
                var s = vm.Suche.Trim();
                hersteller = hersteller.Where(h => h.Name.Contains(s, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            var anzahl = (await _api.GetArticlesAsync()).Where(a => a.HerstellerId != null)
                .GroupBy(a => a.HerstellerId!.Value).ToDictionary(g => g.Key, g => g.Count());

            vm.Hersteller = hersteller.OrderBy(h => h.Name)
                .Select(h => new HerstellerZeile
                {
                    ID = h.Id,
                    Name = h.Name,
                    AnzahlArtikel = anzahl.GetValueOrDefault(h.Id)
                })
                .ToList();
            return View(vm);
        }

        /// <summary>
        /// Show the details of a manufacturer, including its articles.
        /// </summary>
        /// <param name="id">The ID of the manufacturer.</param>
        /// <returns>The view with the manufacturer details.</returns>
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var hersteller = await FindeAsync(id.Value);
            if (hersteller == null) return NotFound();

            var artikel = await _api.GetArticlesAsync(herstellerId: id);
            return View(new HerstellerDetailsViewModel
            {
                Hersteller = hersteller,
                Artikel = artikel.OrderBy(a => a.Name).ThenBy(a => a.SerienNum).Select(LagerService.ToArtikel).ToList()
            });
        }

        /// <summary>
        /// Show the form to create a new manufacturer. The form can be submitted to create the manufacturer.
        /// </summary>
        /// <param name="returnUrl">The URL to redirect to after creating the manufacturer.</param>
        /// <returns>The view with the manufacturer creation form.</returns>
        public IActionResult Create(string returnUrl = null) =>
            View(new HerstellerFormViewModel { ReturnUrl = returnUrl });

        /// <summary>
        /// Create the manufacturer (POST /api/manufacturers). A duplicate name gives 409 from the API.
        /// </summary>
        /// <param name="vm">The view model containing the new manufacturer data.</param>
        /// <returns>The view with the manufacturer creation form or a redirect to the index page.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(HerstellerFormViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            try
            {
                await _api.CreateManufacturerAsync(vm.Name.Trim());
            }
            catch (ApiException ex) when (ex.IsUserError)
            {
                ModelState.AddModelError(ex.IsConflict ? nameof(vm.Name) : "", ex.Message);
                return View(vm);
            }

            TempData["Meldung"] = $"Hersteller '{vm.Name.Trim()}' wurde angelegt.";
            if (Url.IsLocalUrl(vm.ReturnUrl)) return Redirect(vm.ReturnUrl);
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Show the form to edit an existing manufacturer. The form can be submitted to save the changes.
        /// </summary>
        /// <param name="id">The ID of the manufacturer to edit.</param>
        /// <returns>The view with the manufacturer edit form.</returns>
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var hersteller = await FindeAsync(id.Value);
            if (hersteller == null) return NotFound();
            return View(new HerstellerFormViewModel { ID = hersteller.ID, Name = hersteller.Name });
        }

        /// <summary>
        /// Save the changes (PUT /api/manufacturers/{id}).
        /// </summary>
        /// <param name="id">The ID of the manufacturer to edit.</param>
        /// <param name="vm">The view model containing the updated manufacturer data.</param>
        /// <returns>The view with the manufacturer edit form or a redirect to the details page.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, HerstellerFormViewModel vm)
        {
            if (id != vm.ID) return NotFound();
            if (!ModelState.IsValid) return View(vm);

            try
            {
                await _api.UpdateManufacturerAsync(id, vm.Name.Trim());
            }
            catch (ApiException ex) when (ex.IsUserError && !ex.IsNotFound)
            {
                ModelState.AddModelError(ex.IsConflict ? nameof(vm.Name) : "", ex.Message);
                return View(vm);
            }

            TempData["Meldung"] = "Änderungen gespeichert.";
            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>
        /// Show the confirmation page to delete a manufacturer.
        /// </summary>
        /// <param name="id">The ID of the manufacturer to delete.</param>
        /// <returns>The view with the delete confirmation form.</returns>
        public Task<IActionResult> Delete(int? id) => Details(id);

        /// <summary>
        /// Delete the manufacturer (DELETE /api/manufacturers/{id}). 409 while articles use it.
        /// </summary>
        /// <param name="id">The ID of the manufacturer to delete.</param>
        /// <returns>The view with the delete confirmation form or a redirect to the index page.</returns>
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var hersteller = await FindeAsync(id);
            if (hersteller == null) return NotFound();

            try
            {
                await _api.DeleteManufacturerAsync(id);
            }
            catch (ApiException ex) when (ex.IsConflict)
            {
                TempData["Fehler"] = ex.Message;
                return RedirectToAction(nameof(Details), new { id });
            }

            TempData["Meldung"] = $"Hersteller '{hersteller.Name}' wurde gelöscht.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>The API has no GET /api/manufacturers/{id}, so the manufacturer is taken from the list.</summary>
        private async Task<Hersteller?> FindeAsync(int id)
        {
            var h = (await _api.GetManufacturersAsync()).FirstOrDefault(h => h.Id == id);
            return h == null ? null : new Hersteller { ID = h.Id, Name = h.Name };
        }
    }
}
