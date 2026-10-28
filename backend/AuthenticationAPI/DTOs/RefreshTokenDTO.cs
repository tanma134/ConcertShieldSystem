using System.ComponentModel.DataAnnotations;

namespace AuthenticationAPI.DTOs
{
    public class RefreshTokenDTO
    {
        [Required(ErrorMessage = "Refresh token is required")]
        public string RefreshToken { get; set; } = string.Empty;
    }
   
}
