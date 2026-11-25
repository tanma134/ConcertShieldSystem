using System.Net.Http.Json;
using System.Text.Json;

namespace AdminAPI.Services.Risks;

public sealed record TicketRiskRevokeResult(
    bool TicketRevoked,
    string RefundStatus,
    long? RefundAmount,
    string? RefundReference,
    string? RefundError);

// Internal client for TicketAPI /api/internal/tickets/risk/* (X-Internal-Api-Key, never gateway-exposed).
public sealed record TicketInfo(int TicketId, int OwnerUserId, int EventId, string? Status, DateTime? CheckedInAt);

public interface ITicketRiskClient
{
    Task<TicketInfo?> FindTicketAsync(int ticketId, CancellationToken ct = default);

    Task BlockTicketAsync(int ticketId, int userId, string reason, int staffId,
        DateTime? expiresAt, CancellationToken ct = default);

    Task UnblockTicketAsync(int ticketId, string reason, int staffId, CancellationToken ct = default);

    Task<TicketRiskRevokeResult> RevokeAndRefundAsync(
        int ticketId, int userId, int appealId, int staffId, string reason, CancellationToken ct = default);
}

public sealed class TicketRiskClient : ITicketRiskClient
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    private readonly HttpClient _http;
    private readonly ILogger<TicketRiskClient> _logger;

    public TicketRiskClient(HttpClient http, ILogger<TicketRiskClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<TicketInfo?> FindTicketAsync(int ticketId, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(Timeout);
        using var response = await _http.GetAsync($"api/internal/tickets/risk/{ticketId}", cts.Token);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("TicketAPI lookup for ticket {TicketId} returned {Status}: {Body}",
                ticketId, (int)response.StatusCode, body);
            throw new RiskServiceException(502,
                $"TicketAPI could not verify ticket #{ticketId}. Please try again.");
        }

        var data = await response.Content.ReadFromJsonAsync<LookupResponse>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct);
        return data?.OwnerUserId is null ? null
            : new TicketInfo(data.TicketId, data.OwnerUserId.Value, data.EventId, data.Status, data.CheckedInAt);
    }

    public async Task BlockTicketAsync(int ticketId, int userId, string reason, int staffId,
        DateTime? expiresAt, CancellationToken ct = default)
    {
        var response = await PostAsync($"api/internal/tickets/risk/{ticketId}/block",
            new { userId, reason, staffId, expiresAt }, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("TicketAPI block for ticket {TicketId} returned {Status}: {Body}",
                ticketId, (int)response.StatusCode, body);
            throw new RiskServiceException(502,
                $"TicketAPI could not block ticket #{ticketId}. The decision was not saved. {body}");
        }
    }

    public async Task UnblockTicketAsync(int ticketId, string reason, int staffId, CancellationToken ct = default)
    {
        var response = await PostAsync($"api/internal/tickets/risk/{ticketId}/unblock",
            new { reason, staffId }, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("TicketAPI unblock for ticket {TicketId} returned {Status}: {Body}",
                ticketId, (int)response.StatusCode, body);
            // The customer-visible state and the ticket state must not diverge: fail the
            // caller so the decision stays Active and the admin can retry.
            throw new RiskServiceException(502,
                $"TicketAPI could not unblock ticket #{ticketId}. Please retry. {body}");
        }
    }

    public async Task<TicketRiskRevokeResult> RevokeAndRefundAsync(
        int ticketId, int userId, int appealId, int staffId, string reason, CancellationToken ct = default)
    {
        try
        {
            var response = await PostAsync($"api/internal/tickets/risk/{ticketId}/revoke-and-refund",
                new { userId, appealId, staffId, reason }, ct);

            var body = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                var data = JsonSerializer.Deserialize<RevokeResponse>(body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (data != null)
                {
                    return new TicketRiskRevokeResult(
                        data.TicketRevoked,
                        string.IsNullOrWhiteSpace(data.RefundStatus) ? "None" : data.RefundStatus,
                        data.RefundAmount,
                        data.RefundReference,
                        data.RefundError);
                }
            }

            _logger.LogError("TicketAPI revoke-and-refund for ticket {TicketId} returned {Status}: {Body}",
                ticketId, (int)response.StatusCode, body);
            return new TicketRiskRevokeResult(false, "Failed", null, null,
                $"TicketAPI returned HTTP {(int)response.StatusCode}.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "TicketAPI revoke-and-refund call failed for ticket {TicketId}", ticketId);
            return new TicketRiskRevokeResult(false, "Failed", null, null,
                "TicketAPI could not be reached. The ticket was not revoked; please retry.");
        }
    }

    private async Task<HttpResponseMessage> PostAsync(string path, object payload, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(Timeout);
        return await _http.PostAsJsonAsync(path, payload, cts.Token);
    }

    private sealed class LookupResponse
    {
        public int TicketId { get; set; }
        public int? OwnerUserId { get; set; }
        public int EventId { get; set; }
        public string? Status { get; set; }
        public DateTime? CheckedInAt { get; set; }
    }

    private sealed class RevokeResponse
    {
        public bool TicketRevoked { get; set; }
        public string? Status { get; set; }
        public string? RefundStatus { get; set; }
        public long? RefundAmount { get; set; }
        public string? RefundReference { get; set; }
        public string? RefundError { get; set; }
    }
}
