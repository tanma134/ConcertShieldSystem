namespace AuthenticationAPI.DTOs
{
    public class UpdateUserDTO
    {
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? AvatarUrl { get; set; }
        public string? Email { get; set; }
        public List<int>? RoleIds { get; set; }
        public List<string>? RoleNames { get; set; }
        public bool? IsActive { get; set; }
    }
}
