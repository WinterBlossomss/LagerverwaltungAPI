using Lagerverwaltung.Services;
using Lagerverwaltung.Services.Api;
using Lagerverwaltung.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Lagerverwaltung.Controllers
{
    [Authorize(Roles = Rollen.Admin)]
    public class ArtikelController : Controller
    {
        private readonly ApiClient _api;
        private readonly LagerService _lager;

        public ArtikelController(ApiClient api, LagerService lager)
        {
            _api = api;
            _lager = lager;
        }

        /// <summary>
        /// Show a list of all articles, optionally filtered by search term and category.
        /// </summary>
        /// <param name="vm">The view model for the article list.</param>
        /// <returns>The action result.</returns>
        public async Task<IActionResult> Index(ArtikelListeViewModel vm)
        {
            // Category and manufacturer are filtered by the API; the search term also matches parts of the serial number
            var artikel = await _api.GetArticlesAsync(vm.KategorieId, vm.HerstellerId);
            if (!string.IsNullOrWhiteSpace(vm.Suche))
            {
                var s = vm.Suche.Trim();
                artikel = artikel.Where(a => a.Name.Contains(s, StringComparison.OrdinalIgnoreCase)
                    || (a.SerienNum?.ToString().Contains(s) ?? false)).ToList();
            }

            // Order the articles by name and then by serial number, and load them into the view model
            vm.Artikel = artikel.OrderBy(a => a.Name).ThenBy(a => a.SerienNum).Select(LagerService.ToArtikel).ToList();
            vm.Gesamtbestand = artikel.ToDictionary(a => a.Id, a => a.Bestand);
            vm.Kategorien = await KategorienAsync();
            vm.Hersteller = await HerstellerAsync();
            return View(vm);
        }

        /// <summary>
        /// Show the details of a specific article, including its locations and movements.
        /// </summary>
        /// <param name="id">The ID of the article to display.</param>
        /// <returns>The action result.</returns>
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var artikel = LagerService.ToArtikel(await _api.GetArticleAsync(id.Value));   // 404 -> NotFound (ApiExceptionFilter)

            // Locations: the stock of every room, filtered to this article
            var standorte = (await _lager.BestandAsync(await _lager.GetRaeumeAsync()))
                .Where(r => r.Art == id).ToList();

            // Movements of the article, newest first
            var bewegungen = await _lager.BewegungenAsync(artikelId: id);

            // Return the view with the article details, locations, movements, and a flag indicating whether the article can be deleted
            return View(new ArtikelDetailsViewModel
            {
                Artikel = artikel,
                Standorte = standorte,
                Bewegungen = bewegungen,
                KannGeloeschtWerden = !bewegungen.Any() && !standorte.Any()
            });
        }

        /// <summary>
        /// Show the form to create a new article, with a list of categories to choose from.
        /// </summary>
        /// <returns>The action result.</returns>
        public async Task<IActionResult> Create()
        {
            var vm = new ArtikelFormViewModel();
            await FuelleListenAsync(vm);
            return View(vm);
        }

        /// <summary>
        /// Creates the article (POST /api/articles) and books the initial stock into the Hauptlager (POST /api/articles/{id}/stock).
        /// </summary>
        /// <param name="vm">The view model containing the article data.</param>
        /// <returns>The action result.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ArtikelFormViewModel vm)
        {
            if (vm.SerienNum != null && vm.Anfangsbestand != 1)
                ModelState.AddModelError(nameof(vm.Anfangsbestand), "Ein Gerät mit Seriennummer hat immer den Bestand 1.");

            if (ModelState.IsValid)
            {
                int id;
                try
                {
                    id = await _api.CreateArticleAsync(vm.Name.Trim(), vm.SerienNum, vm.KategorieId!.Value, vm.HerstellerId);
                }
                catch (ApiException ex) when (ex.IsUserError)
                {
                    // e.g. 409 "Diese Seriennummer ist bereits vergeben."
                    ModelState.AddModelError(vm.SerienNum != null && ex.IsConflict ? nameof(vm.SerienNum) : "", ex.Message);
                    await FuelleListenAsync(vm);
                    return View(vm);
                }

                try
                {
                    await _api.AddStockAsync(id, vm.Anfangsbestand);
                }
                catch (ApiException ex) when (ex.IsUserError)
                {
                    TempData["Fehler"] = $"{vm.Name} wurde erfasst, aber der Anfangsbestand konnte nicht gebucht werden: {ex.Message}";
                    return RedirectToAction(nameof(Details), new { id });
                }

                TempData["Meldung"] = $"{vm.Name} wurde erfasst und ins Hauptlager gebucht.";
                return RedirectToAction(nameof(Index));
            }

            await FuelleListenAsync(vm);
            return View(vm);
        }

        /// <summary>
        /// Show the form to edit an existing article, pre-populated with its current data and a list of categories to choose from.
        /// </summary>
        /// <param name="id">The ID of the article to edit.</param>
        /// <returns>The action result.</returns>
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var artikel = await _api.GetArticleAsync(id.Value);

            var vm = new ArtikelFormViewModel
            {
                ID = artikel.Id,
                Name = artikel.Name,
                SerienNum = artikel.SerienNum,
                KategorieId = artikel.KategorieId,
                HerstellerId = artikel.HerstellerId
            };
            await FuelleListenAsync(vm);
            return View(vm);
        }

        /// <summary>
        /// Update an existing article (PUT /api/articles/{id}).
        /// </summary>
        /// <param name="id">The ID of the article to update.</param>
        /// <param name="vm">The view model containing the updated article data.</param>
        /// <returns>The action result.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ArtikelFormViewModel vm)
        {
            if (id != vm.ID) return NotFound();

            ModelState.Remove(nameof(vm.Anfangsbestand));
            if (ModelState.IsValid)
            {
                try
                {
                    await _api.UpdateArticleAsync(id, vm.Name.Trim(), vm.SerienNum, vm.KategorieId!.Value, vm.HerstellerId);
                    TempData["Meldung"] = "Änderungen gespeichert.";
                    return RedirectToAction(nameof(Details), new { id });
                }
                catch (ApiException ex) when (ex.IsUserError && !ex.IsNotFound)
                {
                    // e.g. serial number already used, or stock > 1 for an article that should get a serial number
                    ModelState.AddModelError(vm.SerienNum != null && ex.IsConflict ? nameof(vm.SerienNum) : "", ex.Message);
                }
            }

            await FuelleListenAsync(vm);
            return View(vm);
        }

        /// <summary>
        /// Show the form to delete an existing article.
        /// </summary>
        /// <param name="id">The ID of the article to delete.</param>
        /// <returns>The action result.</returns>
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            return await Details(id);
        }

        /// <summary>
        /// Delete an article (DELETE /api/articles/{id}). The API refuses with 409 if it has stock or movements.
        /// </summary>
        /// <param name="id">The ID of the article to delete.</param>
        /// <returns>The action result.</returns>
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var artikel = await _api.GetArticleAsync(id);
            try
            {
                await _api.DeleteArticleAsync(id);
            }
            catch (ApiException ex) when (ex.IsConflict)
            {
                TempData["Fehler"] = ex.Message;
                return RedirectToAction(nameof(Details), new { id });
            }

            TempData["Meldung"] = $"{artikel.Name} wurde gelöscht.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Categories for the dropdown, ordered by name.
        /// </summary>
        private async Task<IEnumerable<SelectListItem>> KategorienAsync() =>
            (await _api.GetCategoriesAsync()).OrderBy(k => k.Name).Select(k => new SelectListItem(k.Name, k.Id.ToString())).ToList();

        /// <summary>
        /// Fill both dropdown lists (categories and manufacturers) of the article form.
        /// </summary>
        /// <param name="vm">The article form view model.</param>
        private async Task FuelleListenAsync(ArtikelFormViewModel vm)
        {
            vm.Kategorien = await KategorienAsync();
            vm.Hersteller = await HerstellerAsync();
        }

        /// <summary>
        /// Manufacturers for the dropdown, ordered by name.
        /// </summary>
        private async Task<IEnumerable<SelectListItem>> HerstellerAsync() =>
            (await _api.GetManufacturersAsync()).OrderBy(h => h.Name).Select(h => new SelectListItem(h.Name, h.Id.ToString())).ToList();
    }
}
