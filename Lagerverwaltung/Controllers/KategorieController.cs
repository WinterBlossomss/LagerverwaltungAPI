//using Lagerverwaltung.Models;
//using Lagerverwaltung.Services;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;

//[Authorize(Roles = Rollen.Admin)]
//public class KategorieController : Controller
//{
//    private readonly LagerverwaltungssystemContext _db;
//    private readonly LagerService _lager;

//    public LaborController(LagerverwaltungssystemContext db, LagerService lager)
//    {
//        _db = db;
//        _lager = lager;
//    }

//    public async Task<IActionResult> Index()
//    {
//        var kategorien = await _db.Kategories.ToListAsync();
//        return View(kategorien);
//    }
//}