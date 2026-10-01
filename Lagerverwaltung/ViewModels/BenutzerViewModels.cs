using Lagerverwaltung.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace Lagerverwaltung.ViewModels
{
    public class BenutzerListeViewModel
    {
        public string Suche { get; set; }
        public string Rolle { get; set; }
        public string Labor { get; set; }
        public List<BenutzerZeile> Benutzer { get; set; } = new();
        public IEnumerable<SelectListItem> Rollen { get; set; } = [];
        public IEnumerable<SelectListItem> Labore { get; set; } = [];
    }

    public class BenutzerZeile
    {
        public int ID { get; set; }
        public string Username { get; set; }
        public string Rolle { get; set; }
        public string Labor { get; set; }
    }

    public class BenutzerFormViewModel
    {
        public int ID { get; set; }

        [Required(ErrorMessage = "Bitte einen Benutzernamen eingeben.")]
        [StringLength(20)]
        [Display(Name = "Benutzername")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Bitte eine Rolle wählen.")]
        [Display(Name = "Rolle")]
        public string Rolle { get; set; }

        [Display(Name = "Labor")]
        public int? LaborId { get; set; }

        [StringLength(100, MinimumLength = 3, ErrorMessage = "Mindestens 3 Zeichen.")]
        [DataType(DataType.Password)]
        [Display(Name = "Passwort")]
        public string Password { get; set; }

        public bool IstNeu => ID == 0;

        public IEnumerable<SelectListItem> Rollen { get; set; } = [];
        public IEnumerable<SelectListItem> Labore { get; set; } = [];
    }

    public class BenutzerDetailsViewModel
    {
        public Benutzer Benutzer { get; set; }
        public string Labor { get; set; }
        public List<Lagerbewegungen> Bewegungen { get; set; } = new();
        public bool KannGeloeschtWerden { get; set; }
    }
}