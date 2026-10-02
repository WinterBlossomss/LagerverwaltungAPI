#nullable disable

using System.ComponentModel.DataAnnotations;
using Lagerverwaltung.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Lagerverwaltung.ViewModels
{
    public class RaumZeile
    {
        public int ID { get; set; }
        public int? RaumNum { get; set; }
        public string Verantwortlich { get; set; }
        public int Positionen { get; set; }
        public int Stueck { get; set; }
    }

    public class RaumListeViewModel
    {
        public string Suche { get; set; }
        public List<RaumZeile> Raeume { get; set; } = new();
    }

    public class RaumFormViewModel
    {
        public int ID { get; set; }

        [Required(ErrorMessage = "Bitte eine Raumnummer eingeben.")]
        [Range(1, 9999, ErrorMessage = "Bitte eine Zahl zwischen 1 und 9999 eingeben.")]
        [Display(Name = "Raumnummer")]
        public int? RaumNum { get; set; }

        [Required(ErrorMessage = "Bitte eine verantwortliche Person wählen.")]
        [Display(Name = "Verantwortlich")]
        public int? BenutzerId { get; set; }

        public IEnumerable<SelectListItem> Verantwortliche { get; set; } = new List<SelectListItem>();

        public bool IstNeu => ID == 0;
    }

    public class RaumDetailsViewModel
    {
        public Raum Raum { get; set; }
        public int Positionen { get; set; }
        public bool HatBewegungen { get; set; }
        public bool KannGeloeschtWerden => Positionen == 0 && !HatBewegungen;
    }
}
