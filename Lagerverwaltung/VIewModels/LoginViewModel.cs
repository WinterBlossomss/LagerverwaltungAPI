#nullable disable
using System.ComponentModel.DataAnnotations;

namespace Lagerverwaltung.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Bitte geben Sie Ihren Benutzernamen ein.")]
        [DataType(DataType.Text)]
        [Display(Name = "Benutzername")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Bitte geben Sie Ihr Passwort ein.")]
        [DataType(DataType.Password)]
        [Display(Name = "Passwort")]
        public string Password { get; set; }

        [Display(Name = "Angemeldet bleiben")]
        public bool RememberMe { get; set; }
    }
}