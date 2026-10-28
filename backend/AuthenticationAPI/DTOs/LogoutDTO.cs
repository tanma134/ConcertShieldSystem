using System.ComponentModel.DataAnnotations;

namespace AuthenticationAPI.DTOs
{
    public class LogoutDTO
    {
        [Required(ErrorMessage = "Refresh token is required")]
        public string RefreshToken { get; set; } = string.Empty;
    }
}
