namespace AuthenticationAPI.DTOs
{
    public class RefreshResponseDTO
    {
        public string AccessToken { get; set; } = string.Empty;
        public DateTime AccessTokenExpiresAt { get; set; }
        public string RefreshToken { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new();
    }
}
