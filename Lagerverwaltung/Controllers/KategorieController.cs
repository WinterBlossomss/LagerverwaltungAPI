
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Lagerverwaltung.Models;

public class KategorieController : Controller
{
    private readonly LagerverwaltungssystemContext _context;

    public KategorieController(LagerverwaltungssystemContext context)
    {
        _context = context;
    }

    // GET: KATEGORIES
    public async Task<IActionResult> Index()    
    {
        return View(await _context.Kategories.ToListAsync());
    }

    // GET: KATEGORIES/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var kategorie = await _context.Kategories
            .FirstOrDefaultAsync(m => m.ID == id);
        if (kategorie == null)
        {
            return NotFound();
        }

        return View(kategorie);
    }

    // GET: KATEGORIES/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: KATEGORIES/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("ID,Name,Hersteller,ModelNum,Artikels,HerstellerNavigation")] Kategorie kategorie)
    {
        if (ModelState.IsValid)
        {
            _context.Add(kategorie);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(kategorie);
    }

    // GET: KATEGORIES/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var kategorie = await _context.Kategories.FindAsync(id);
        if (kategorie == null)
        {
            return NotFound();
        }
        return View(kategorie);
    }

    // POST: KATEGORIES/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("ID,Name,Hersteller,ModelNum,Artikels,HerstellerNavigation")] Kategorie kategorie)
    {
        if (id != kategorie.ID)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(kategorie);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!KategorieExists(kategorie.ID))
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
        return View(kategorie);
    }

    // GET: KATEGORIES/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var kategorie = await _context.Kategories
            .FirstOrDefaultAsync(m => m.ID == id);
        if (kategorie == null)
        {
            return NotFound();
        }

        return View(kategorie);
    }

    // POST: KATEGORIES/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var kategorie = await _context.Kategories.FindAsync(id);
        if (kategorie != null)
        {
            _context.Kategories.Remove(kategorie);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool KategorieExists(int? id)
    {
        return _context.Kategories.Any(e => e.ID == id);
    }
}
