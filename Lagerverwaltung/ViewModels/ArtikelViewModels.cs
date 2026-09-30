#nullable disable
using System.ComponentModel.DataAnnotations;
using Lagerverwaltung.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Lagerverwaltung.ViewModels
{
    public class ArtikelListeViewModel
    {
        public string Suche { get; set; }
        public int? KategorieId { get; set; }
        public List<Artikel> Artikel { get; set; } = new();
        public Dictionary<int, int> Gesamtbestand { get; set; } = new();
        public IEnumerable<SelectListItem> Kategorien { get; set; } = [];
    }

    public class ArtikelFormViewModel
    {
        public int ID { get; set; }

        [Required(ErrorMessage = "Bitte eine Bezeichnung eingeben.")]
        [StringLength(100)]
        [Display(Name = "Bezeichnung")]
        public string Name { get; set; }

        [Display(Name = "Seriennummer")]
        public int? SerienNum { get; set; }

        [Required(ErrorMessage = "Bitte eine Kategorie wählen.")]
        [Display(Name = "Kategorie")]
        public int? KategorieId { get; set; }

        [Range(1, 100000, ErrorMessage = "Mindestens 1.")]
        [Display(Name = "Anfangsbestand im Hauptlager")]
        public int Anfangsbestand { get; set; } = 1;

        public bool IstNeu => ID == 0;
        public IEnumerable<SelectListItem> Kategorien { get; set; } = [];
    }

    public class ArtikelDetailsViewModel
    {
        public Artikel Artikel { get; set; }
        public List<RaumArtikel> Standorte { get; set; } = new();
        public List<Lagerbewegungen> Bewegungen { get; set; } = new();
        public bool KannGeloeschtWerden { get; set; }
    }
}