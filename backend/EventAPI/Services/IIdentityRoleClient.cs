namespace EventAPI.Services
{
    public class GrantRoleResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new();
    }

    /// <summary>
    /// Talks to AuthenticationAPI so EventAPI can grant the "Organizer" role to a
    /// concert owner the moment their concert is approved. The grant is additive —
    /// the user keeps "Customer" and ends up with both roles.
    /// </summary>
    public interface IIdentityRoleClient
    {
        /// <param name="bearerToken">
        /// The approving Admin's raw JWT, forwarded so AuthenticationAPI's
        /// "CanManageUsers" policy authorizes the call.
        /// </param>
        Task<GrantRoleResult> GrantRoleAsync(int userId, string roleName, string? bearerToken, CancellationToken ct = default);
    }
}
