using System.Net.Http.Headers;
using System.Text.Json;

namespace EventAPI.Services
{
    public class StaffDirectoryClient : IStaffDirectoryClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _http;
        private readonly ILogger<StaffDirectoryClient> _logger;
        private readonly IConfiguration _configuration;

        public StaffDirectoryClient(HttpClient http, ILogger<StaffDirectoryClient> logger, IConfiguration configuration)
        {
            _http = http;
            _logger = logger;
            _configuration = configuration;
        }

        // Staff accounts whose name or email contains the text (empty text lists the first page).
        public Task<List<StaffDirectoryEntry>> SearchAsync(string? query, string? bearerToken, CancellationToken ct = default)
        {
            var url = "api/users/staff?q=" + Uri.EscapeDataString(query ?? string.Empty);
            return FetchAsync(url, bearerToken, ct);
        }

        // Only the Staff accounts among the given ids. An id that is not Staff is simply missing.
        public Task<List<StaffDirectoryEntry>> GetByIdsAsync(IEnumerable<int> ids, string? bearerToken, CancellationToken ct = default)
        {
            var list = ids.Distinct().ToList();
            if (list.Count == 0)
                return Task.FromResult(new List<StaffDirectoryEntry>());

            return FetchAsync("api/users/staff?ids=" + string.Join(",", list), bearerToken, ct);
        }

        // Calls AuthenticationAPI with the caller's own token and reads the JSON array.
        // A failure is reported as an error instead of an empty list, so a broken
        // account service is never mistaken for "this user is not staff".
        private async Task<List<StaffDirectoryEntry>> FetchAsync(string url, string? bearerToken, CancellationToken ct)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);

                // EventAPI has already verified that the caller owns the event.
                // Use a service credential as well, so staff lookup does not depend on
                // forwarding a browser JWT between microservices.
                var internalKey = _configuration["Services:InternalApiKey"];
                if (!string.IsNullOrWhiteSpace(internalKey))
                    request.Headers.TryAddWithoutValidation("X-Internal-Api-Key", internalKey);
                if (!string.IsNullOrWhiteSpace(bearerToken))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

                using var response = await _http.SendAsync(request, ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Staff directory returned {Status} for {Url}", (int)response.StatusCode, url);
                    throw new InvalidOperationException("The account service could not list staff members right now.");
                }

                var body = await response.Content.ReadAsStringAsync(ct);
                return JsonSerializer.Deserialize<List<StaffDirectoryEntry>>(body, JsonOptions) ?? new List<StaffDirectoryEntry>();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Cannot reach the staff directory at {Url}", url);
                throw new InvalidOperationException("The account service is unreachable. Please try again later.");
            }
        }
    }
}
