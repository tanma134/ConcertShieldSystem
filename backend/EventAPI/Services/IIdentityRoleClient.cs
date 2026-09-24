namespace EventAPI.Services
{
    public class GrantRoleResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new();
    }

    public interface IIdentityRoleClient
    {
        Task<GrantRoleResult> GrantRoleAsync(
            int userId,
            string roleName,
            string? bearerToken,
            CancellationToken ct = default);
    }
}
