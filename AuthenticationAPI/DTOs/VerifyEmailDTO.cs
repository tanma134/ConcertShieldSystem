using System.ComponentModel.DataAnnotations;

namespace AuthenticationAPI.DTOs
{
    public class VerifyEmailDTO
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [MaxLength(256, ErrorMessage = "Email must not exceed 256 characters")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "OTP is required")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP must be 6 digits")]
        public string OTP { get; set; } = string.Empty;
    }
}
