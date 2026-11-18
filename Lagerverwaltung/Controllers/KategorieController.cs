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
                q = q.Where(k => k.Name.Contains(s) || (k.ModelNum != null && k.ModelNum.ToString()!.Contains(s)));
            }

            vm.Kategorien = await q
                .OrderBy(k => k.Name)
                .Select(k => new KategorieZeile
                {
                    ID = k.ID,
                    Name = k.Name,
                    ModelNum = k.ModelNum,
                    AnzahlArtikel = k.Artikels.Count()
                })
                .ToListAsync();
            return View(vm);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var kategorie = await _context.Kategories
                .Include(k => k.Artikels).ThenInclude(a => a.HerstellerNavigation)
                .FirstOrDefaultAsync(k => k.ID == id);
            if (kategorie == null) return NotFound();

            return View(new KategorieDetailsViewModel
            {
                Kategorie = kategorie,
                Artikel = kategorie.Artikels.OrderBy(a => a.Name).ThenBy(a => a.SerienNum).ToList()
            });
        }

        public IActionResult Create(string returnUrl = null) =>
            View(new KategorieFormViewModel { ReturnUrl = returnUrl });

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(KategorieFormViewModel vm)
        {
            await ValidiereAsync(vm);
            if (!ModelState.IsValid) return View(vm);

            var kategorie = new Kategorie { Name = vm.Name.Trim(), ModelNum = vm.ModelNum };
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
                ModelNum = kategorie.ModelNum
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
            if (!ModelState.IsValid) return View(vm);

            kategorie.Name = vm.Name.Trim();
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

        /// <summary>Kategorie-Namen müssen eindeutig sein.</summary>
        private async Task ValidiereAsync(KategorieFormViewModel vm)
        {
            if (!string.IsNullOrWhiteSpace(vm.Name) &&
                await _context.Kategories.AnyAsync(k => k.Name == vm.Name.Trim() && k.ID != vm.ID))
                ModelState.AddModelError(nameof(vm.Name), "Diese Kategorie existiert bereits.");
        }
    }
}