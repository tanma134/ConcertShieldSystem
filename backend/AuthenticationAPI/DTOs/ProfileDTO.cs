namespace AuthenticationAPI.DTOs
{
    public class ProfileDTO
    {
        public int UserId { get; set; }

        public string Email { get; set; } = null!;

        public string FullName { get; set; } = null!;

        public string? PhoneNumber { get; set; }

        public string? RoleName { get; set; }

        public string? AvatarUrl { get; set; }

        public bool IsVerified { get; set; }

        public string? EkycStatus { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}