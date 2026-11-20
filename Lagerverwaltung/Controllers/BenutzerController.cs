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
    /// User administration over the REST API:
    /// - create  = POST /api/auth/register (always a LehrerIn) + PUT /api/users/{id}/role if Administrator
    /// - role    = PUT /api/users/{id}/role
    /// - lab     = PUT /api/locations/{labId}/teacher
    /// - delete  = DELETE /api/users/{id}
    /// The API has no endpoint for an administrator to rename another user or reset their password.
    /// </summary>
    [Authorize(Roles = Rollen.Admin)]
    public class BenutzerController : Controller
    {
        private readonly ApiClient _api;
        private readonly LagerService _lager;

        public BenutzerController(ApiClient api, LagerService lager)
        {
            _api = api;
            _lager = lager;
        }

        /// <summary>
        /// Shows a list of users with optional search and filtering by role and laboratory.
        /// </summary>
        /// <param name="vm">The view model for the user list.</param>
        /// <returns></returns>
        public async Task<IActionResult> Index(BenutzerListeViewModel vm)
        {
            IEnumerable<UserDto> q = await _api.GetUsersAsync();
            var labore = await _lager.GetLaboreAsync();
            List<int?> LaborNummern(int userId) => labore.Where(r => r.Benutzer == userId && r.RaumNum != null).Select(r => r.RaumNum).ToList();

            if (!string.IsNullOrWhiteSpace(vm.Suche))
            {
                var s = vm.Suche.Trim();
                q = q.Where(b => b.Username.Contains(s, StringComparison.OrdinalIgnoreCase)
                    || b.RoleName.Contains(s, StringComparison.OrdinalIgnoreCase)
                    || LaborNummern(b.Id).Any(n => n.ToString()!.Contains(s)));
            }
            if (!string.IsNullOrEmpty(vm.Rolle))
                q = q.Where(b => b.RoleName == vm.Rolle);
            if (int.TryParse(vm.Labor, out var laborId))
                q = q.Where(b => labore.Any(r => r.ID == laborId && r.Benutzer == b.Id));

            vm.Benutzer = q.OrderBy(b => b.Username).Select(b => new BenutzerZeile
            {
                ID = b.Id,
                Username = b.Username,
                Rolle = b.RoleName,
                Labor = LaborText(LaborNummern(b.Id))
            }).ToList();

            vm.Rollen = RollenListe();
            vm.Labore = LaborListe(labore);
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

            var benutzer = await FindeAsync(id.Value);
            if (benutzer == null) return NotFound();

            var raeume = (await _lager.GetRaeumeAsync()).Where(r => r.Benutzer == id).ToList();
            // The movement list names the user who booked, not the ID
            var bewegungen = (await _lager.BewegungenAsync())
                .Where(b => b.BenutzerNavigation.Username == benutzer.Username).ToList();

            var vm = new BenutzerDetailsViewModel
            {
                Benutzer = benutzer,
                Labor = LaborText(raeume.Where(r => r.RaumTypNavigation.Name == RaumTypen.Labor).Select(r => r.RaumNum)),
                Bewegungen = bewegungen.Take(20).ToList(),
                KannGeloeschtWerden = !bewegungen.Any() && !raeume.Any()
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
        /// Creates the user via the API. The API hashes the password (salt + pepper).
        /// </summary>
        /// <param name="vm"></param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BenutzerFormViewModel vm)
        {
            if (string.IsNullOrWhiteSpace(vm.Password))
                ModelState.AddModelError(nameof(vm.Password), "Bitte ein Passwort eingeben.");
            Validiere(vm);

            if (ModelState.IsValid)
            {
                int id;
                try
                {
                    id = await _api.RegisterAsync(vm.Username.Trim(), vm.Password!);
                }
                catch (ApiException ex) when (ex.IsUserError)
                {
                    // 409 "Benutzername ist bereits vergeben." / 400 password rules
                    ModelState.AddModelError(ex.FieldErrors.ContainsKey("Password") ? nameof(vm.Password) : nameof(vm.Username), ex.Message);
                    await FuelleListenAsync(vm);
                    return View(vm);
                }

                try
                {
                    if (vm.Rolle != Rollen.Lehrer)
                        await _api.ChangeRoleAsync(id, vm.Rolle);
                    if (vm.LaborId != null)
                        await _api.AssignTeacherAsync(vm.LaborId.Value, id);
                }
                catch (ApiException ex) when (ex.IsUserError)
                {
                    TempData["Fehler"] = $"Benutzer '{vm.Username.Trim()}' wurde erfasst, aber: {ex.Message}";
                    return RedirectToAction(nameof(Edit), new { id });
                }

                TempData["Meldung"] = $"Benutzer '{vm.Username.Trim()}' wurde erfasst.";
                return RedirectToAction(nameof(Index));
            }

            await FuelleListenAsync(vm);
            return View(vm);
        }

        /// <summary>
        /// Displays the form to edit an existing user, pre-filling the fields with the current data.
        /// </summary>
        /// <param name="id">The ID of the user to edit.</param>
        /// <returns></returns>
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var benutzer = await FindeAsync(id.Value);
            if (benutzer == null) return NotFound();

            var vm = new BenutzerFormViewModel
            {
                ID = benutzer.ID,
                Username = benutzer.Username,
                Rolle = benutzer.RolleNavigation.Name,
                LaborId = (await _lager.GetLaboreAsync()).FirstOrDefault(r => r.Benutzer == benutzer.ID)?.ID
            };
            await FuelleListenAsync(vm);
            return View(vm);
        }

        /// <summary>
        /// Saves role and laboratory. Username and password of another user can't be changed through the API.
        /// </summary>
        /// <param name="id">The ID of the user to edit.</param>
        /// <param name="vm">The view model for the user form.</param>
        /// <returns></returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, BenutzerFormViewModel vm)
        {
            if (id != vm.ID) return NotFound();

            var benutzer = await FindeAsync(id);
            if (benutzer == null) return NotFound();

            Validiere(vm);
            if (!string.IsNullOrWhiteSpace(vm.Username) && vm.Username.Trim() != benutzer.Username)
                ModelState.AddModelError(nameof(vm.Username), "Den Benutzernamen kann über die REST-API nur der Benutzer selbst ändern.");
            if (!string.IsNullOrWhiteSpace(vm.Password))
                ModelState.AddModelError(nameof(vm.Password), "Das Passwort kann über die REST-API nur der Benutzer selbst ändern.");

            if (ModelState.IsValid)
            {
                try
                {
                    var altesLabor = (await _lager.GetLaboreAsync()).FirstOrDefault(r => r.Benutzer == id);

                    // Order matters for the API rules: a LehrerIn can only get a lab after the role change,
                    // and an Administrator can't keep a lab.
                    if (vm.Rolle != benutzer.RolleNavigation.Name)
                        await _api.ChangeRoleAsync(id, vm.Rolle);
                    if (vm.LaborId != null && vm.LaborId != altesLabor?.ID)
                        await _api.AssignTeacherAsync(vm.LaborId.Value, id);

                    TempData["Meldung"] = $"Benutzer '{benutzer.Username}' wurde gespeichert.";
                    return RedirectToAction(nameof(Details), new { id });
                }
                catch (ApiException ex) when (ex.IsUserError && !ex.IsNotFound)
                {
                    ModelState.AddModelError("", ex.Message);
                }
            }

            await FuelleListenAsync(vm);
            return View(vm);
        }

        /// <summary>
        /// Displays the confirmation page for deleting a user, showing details and checking if the user can be deleted.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public Task<IActionResult> Delete(int? id) => Details(id);

        /// <summary>
        /// Deletes the user (DELETE /api/users/{id}). The API refuses with 409 if history would be lost.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var benutzer = await FindeAsync(id);
            if (benutzer == null) return NotFound();

            try
            {
                await _api.DeleteUserAsync(id);
            }
            catch (ApiException ex) when (ex.IsConflict)
            {
                TempData["Fehler"] = $"Benutzer '{benutzer.Username}' kann nicht gelöscht werden: {ex.Message}";
                return RedirectToAction(nameof(Details), new { id });
            }

            TempData["Meldung"] = $"Benutzer '{benutzer.Username}' wurde gelöscht.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Only LehrerInnen can be assigned to a laboratory (API rule).
        /// A new LehrerIn may be saved without a lab: the API only creates a lab for an existing LehrerIn
        /// (Räume -> Neues Labor), so "lab required" would make a new LehrerIn + new lab impossible.
        /// </summary>
        private void Validiere(BenutzerFormViewModel vm)
        {
            if (!string.IsNullOrEmpty(vm.Rolle) && !Rollen.Alle.Contains(vm.Rolle))
                ModelState.AddModelError(nameof(vm.Rolle), "Unbekannte Rolle.");
            if (vm.Rolle == Rollen.Admin && vm.LaborId != null)
                ModelState.AddModelError(nameof(vm.LaborId), "Ein Labor kann nur einer LehrerIn zugeordnet werden.");
        }

        /// <summary>The API has no GET /api/users/{id}, so the user is taken from the list.</summary>
        private async Task<Benutzer?> FindeAsync(int id)
        {
            var u = (await _api.GetUsersAsync()).FirstOrDefault(u => u.Id == id);
            return u == null ? null : new Benutzer
            {
                ID = u.Id,
                Username = u.Username,
                RolleNavigation = new Rolle { Name = u.RoleName }
            };
        }

        /// <summary>
        /// Fills the lists of roles and laboratories in the user form view model for use in dropdowns in the view.
        /// </summary>
        private async Task FuelleListenAsync(BenutzerFormViewModel vm)
        {
            vm.Rollen = RollenListe();
            vm.Labore = LaborListe(await _lager.GetLaboreAsync());
        }

        /// <summary>
        /// Generates a string representation of the assigned laboratories for display in the user list or details view.
        /// </summary>
        private static string LaborText(IEnumerable<int?> raumNums)
        {
            var liste = raumNums.Where(n => n != null).OrderBy(n => n).Select(n => $"Raum {n}").ToList();
            return liste.Any() ? string.Join(", ", liste) : "—";
        }

        /// <summary>The two roles. The value is the role name, which is what the API expects.</summary>
        private static IEnumerable<SelectListItem> RollenListe() =>
            Rollen.Alle.Order().Select(r => new SelectListItem(r, r)).ToList();

        private static IEnumerable<SelectListItem> LaborListe(IEnumerable<Raum> labore) =>
            labore.OrderBy(r => r.RaumNum)
                .Select(r => new SelectListItem($"Raum {r.RaumNum} · {r.BenutzerNavigation.Username}", r.ID.ToString()))
                .ToList();
    }
}
