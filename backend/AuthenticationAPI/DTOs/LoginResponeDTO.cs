namespace AuthenticationAPI.DTOs
{
    public class LoginResponseDTO
    {
        public string AccessToken { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
        public DateTime AccessTokenExpiresAt { get; set; }
        public List<string> Roles { get; set; } = new();
        public UserInfoDTO User { get; set; } = null!;
    }

    public class UserInfoDTO
    {
        public int UserId { get; set; }
        public string Email { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public string AuthProvider { get; set; } = null!;
        public bool HasPassword { get; set; }
    }
}