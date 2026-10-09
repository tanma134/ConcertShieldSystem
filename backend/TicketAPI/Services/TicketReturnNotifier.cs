using System.Net.Http.Json;

namespace TicketAPI.Services;

// Tells a customer what happened to their return request (best effort).
// A failed notification must never undo a decision, so errors are only logged.
// The idempotency key makes a repeated call for the same request and outcome a no-op in NotificationAPI.
public class TicketReturnNotifier
{
    private readonly IHttpClientFactory _factory;
    private readonly ILogger<TicketReturnNotifier> _logger;

    public TicketReturnNotifier(IHttpClientFactory factory, ILogger<TicketReturnNotifier> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task NotifyCustomerAsync(int customerUserId, int requestId, string outcome, string title, string message)
    {
        try
        {
            var client = _factory.CreateClient("ChangeNotification");
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/internal/notifications");
            request.Headers.Add("Idempotency-Key", $"return-{requestId}-{outcome}");
            request.Content = JsonContent.Create(new
            {
                userId = customerUserId,
                title,
                message,
                category = "ticket_return",
                targetUrl = "/my-tickets"
            });
            using var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("Return notification for request {Id} failed with HTTP {Status}", requestId, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Return notification for request {Id} could not be sent", requestId);
        }
    }
}
