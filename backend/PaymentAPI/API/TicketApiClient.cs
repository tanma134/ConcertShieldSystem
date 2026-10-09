using PaymentAPI.DTOs;

namespace PaymentAPI.API
{
    public class TicketApiClient : ITicketApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<TicketApiClient> _logger;

        public TicketApiClient(
            HttpClient httpClient,
            ILogger<TicketApiClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<bool> ConfirmOrderPaymentAsync(
            int orderId,
            ConfirmOrderPaymentRequestDTO request)
        {
            var (ok, _) = await ConfirmOrderPaymentWithReasonAsync(orderId, request);
            return ok;
        }

        public async Task<(bool Ok, string? Error)> ConfirmOrderPaymentWithReasonAsync(
            int orderId,
            ConfirmOrderPaymentRequestDTO request)
        {
            try
            {
                using var response = await _httpClient.PutAsJsonAsync(
                    $"api/Orders/{orderId}/confirm-payment",
                    request);

                if (response.IsSuccessStatusCode)
                    return (true, null);

                var body = await response.Content.ReadAsStringAsync();

                _logger.LogError(
                    "TicketAPI không xác nhận được Order {OrderId}. Status: {StatusCode}, Body: {Body}",
                    orderId,
                    response.StatusCode,
                    body);

                return (false, ExtractReason((int)response.StatusCode, body));
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Lỗi gọi TicketAPI xác nhận thanh toán cho Order {OrderId}",
                    orderId);

                return (false, $"Cannot reach TicketAPI ({ex.GetType().Name}: {ex.Message}).");
            }
        }

        // Prefers the "message" field TicketAPI returns; falls back to the start of the raw body.
        private static string ExtractReason(int status, string body)
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("message", out var m) && m.ValueKind == System.Text.Json.JsonValueKind.String)
                    return $"TicketAPI {status}: {m.GetString()}";
            }
            catch (System.Text.Json.JsonException) { }

            var text = string.IsNullOrWhiteSpace(body) ? "no details" : body.Trim();
            if (text.Length > 300) text = text[..300] + "...";
            return $"TicketAPI {status}: {text}";
        }
    }
}
