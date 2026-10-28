using System.ComponentModel.DataAnnotations;

namespace AuthenticationAPI.DTOs
{
    public class ForgotPasswordDTO
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        [MaxLength(256, ErrorMessage = "Email must not exceed 256 characters")]
        public string Email { get; set; } = string.Empty;
    }
}
