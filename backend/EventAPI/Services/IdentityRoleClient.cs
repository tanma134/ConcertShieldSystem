using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace EventAPI.Services
{
    public class IdentityRoleClient : IIdentityRoleClient
    {
        private readonly HttpClient _http;
        private readonly ILogger<IdentityRoleClient> _logger;

        public IdentityRoleClient(HttpClient http, ILogger<IdentityRoleClient> logger)
        {
            _http = http;
            _logger = logger;
        }

        public async Task<GrantRoleResult> GrantRoleAsync(
            int userId, string roleName, string? bearerToken, CancellationToken ct = default)
        {
            try
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Post, $"api/auth/users/{userId}/roles")
                {
                    Content = JsonContent.Create(new { roleName })
                };

                if (!string.IsNullOrWhiteSpace(bearerToken))
                {
                    request.Headers.Authorization =
                        new AuthenticationHeaderValue("Bearer", bearerToken);
                }

                using var response = await _http.SendAsync(request, ct);
                var body = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Granting role {Role} to user {UserId} failed: {Status} {Body}",
                        roleName, userId, (int)response.StatusCode, body);

                    return new GrantRoleResult
                    {
                        Success = false,
                        Message = $"AuthenticationAPI returned {(int)response.StatusCode}: {body}"
                    };
                }

                var roles = new List<string>();
                var message = "Role granted.";

                try
                {
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("roles", out var rolesEl) &&
                        rolesEl.ValueKind == JsonValueKind.Array)
                    {
                        roles = rolesEl.EnumerateArray()
                            .Select(r => r.GetString() ?? string.Empty)
                            .Where(r => r.Length > 0)
                            .ToList();
                    }

                    if (doc.RootElement.TryGetProperty("message", out var msgEl))
                        message = msgEl.GetString() ?? message;
                }
                catch (JsonException)
                {
                    // Non-JSON success body — the grant still worked, just report it plainly.
                }

                _logger.LogInformation(
                    "Granted role {Role} to user {UserId}. Roles now: {Roles}",
                    roleName, userId, string.Join(", ", roles));

                return new GrantRoleResult { Success = true, Message = message, Roles = roles };
            }
            catch (Exception ex)
            {
                // A role-grant failure must NOT roll back an approval that already
                // succeeded — the caller surfaces this as a warning instead.
                _logger.LogError(ex, "Could not reach AuthenticationAPI to grant role {Role} to user {UserId}",
                    roleName, userId);

                return new GrantRoleResult
                {
                    Success = false,
                    Message = "Could not reach AuthenticationAPI: " + ex.Message
                };
            }
        }
    }
}
