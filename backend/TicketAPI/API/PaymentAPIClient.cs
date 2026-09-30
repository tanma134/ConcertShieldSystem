using System.Net.Http.Json;
using TicketAPI.API;
using TicketAPI.DTOs;

public class PaymentAPIClient : IPaymentAPIClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PaymentAPIClient> _logger;

    public PaymentAPIClient(
        HttpClient httpClient,
        ILogger<PaymentAPIClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<CreateVnPayPaymentResponseDto> CreateVnPayPaymentAsync(
        CreateVnPayPaymentRequestDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
            "api/VnPay/create-payment",
            request,
            cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content
                .ReadFromJsonAsync<CreateVnPayPaymentResponseDto>(
                    cancellationToken: cancellationToken);

            if (string.IsNullOrWhiteSpace(result?.PaymentUrl))
                throw new InvalidOperationException("PaymentAPI không trả về PaymentUrl.");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gọi PaymentAPI tạo VNPay URL");
            throw;
        }
    }
}