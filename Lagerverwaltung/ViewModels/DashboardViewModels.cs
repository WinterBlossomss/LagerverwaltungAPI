#nullable disable
using Lagerverwaltung.Models;

namespace Lagerverwaltung.ViewModels
{
    /// <summary>
    /// Represents an overview of a room, including its ID, name, type, responsible person, number of positions, number of items,
    /// and counts of open handovers and returns.
    /// </summary>
    public class RaumUebersicht
    {
        public int RaumId { get; set; }
        public string Name { get; set; }
        public string Typ { get; set; }
        public string Verantwortlich { get; set; }
        public int Positionen { get; set; }
        public int Stueck { get; set; }
        public int OffeneUebergaben { get; set; }
        public int OffeneRueckgaben { get; set; }
    }

    /// <summary>
    /// Represents a summary of the dashboard for administrators, including counts of items in the main warehouse, number of positions,
    /// number of laboratories, number of teachers, and lists of open handovers, open returns, and recent movements.
    /// </summary>
    public class DashboardAdminViewModel
    {
        public int StueckHauptlager { get; set; }
        public int PositionenHauptlager { get; set; }
        public int AnzahlLabore { get; set; }
        public int AnzahlLehrer { get; set; }
        public List<Lagerbewegungen> OffeneAusgaben { get; set; } = new();
        public List<Lagerbewegungen> OffeneRueckgaben { get; set; } = new();
        public List<Lagerbewegungen> LetzteBewegungen { get; set; } = new();
    }

    /// <summary>
    /// Represents a summary of the dashboard for teachers, including their name, lists of open handovers, laboratories, number of returns in progress,
    /// and counts of items and positions in laboratories.
    /// </summary>
    public class DashboardLehrerViewModel
    {
        public string Name { get; set; }
        public List<Lagerbewegungen> OffeneUebergaben { get; set; } = new();
        public List<RaumUebersicht> Labore { get; set; } = new();
        public int RueckgabenUnterwegs { get; set; }
        public int StueckInLaboren => Labore.Sum(l => l.Stueck);
        public int PositionenInLaboren => Labore.Sum(l => l.Positionen);
    }

    /// <summary>
    /// Represents a view model for displaying warehouses and laboratories, including lists of warehouse and laboratory tiles.
    /// </summary>
    public class LagerViewModel
    {
        public List<RaumUebersicht> Lager { get; set; } = new();
        public List<RaumUebersicht> Labore { get; set; } = new();
    }

    /// <summary>
    /// Represents a view model for managing laboratory operations, including the selected room, a list of available rooms,
    /// search term, stock information, handover dates, open handovers, permissions for confirming and returning items, and counts of positions, items,
    /// and items with serial numbers.
    /// </summary>
    public class LaborViewModel
    {
        public Raum Raum { get; set; }
        public List<Raum> Auswahl { get; set; } = new();
        public string Suche { get; set; }
        public List<RaumArtikel> Bestand { get; set; } = new();
        public Dictionary<int, DateOnly> UebernommenAm { get; set; } = new();
        public List<Lagerbewegungen> OffeneUebergaben { get; set; } = new();
        public bool DarfBestaetigen { get; set; }
        public bool DarfZurueckgeben { get; set; }
        public int Positionen { get; set; }
        public int Stueck { get; set; }
        public int MitSeriennummer { get; set; }
    }
}