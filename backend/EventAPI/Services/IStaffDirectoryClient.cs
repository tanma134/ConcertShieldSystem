namespace EventAPI.Services
{
    // One account returned by AuthenticationAPI's staff directory.
    public class StaffDirectoryEntry
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    // Reads the list of accounts that hold the Staff role from AuthenticationAPI.
    public interface IStaffDirectoryClient
    {
        Task<List<StaffDirectoryEntry>> SearchAsync(string? query, string? bearerToken, CancellationToken ct = default);

        Task<List<StaffDirectoryEntry>> GetByIdsAsync(IEnumerable<int> ids, string? bearerToken, CancellationToken ct = default);
    }
}
