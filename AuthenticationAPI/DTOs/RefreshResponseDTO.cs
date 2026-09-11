namespace AuthenticationAPI.DTOs
{
    // Ensure there is only one definition of RefreshResponseDTO in the namespace
    public class RefreshResponseDTO
    {
        public string AccessToken { get; set; } = string.Empty;
        public DateTime AccessTokenExpiresAt { get; set; }
    }
}
