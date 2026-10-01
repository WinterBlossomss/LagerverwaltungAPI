using Lagerverwaltung.Models;
using Lagerverwaltung.Services;
using Lagerverwaltung.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Lagerverwaltung.Controllers
{
    [Authorize(Roles = Rollen.Admin)]
    public class KategorieController : Controller
    {
        private readonly LagerverwaltungssystemContext _context;

        public KategorieController(LagerverwaltungssystemContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(KategorieListeViewModel vm)
        {
            var q = _context.Kategories.AsQueryable();
            if (!string.IsNullOrWhiteSpace(vm.Suche))
            {
                var s = vm.Suche.Trim();
                q = q.Where(k => k.Name.Contains(s) || k.HerstellerNavigation.Name.Contains(s)
                              || (k.ModelNum != null && k.ModelNum.ToString()!.Contains(s)));
            }
            if (vm.HerstellerId != null) q = q.Where(k => k.Hersteller == vm.HerstellerId);

            vm.Kategorien = await q
                .OrderBy(k => k.HerstellerNavigation.Name).ThenBy(k => k.Name)
                .Select(k => new KategorieZeile
                {
                    ID = k.ID,
                    Name = k.Name,
                    Hersteller = k.HerstellerNavigation.Name,
                    ModelNum = k.ModelNum,
                    AnzahlArtikel = k.Artikels.Count()
                })
                .ToListAsync();
            vm.Hersteller = await HerstellerAsync();
            return View(vm);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var kategorie = await _context.Kategories
                .Include(k => k.HerstellerNavigation)
                .Include(k => k.Artikels)
                .FirstOrDefaultAsync(k => k.ID == id);
            if (kategorie == null) return NotFound();

            return View(new KategorieDetailsViewModel
            {
                Kategorie = kategorie,
                Artikel = kategorie.Artikels.OrderBy(a => a.Name).ThenBy(a => a.SerienNum).ToList()
            });
        }

        public async Task<IActionResult> Create(string returnUrl = null)
        {
            var vm = new KategorieFormViewModel { ReturnUrl = returnUrl };
            vm.Hersteller = await HerstellerAsync();
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(KategorieFormViewModel vm)
        {
            await ValidiereAsync(vm);
            if (!ModelState.IsValid)
            {
                vm.Hersteller = await HerstellerAsync();
                return View(vm);
            }

            var kategorie = new Kategorie { Name = vm.Name.Trim(), Hersteller = vm.HerstellerId, ModelNum = vm.ModelNum };
            _context.Kategories.Add(kategorie);
            await _context.SaveChangesAsync();

            TempData["Meldung"] = $"Kategorie '{kategorie.Name}' wurde angelegt.";
            if (Url.IsLocalUrl(vm.ReturnUrl)) return Redirect(vm.ReturnUrl);
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var kategorie = await _context.Kategories.FindAsync(id);
            if (kategorie == null) return NotFound();

            return View(new KategorieFormViewModel
            {
                ID = kategorie.ID,
                Name = kategorie.Name,
                HerstellerId = kategorie.Hersteller,
                ModelNum = kategorie.ModelNum,
                Hersteller = await HerstellerAsync()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, KategorieFormViewModel vm)
        {
            if (id != vm.ID) return NotFound();
            var kategorie = await _context.Kategories.FindAsync(id);
            if (kategorie == null) return NotFound();

            await ValidiereAsync(vm);
            if (!ModelState.IsValid)
            {
                vm.Hersteller = await HerstellerAsync();
                return View(vm);
            }

            kategorie.Name = vm.Name.Trim();
            kategorie.Hersteller = vm.HerstellerId;
            kategorie.ModelNum = vm.ModelNum;
            await _context.SaveChangesAsync();

            TempData["Meldung"] = "Änderungen gespeichert.";
            return RedirectToAction(nameof(Details), new { id });
        }

        public Task<IActionResult> Delete(int? id) => Details(id);

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var kategorie = await _context.Kategories.FindAsync(id);
            if (kategorie == null) return NotFound();

            if (await _context.Artikels.AnyAsync(a => a.Kategorie == id))
            {
                TempData["Fehler"] = "Die Kategorie enthält noch Artikel und kann nicht gelöscht werden.";
                return RedirectToAction(nameof(Details), new { id });
            }

            _context.Kategories.Remove(kategorie);
            await _context.SaveChangesAsync();
            TempData["Meldung"] = $"Kategorie '{kategorie.Name}' wurde gelöscht.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>Gleicher Name beim gleichen Hersteller ist nicht erlaubt.</summary>
        private async Task ValidiereAsync(KategorieFormViewModel vm)
        {
            if (!string.IsNullOrWhiteSpace(vm.Name) &&
                await _context.Kategories.AnyAsync(k => k.Name == vm.Name.Trim() && k.Hersteller == vm.HerstellerId && k.ID != vm.ID))
                ModelState.AddModelError(nameof(vm.Name), "Diese Kategorie existiert für diesen Hersteller bereits.");
        }

        private async Task<IEnumerable<SelectListItem>> HerstellerAsync() =>
            await _context.Herstellers.OrderBy(h => h.Name)
                .Select(h => new SelectListItem(h.Name, h.ID.ToString()))
                .ToListAsync();
    }
}