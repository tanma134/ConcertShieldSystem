using System.ComponentModel.DataAnnotations;

namespace AuthenticationAPI.DTOs
{
    public class ResetPasswordDTO
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        [MaxLength(256, ErrorMessage = "Email must not exceed 256 characters")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Reset token is required.")]
        public string ResetToken { get; set; } = string.Empty;

        [Required(ErrorMessage = "New password is required.")]
        [StringLength(72, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 72 characters")]
        public string NewPassword { get; set; } = string.Empty;

        [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
