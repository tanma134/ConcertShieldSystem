using System.Net.Http.Headers;
using System.Text.Json;

namespace ReviewAPI.Services;

public interface ICheckinClient
{
    Task<bool> HasCheckedInAsync(int eventId, int userId, CancellationToken cancellationToken);
}

public sealed class CheckinClient(IHttpClientFactory factory, IConfiguration configuration, IHttpContextAccessor accessor, ILogger<CheckinClient> logger) : ICheckinClient
{
    public async Task<bool> HasCheckedInAsync(int eventId, int userId, CancellationToken cancellationToken)
    {
        var client = factory.CreateClient("checkin");
        var path = configuration["Checkin:VerificationPath"] ?? "/api/checkin/can-review";
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{path}?eventId={eventId}&userId={userId}");
        var bearer = accessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(bearer)) request.Headers.TryAddWithoutValidation("Authorization", bearer);
        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return false;
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (document.RootElement.TryGetProperty("checkedIn", out var checkedIn)) return checkedIn.GetBoolean();
            if (document.RootElement.TryGetProperty("data", out var data) && data.TryGetProperty("checkedIn", out checkedIn)) return checkedIn.GetBoolean();
        }
        catch (Exception ex) { logger.LogWarning(ex, "Unable to verify check-in for UserId={UserId}, EventId={EventId}", userId, eventId); }
        return false;
    }
}

public interface IEventClient
{
    Task<(bool Exists, string? Name, int? OrganizerId)> GetEventAsync(int eventId, CancellationToken cancellationToken);
}

public sealed class EventClient(IHttpClientFactory factory, ILogger<EventClient> logger) : IEventClient
{
    public async Task<(bool Exists, string? Name, int? OrganizerId)> GetEventAsync(int eventId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await factory.CreateClient("event").GetAsync($"/api/events/{eventId}", cancellationToken);
            if (!response.IsSuccessStatusCode) return (false, null, null);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = document.RootElement;
            var data = root.TryGetProperty("data", out var wrapped) ? wrapped : root;
            var name = data.TryGetProperty("title", out var title) ? title.GetString() : null;
            int? organizerId = data.TryGetProperty("organizerId", out var owner) && owner.TryGetInt32(out var id) ? id : null;
            return (true, name, organizerId);
        }
        catch (Exception ex) { logger.LogWarning(ex, "Unable to load EventId={EventId}", eventId); return (false, null, null); }
    }
}

public interface IUserClient
{
    Task<string> GetUserNameAsync(int userId, CancellationToken cancellationToken);
}

public sealed class UserClient(IHttpClientFactory factory, IHttpContextAccessor accessor) : IUserClient
{
    public async Task<string> GetUserNameAsync(int userId, CancellationToken cancellationToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"/api/auth/users/{userId}");
            var bearer = accessor.HttpContext?.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrWhiteSpace(bearer)) request.Headers.TryAddWithoutValidation("Authorization", bearer);
            var response = await factory.CreateClient("identity").SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return $"User #{userId}";
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var data = document.RootElement.TryGetProperty("data", out var wrapped) ? wrapped : document.RootElement;
            if (data.TryGetProperty("fullName", out var fullName) && !string.IsNullOrWhiteSpace(fullName.GetString())) return fullName.GetString()!;
            if (data.TryGetProperty("email", out var email) && !string.IsNullOrWhiteSpace(email.GetString())) return email.GetString()!;
        }
        catch { }
        return $"User #{userId}";
    }
}
