namespace TicketAPI.API;
public record EventEligibility(bool CanSell, string Reason, string Status, DateTime StartsAt, DateTime EndsAt, int ScheduleVersion);
public class EventEligibilityClient(HttpClient http)
{
    // Fail closed khi EventAPI không thể xác nhận điều kiện bán; không dùng dữ liệu cache cũ để cho mua.
    public async Task<EventEligibility> GetAsync(int eventId, CancellationToken ct = default)
    {
        if (eventId <= 0) throw new ArgumentException("Invalid event ID.");
        using var response = await http.GetAsync($"api/internal/events/{eventId}/eligibility", ct);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Cannot verify the concert status. Please retry.");
        return await response.Content.ReadFromJsonAsync<EventEligibility>(cancellationToken: ct) ?? throw new InvalidOperationException("Invalid concert eligibility response.");
    }
    public async Task EnsureSalesAsync(int eventId, CancellationToken ct = default)
    { var ev = await GetAsync(eventId, ct); if (!ev.CanSell) throw new InvalidOperationException(ev.Reason); }
}
