#nullable disable

using System.ComponentModel.DataAnnotations;
using Lagerverwaltung.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Lagerverwaltung.ViewModels
{
    public class KategorieZeile
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public int? ModelNum { get; set; }
        public int AnzahlArtikel { get; set; }
    }

    public class KategorieListeViewModel
    {
        public string Suche { get; set; }
        public List<KategorieZeile> Kategorien { get; set; } = new();
    }

    public class KategorieFormViewModel
    {
        public int ID { get; set; }

        [Required(ErrorMessage = "Bitte einen Namen eingeben.")]
        [StringLength(100, ErrorMessage = "Höchstens 100 Zeichen.")]
        [Display(Name = "Name")]
        public string Name { get; set; }

        [Display(Name = "Modellnummer")]
        public int? ModelNum { get; set; }

        // Zurück zur Seite, von der aus die Kategorie angelegt wurde (z. B. Artikel erfassen)
        public string ReturnUrl { get; set; }

        public bool IstNeu => ID == 0;
    }

    public class KategorieDetailsViewModel
    {
        public Kategorie Kategorie { get; set; }
        public List<Artikel> Artikel { get; set; } = new();
        public bool KannGeloeschtWerden => !Artikel.Any();
    }
}