namespace AuthenticationAPI.DTOs
{
    public class UpdateProfileDTO
    {
        public string? Email { get; set; }

        public string? FullName { get; set; }

        public string? PhoneNumber { get; set; }

        public string? AvatarUrl { get; set; }
    }
}