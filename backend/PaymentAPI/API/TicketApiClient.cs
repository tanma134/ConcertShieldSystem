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
            try
            {
                using var response = await _httpClient.PutAsJsonAsync(
                    $"api/Orders/{orderId}/confirm-payment",
                    request);

                if (response.IsSuccessStatusCode)
                    return true;

                var body = await response.Content.ReadAsStringAsync();

                _logger.LogError(
                    "TicketAPI không xác nhận được Order {OrderId}. Status: {StatusCode}, Body: {Body}",
                    orderId,
                    response.StatusCode,
                    body);

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Lỗi gọi TicketAPI xác nhận thanh toán cho Order {OrderId}",
                    orderId);

                return false;
            }
        }
    }
}
