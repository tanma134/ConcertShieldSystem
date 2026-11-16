using System.Net.Http.Json;
using System.Text.Json;

namespace AdminAPI.Services.Risks;

/// <summary>Minimal account facts returned by AuthenticationAPI for a user id.</summary>
public sealed record AccountInfo(int UserId, bool IsActive, IReadOnlyList<string> Roles);

/// <summary>
/// Verifies that a fraud-case assignee is an active Staff account, and looks up
/// account existence before a manual block is created.
/// AuthenticationAPI exposes /api/users/staff?ids=... for trusted internal callers.
/// </summary>
public interface IStaffDirectoryClient
{
    Task<bool> IsActiveStaffAsync(int userId, CancellationToken ct = default);

    /// <summary>Returns null when the user does not exist.</summary>
    Task<AccountInfo?> FindAccountAsync(int userId, CancellationToken ct = default);
}

public sealed class StaffDirectoryClient : IStaffDirectoryClient
{
    private readonly HttpClient _http;
    private readonly ILogger<StaffDirectoryClient> _logger;

    public StaffDirectoryClient(HttpClient http, ILogger<StaffDirectoryClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<bool> IsActiveStaffAsync(int userId, CancellationToken ct = default)
    {
        try
        {
            using var response = await _http.GetAsync($"api/users/staff?ids={userId}", ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Staff directory lookup returned {Status} for user {UserId}",
                    (int)response.StatusCode, userId);
                throw new RiskServiceException(503,
                    "Could not verify the assignee with AuthenticationAPI. Please try again.");
            }

            var staff = await response.Content.ReadFromJsonAsync<List<StaffUser>>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct);
            return staff?.Any(u => u.UserId == userId) == true;
        }
        catch (RiskServiceException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Staff directory lookup failed for user {UserId}", userId);
            throw new RiskServiceException(503,
                "Could not verify the assignee with AuthenticationAPI. Please try again.");
        }
    }

    public async Task<AccountInfo?> FindAccountAsync(int userId, CancellationToken ct = default)
    {
        using var response = await GetAsync($"api/users/staff/accounts?ids={userId}", userId, ct);
        var accounts = await response.Content.ReadFromJsonAsync<List<AccountResponse>>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct);

        var hit = accounts?.FirstOrDefault(a => a.UserId == userId);
        return hit == null ? null : new AccountInfo(hit.UserId, hit.IsActive, hit.Roles ?? new List<string>());
    }

    private async Task<HttpResponseMessage> GetAsync(string path, int userId, CancellationToken ct)
    {
        try
        {
            var response = await _http.GetAsync(path, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Account lookup returned {Status} for user {UserId}",
                    (int)response.StatusCode, userId);
                throw new RiskServiceException(503,
                    "Could not verify the target account with AuthenticationAPI. Please try again.");
            }
            return response;
        }
        catch (RiskServiceException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Account lookup failed for user {UserId}", userId);
            throw new RiskServiceException(503,
                "Could not verify the target account with AuthenticationAPI. Please try again.");
        }
    }

    private sealed class StaffUser
    {
        public int UserId { get; set; }
    }

    private sealed class AccountResponse
    {
        public int UserId { get; set; }
        public bool IsActive { get; set; }
        public List<string>? Roles { get; set; }
    }
}
