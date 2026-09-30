#nullable disable
using System.ComponentModel.DataAnnotations;
using Lagerverwaltung.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Lagerverwaltung.ViewModels
{
    /// <summary>Eine auswählbare Zeile bei Ausgabe und Rückgabe.</summary>
    public class AuswahlPosition
    {
        public int ArtikelId { get; set; }
        public string Name { get; set; }
        public string Kategorie { get; set; }
        public int? SerienNum { get; set; }
        public int Verfuegbar { get; set; }
        public bool Ausgewaehlt { get; set; }

        [Range(1, 100000, ErrorMessage = "Mindestens 1.")]
        public int Menge { get; set; } = 1;
    }

    public class AusgabeViewModel
    {
        [Required(ErrorMessage = "Bitte ein Labor wählen.")]
        [Display(Name = "Labor")]
        public int? ZielRaumId { get; set; }
        public string Suche { get; set; }
        public List<AuswahlPosition> Positionen { get; set; } = new();
        public IEnumerable<SelectListItem> Labore { get; set; } = [];
        public Dictionary<int, string> Verantwortliche { get; set; } = new();
    }

    public class UebergabenViewModel
    {
        public List<Lagerbewegungen> Offen { get; set; } = new();
        public List<Lagerbewegungen> Bestaetigt { get; set; } = new();
    }

    public class RueckgabeViewModel
    {
        public int RaumId { get; set; }
        public string RaumName { get; set; }
        public List<Raum> Labore { get; set; } = new();
        public List<AuswahlPosition> Positionen { get; set; } = new();
    }

    public class UmbuchungBestand
    {
        public int ArtikelId { get; set; }
        public string Name { get; set; }
        public int? SerienNum { get; set; }
        public int Menge { get; set; }
        public int Einbuchbar { get; set; }
    }

    public class UmbuchungViewModel
    {
        public List<Lagerbewegungen> OffeneRueckgaben { get; set; } = new();
        public List<UmbuchungBestand> Bestand { get; set; } = new();
        public List<Lagerbewegungen> Eingebucht { get; set; } = new();
    }

    public class BewegungenViewModel
    {
        public string Art { get; set; }
        public string Status { get; set; }     // offen | abgeschlossen
        public int? RaumId { get; set; }
        public string Suche { get; set; }
        public List<Lagerbewegungen> Bewegungen { get; set; } = new();
        public IEnumerable<SelectListItem> Arten { get; set; } = [];
        public IEnumerable<SelectListItem> Raeume { get; set; } = [];
    }
}