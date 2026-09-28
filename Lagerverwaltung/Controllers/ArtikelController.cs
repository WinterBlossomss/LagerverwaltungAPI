
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Lagerverwaltung.Models;

public class ArtikelController : Controller
{
    private readonly LagerverwaltungssystemContext _context;

    public ArtikelController(LagerverwaltungssystemContext context)
    {
        _context = context;
    }

    // GET: ARTIKELS
    public async Task<IActionResult> Index()    
    {
        return View(await _context.Artikels.ToListAsync());
    }

    // GET: ARTIKELS/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var artikel = await _context.Artikels
            .FirstOrDefaultAsync(m => m.ID == id);
        if (artikel == null)
        {
            return NotFound();
        }

        return View(artikel);
    }

    // GET: ARTIKELS/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: ARTIKELS/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("ID,Name,SerienNum,Kategorie,KategorieNavigation,RaumArtikels")] Artikel artikel)
    {
        if (ModelState.IsValid)
        {
            _context.Add(artikel);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(artikel);
    }

    // GET: ARTIKELS/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var artikel = await _context.Artikels.FindAsync(id);
        if (artikel == null)
        {
            return NotFound();
        }
        return View(artikel);
    }

    // POST: ARTIKELS/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("ID,Name,SerienNum,Kategorie,KategorieNavigation,RaumArtikels")] Artikel artikel)
    {
        if (id != artikel.ID)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(artikel);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ArtikelExists(artikel.ID))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return RedirectToAction(nameof(Index));
        }
        return View(artikel);
    }

    // GET: ARTIKELS/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var artikel = await _context.Artikels
            .FirstOrDefaultAsync(m => m.ID == id);
        if (artikel == null)
        {
            return NotFound();
        }

        return View(artikel);
    }

    // POST: ARTIKELS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var artikel = await _context.Artikels.FindAsync(id);
        if (artikel != null)
        {
            _context.Artikels.Remove(artikel);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool ArtikelExists(int? id)
    {
        return _context.Artikels.Any(e => e.ID == id);
    }
}
