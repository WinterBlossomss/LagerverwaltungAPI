#nullable disable
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Lagerverwaltung.ViewModels
{
    public class InventarZeile
    {
        public int ArtikelId { get; set; }
        public string Name { get; set; }
        public string Kategorie { get; set; }
        public string Hersteller { get; set; }
        public int? SerienNum { get; set; }
        public string Standort { get; set; }
        public int? RaumId { get; set; }
        public string Verantwortlich { get; set; }
        public int Menge { get; set; }
        public string Status { get; set; } // Verfügbar | Im Labor | Übergabe offen | Umbuchungslager
    }

    public class InventarViewModel
    {
        public string Suche { get; set; }
        public int? KategorieId { get; set; }
        public int? RaumId { get; set; }
        public int? LehrerId { get; set; }
        public string Seriennummer { get; set; } = "alle"; // alle | mit | ohne

        public List<InventarZeile> Zeilen { get; set; } = new();
        public int GesamtPositionen { get; set; }
        public IEnumerable<SelectListItem> Kategorien { get; set; } = [];
        public IEnumerable<SelectListItem> Raeume { get; set; } = [];
        public IEnumerable<SelectListItem> Lehrer { get; set; } = [];

        public bool HatFilter => !string.IsNullOrWhiteSpace(Suche) || KategorieId != null || RaumId != null || LehrerId != null || Seriennummer != "alle";
    }
}
