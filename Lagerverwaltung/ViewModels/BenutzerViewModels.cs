using Lagerverwaltung.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace Lagerverwaltung.ViewModels
{
    public class BenutzerListeViewModel
    {
        public string Suche { get; set; }
        public List<Benutzer> Benutzer { get; set; } = new();
        public string Labor { get; set; }
        public string Rolle { get; set; }
    }

    public class BenutzerFormViewModel
    {
        public int ID { get; set; }

        [Required(ErrorMessage = "Bitte einen Benutzernamen eingeben.")]
        [StringLength(50)]
        [Display(Name = "Benutzername")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Bitte eine Rolle wählen.")]
        [Display(Name = "Rolle")]
        public string Rolle { get; set; }

        [Required(ErrorMessage = "Bitte ein Labor wählen.")]
        [Display(Name = "Labor")]
        public int? LaborId { get; set; }

        public bool IstNeu => ID == 0;

        public IEnumerable<SelectListItem> Rollen { get; set; } = [];
        public IEnumerable<SelectListItem> Labore { get; set; } = [];
    }

    public class BenutzerDetailsViewModel
    {
        public Benutzer Benutzer { get; set; }
        public List<Lagerbewegungen> Bewegungen { get; set; } = new();
        public bool KannGeloeschtWerden { get; set; }
    }
}