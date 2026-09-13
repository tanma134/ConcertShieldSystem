using System.ComponentModel.DataAnnotations;

namespace AuthenticationAPI.DTOs
{
    public class LoginDTO
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [MaxLength(256, ErrorMessage = "Email must not exceed 256 characters")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [StringLength(72, MinimumLength = 1, ErrorMessage = "Invalid password")]
        public string Password { get; set; } = string.Empty;
    }
}
