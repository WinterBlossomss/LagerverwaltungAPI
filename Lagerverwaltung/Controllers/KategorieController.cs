using Lagerverwaltung.Models;
using Lagerverwaltung.Services;
using Lagerverwaltung.Services.Api;
using Lagerverwaltung.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lagerverwaltung.Controllers
{
    [Authorize(Roles = Rollen.Admin)]
    public class KategorieController : Controller
    {
        private readonly ApiClient _api;

        public KategorieController(ApiClient api)
        {
            _api = api;
        }

        public async Task<IActionResult> Index(KategorieListeViewModel vm)
        {
            var kategorien = await _api.GetCategoriesAsync();
            if (!string.IsNullOrWhiteSpace(vm.Suche))
            {
                var s = vm.Suche.Trim();
                kategorien = kategorien.Where(k => k.Name.Contains(s, StringComparison.OrdinalIgnoreCase)
                    || (k.ModelNum?.ToString().Contains(s) ?? false)).ToList();
            }

            // Number of articles per category, counted from the article list
            var anzahl = (await _api.GetArticlesAsync()).GroupBy(a => a.KategorieId).ToDictionary(g => g.Key, g => g.Count());

            vm.Kategorien = kategorien
                .OrderBy(k => k.Name)
                .Select(k => new KategorieZeile
                {
                    ID = k.Id,
                    Name = k.Name,
                    ModelNum = k.ModelNum,
                    AnzahlArtikel = anzahl.GetValueOrDefault(k.Id)
                })
                .ToList();
            return View(vm);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var kategorie = await FindeAsync(id.Value);
            if (kategorie == null) return NotFound();

            var artikel = await _api.GetArticlesAsync(kategorieId: id);
            return View(new KategorieDetailsViewModel
            {
                Kategorie = kategorie,
                Artikel = artikel.OrderBy(a => a.Name).ThenBy(a => a.SerienNum).Select(LagerService.ToArtikel).ToList()
            });
        }

        public IActionResult Create(string returnUrl = null) =>
            View(new KategorieFormViewModel { ReturnUrl = returnUrl });

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(KategorieFormViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            try
            {
                await _api.CreateCategoryAsync(vm.Name.Trim(), vm.ModelNum);
            }
            catch (ApiException ex) when (ex.IsUserError)
            {
                ModelState.AddModelError(ex.IsConflict ? nameof(vm.Name) : "", ex.Message);   // 409: name already exists
                return View(vm);
            }

            TempData["Meldung"] = $"Kategorie '{vm.Name.Trim()}' wurde angelegt.";
            if (Url.IsLocalUrl(vm.ReturnUrl)) return Redirect(vm.ReturnUrl);
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var kategorie = await FindeAsync(id.Value);
            if (kategorie == null) return NotFound();

            return View(new KategorieFormViewModel
            {
                ID = kategorie.ID,
                Name = kategorie.Name,
                ModelNum = kategorie.ModelNum
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, KategorieFormViewModel vm)
        {
            if (id != vm.ID) return NotFound();
            if (!ModelState.IsValid) return View(vm);

            try
            {
                await _api.UpdateCategoryAsync(id, vm.Name.Trim(), vm.ModelNum);
            }
            catch (ApiException ex) when (ex.IsUserError && !ex.IsNotFound)
            {
                ModelState.AddModelError(ex.IsConflict ? nameof(vm.Name) : "", ex.Message);
                return View(vm);
            }

            TempData["Meldung"] = "Änderungen gespeichert.";
            return RedirectToAction(nameof(Details), new { id });
        }

        public Task<IActionResult> Delete(int? id) => Details(id);

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var kategorie = await FindeAsync(id);
            if (kategorie == null) return NotFound();

            try
            {
                await _api.DeleteCategoryAsync(id);
            }
            catch (ApiException ex) when (ex.IsConflict)
            {
                TempData["Fehler"] = ex.Message;   // "Kategorie wird noch von Artikeln verwendet."
                return RedirectToAction(nameof(Details), new { id });
            }

            TempData["Meldung"] = $"Kategorie '{kategorie.Name}' wurde gelöscht.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>The API has no GET /api/categories/{id}, so the category is taken from the list.</summary>
        private async Task<Kategorie?> FindeAsync(int id)
        {
            var k = (await _api.GetCategoriesAsync()).FirstOrDefault(k => k.Id == id);
            return k == null ? null : new Kategorie { ID = k.Id, Name = k.Name, ModelNum = k.ModelNum };
        }
    }
}
