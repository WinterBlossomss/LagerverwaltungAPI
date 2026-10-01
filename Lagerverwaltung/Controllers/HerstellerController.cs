using Lagerverwaltung.Models;
using Lagerverwaltung.Services;
using Lagerverwaltung.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Model;

namespace Lagerverwaltung.Controllers
{
    [Authorize(Roles = Rollen.Admin)]
    public class HerstellerController : Controller
    {
        private readonly LagerverwaltungssystemContext _context;

        public HerstellerController(LagerverwaltungssystemContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Show a list of all manufacturers. The list can be filtered by a search term.
        /// </summary>
        /// <param name="vm">The view model for the manufacturer list.</param>
        /// <returns>The view with the list of manufacturers.</returns>
        public async Task<IActionResult> Index(HerstellerListeViewModel vm)
        {
            var q = _context.Herstellers.AsQueryable();
            if (!string.IsNullOrWhiteSpace(vm.Suche))
            {
                var s = vm.Suche.Trim();
                q = q.Where(h => h.Name.Contains(s));
            }

            vm.Hersteller = await q.OrderBy(h => h.Name)
                .Select(h => new HerstellerZeile
                {
                    ID = h.ID,
                    Name = h.Name,
                    AnzahlKategorien = h.Kategories.Count(),
                    AnzahlArtikel = h.Kategories.SelectMany(k => k.Artikels).Count()
                })
                .ToListAsync();
            return View(vm);
        }

        /// <summary>
        /// Show the details of a manufacturer, including its categories and the number of articles in each category.
        /// </summary>
        /// <param name="id">The ID of the manufacturer.</param>
        /// <returns>The view with the manufacturer details.</returns>
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var hersteller = await _context.Herstellers
                .Include(h => h.Kategories).ThenInclude(k => k.Artikels)
                .FirstOrDefaultAsync(h => h.ID == id);
            if (hersteller == null) return NotFound();

            var kategorien = hersteller.Kategories.OrderBy(k => k.Name).ToList();
            return View(new HerstellerDetailsViewModel
            {
                Hersteller = hersteller,
                Kategorien = kategorien,
                AnzahlArtikel = kategorien.Sum(k => k.Artikels.Count)
            });
        }

        /// <summary>
        /// Show the form to create a new manufacturer. The form can be submitted to create the manufacturer.
        /// </summary>
        /// <param name="returnUrl">The URL to redirect to after creating the manufacturer.</param>
        /// <returns>The view with the manufacturer creation form.</returns>
        public IActionResult Create(string returnUrl = null) =>
            View(new HerstellerFormViewModel { ReturnUrl = returnUrl });

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(HerstellerFormViewModel vm)
        {
            await ValidiereAsync(vm);
            if (!ModelState.IsValid) return View(vm);

            var hersteller = new Hersteller { Name = vm.Name.Trim() };
            _context.Herstellers.Add(hersteller);
            await _context.SaveChangesAsync();

            TempData["Meldung"] = $"Hersteller '{hersteller.Name}' wurde angelegt.";
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
            var hersteller = await _context.Herstellers.FindAsync(id);
            if (hersteller == null) return NotFound();
            return View(new HerstellerFormViewModel { ID = hersteller.ID, Name = hersteller.Name });
        }

        /// <summary>
        /// Handle the submission of the manufacturer edit form. Validates the input and saves the changes to the database.
        /// </summary>
        /// <param name="id">The ID of the manufacturer to edit.</param>
        /// <param name="vm">The view model containing the updated manufacturer data.</param>
        /// <returns>The view with the manufacturer edit form or a redirect to the details page.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, HerstellerFormViewModel vm)
        {
            if (id != vm.ID) return NotFound();
            var hersteller = await _context.Herstellers.FindAsync(id);
            if (hersteller == null) return NotFound();

            await ValidiereAsync(vm);
            if (!ModelState.IsValid) return View(vm);

            hersteller.Name = vm.Name.Trim();
            await _context.SaveChangesAsync();
            TempData["Meldung"] = "Änderungen gespeichert.";
            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>
        /// Show the confirmation page to delete a manufacturer. The manufacturer can only be deleted if it is not used by any categories.
        /// </summary>
        /// <param name="id">The ID of the manufacturer to delete.</param>
        /// <returns>The view with the delete confirmation form.</returns>
        public Task<IActionResult> Delete(int? id) => Details(id);

        /// <summary>
        /// Handle the submission of the manufacturer delete confirmation form.
        /// Validates that the manufacturer is not used by any categories and deletes it from the database.
        /// </summary>
        /// <param name="id">The ID of the manufacturer to delete.</param>
        /// <returns>The view with the delete confirmation form or a redirect to the index page.</returns>
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var hersteller = await _context.Herstellers.FindAsync(id);
            if (hersteller == null) return NotFound();

            if (await _context.Kategories.AnyAsync(k => k.Hersteller == id))
            {
                TempData["Fehler"] = "Der Hersteller wird noch von Kategorien verwendet und kann nicht gelöscht werden.";
                return RedirectToAction(nameof(Details), new { id });
            }

            _context.Herstellers.Remove(hersteller);
            await _context.SaveChangesAsync();
            TempData["Meldung"] = $"Hersteller '{hersteller.Name}' wurde gelöscht.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Validates the manufacturer form view model. Checks if the name is not empty and if it already exists in the database (excluding the current manufacturer).
        /// </summary>
        /// <param name="vm"></param>
        /// <returns></returns>
        private async Task ValidiereAsync(HerstellerFormViewModel vm)
        {
            if (!string.IsNullOrWhiteSpace(vm.Name) &&
                await _context.Herstellers.AnyAsync(h => h.Name == vm.Name.Trim() && h.ID != vm.ID))
                ModelState.AddModelError(nameof(vm.Name), "Dieser Hersteller existiert bereits.");
        }
    }
}