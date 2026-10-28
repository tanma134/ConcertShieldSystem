using System.Net.Http.Headers;
using System.Net.Http.Json;
using TicketAPI.API;
using TicketAPI.DTOs;

public class PaymentAPIClient : IPaymentAPIClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PaymentAPIClient> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PaymentAPIClient(
        HttpClient httpClient,
        ILogger<PaymentAPIClient> logger, IHttpContextAccessor httpContextAccessor)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
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

    public async Task<RefundOutcome> RefundAsync(int returnRequestId, int orderId, long amount, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                "api/internal/refunds",
                new { returnRequestId, orderId, amount },
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("PaymentAPI refund returned HTTP {Status} for return request {Id}", (int)response.StatusCode, returnRequestId);
                return new RefundOutcome(false, null, $"Payment service returned HTTP {(int)response.StatusCode}.");
            }

            var result = await response.Content.ReadFromJsonAsync<RefundResponse>(cancellationToken: cancellationToken);
            return result?.Status == "Refunded"
                ? new RefundOutcome(true, result.Reference, null)
                : new RefundOutcome(false, null, result?.Error ?? "Refund was not completed.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "PaymentAPI refund call failed for return request {Id}", returnRequestId);
            return new RefundOutcome(false, null, "Payment service is unavailable. Please retry later.");
        }
    }

    private sealed class RefundResponse
    {
        public string? Status { get; set; }
        public string? Reference { get; set; }
        public string? Error { get; set; }
    }

    public async Task<ApplyVoucherResponseDto> ApplyVoucherAsync(
        ApplyVoucherRequestDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                "api/v1/vouchers/apply")
            {
                Content = JsonContent.Create(request)
            };

            var bearerToken = _httpContextAccessor.HttpContext?
                .Request.Headers.Authorization.ToString();

            if (!string.IsNullOrWhiteSpace(bearerToken) &&
                AuthenticationHeaderValue.TryParse(bearerToken, out var authorization))
            {
                httpRequest.Headers.Authorization = authorization;
            }

            using var response = await _httpClient.SendAsync(
                httpRequest,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content
                .ReadFromJsonAsync<ApplyVoucherResponseDto>(
                    cancellationToken: cancellationToken);

            return result ?? throw new InvalidOperationException("PaymentAPI không trả về kết quả áp dụng voucher.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gọi PaymentAPI áp dụng voucher");
            throw;
        }
    }
}