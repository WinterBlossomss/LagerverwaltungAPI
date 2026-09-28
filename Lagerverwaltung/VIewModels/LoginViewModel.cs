using System.ComponentModel.DataAnnotations;

namespace Lagerverwaltung.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Please enter your username.")]
        [DataType(DataType.Text)]
        public string UserName { get; set; }
        [Required(ErrorMessage = "Please enter your password.")]
        [DataType(DataType.Password)]
        public string Password { get; set; }
        [Display(Name = "Remember Me?")]
        public bool RememberMe { get; set; }

    }
}
