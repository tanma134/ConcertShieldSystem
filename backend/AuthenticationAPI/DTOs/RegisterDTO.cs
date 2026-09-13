using System.ComponentModel.DataAnnotations;

namespace AuthenticationAPI.DTOs
{
    public class RegisterDTO
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [MaxLength(256, ErrorMessage = "Email must not exceed 256 characters")]
        public string Email { get; set; } = string.Empty;
        [Required(ErrorMessage = "Password is required")]
        [StringLength(72, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 72 characters")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Full name is required")]
        [MaxLength(100, ErrorMessage = "Full name must not exceed 100 characters")]
        public string FullName { get; set; } = string.Empty;

        [MaxLength(10, ErrorMessage = "Phone number must not exceed 10 characters")]
        public string? PhoneNumber { get; set; }
    }
}
