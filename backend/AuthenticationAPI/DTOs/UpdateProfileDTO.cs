using System.ComponentModel.DataAnnotations;

namespace AuthenticationAPI.DTOs
{
    public class UpdateProfileDTO
    {
        [Required]
        public string? Email { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 100 characters.")]
        [RegularExpression(@"^[\p{L}\p{M} ]+$", ErrorMessage = "Full name can contain letters and spaces only.")]
        public string? FullName { get; set; }

        [Required]
        [RegularExpression(@"^0[35789][0-9]{8,13}$", ErrorMessage = "Phone number must be 10 to 15 digits and start with 03, 05, 07, 08, or 09.")]
        public string? PhoneNumber { get; set; }

        public string? AvatarUrl { get; set; }
    }
}