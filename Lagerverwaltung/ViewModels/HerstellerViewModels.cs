#nullable disable

using System.ComponentModel.DataAnnotations;
using Lagerverwaltung.Models;

namespace Lagerverwaltung.ViewModels
{
    public class HerstellerZeile
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public int AnzahlKategorien { get; set; }
        public int AnzahlArtikel { get; set; }
    }

    public class HerstellerListeViewModel
    {
        public string Suche { get; set; }
        public List<HerstellerZeile> Hersteller { get; set; } = new();
    }

    public class HerstellerFormViewModel
    {
        public int ID { get; set; }

        [Required(ErrorMessage = "Bitte einen Namen eingeben.")]
        [StringLength(50, ErrorMessage = "Höchstens 50 Zeichen.")]
        [Display(Name = "Name")]
        public string Name { get; set; }

        // Zurück zur Seite, von der aus der Hersteller angelegt wurde (z. B. Artikel erfassen)
        public string ReturnUrl { get; set; }

        public bool IstNeu => ID == 0;
    }

    public class HerstellerDetailsViewModel
    {
        public Hersteller Hersteller { get; set; }
        public List<Kategorie> Kategorien { get; set; } = new();
        public int AnzahlArtikel { get; set; }
        public bool KannGeloeschtWerden => !Kategorien.Any();
    }
}