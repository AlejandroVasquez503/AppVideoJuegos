using System.ComponentModel.DataAnnotations;

namespace appWeb2.Models
{
    public class RegisterViewModel
    {
        [Required]
        [StringLength(100)]
        public string nombre { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(150)]
        public string correo { get; set; }

        [Required]
        [StringLength(255)]
        [DataType(DataType.Password)]
        public string PasswordInput { get; set; }

        public string ConfirmPassword { get; set; }

        public int? idRol { get; set; }
    }
}
