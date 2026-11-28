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

    public async Task<CreateVnPayPaymentResponseDto> CreateVnPayPaymentAsync(CreateVnPayPaymentRequestDto request)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
            "api/VnPay/create-payment",
            request);

            response.EnsureSuccessStatusCode();

            var result = await response.Content
                .ReadFromJsonAsync<CreateVnPayPaymentResponseDto>();

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

    public async Task<RefundOutcome> RefundAsync(int returnRequestId, int orderId, long amount, string? customerName = null, int? eventId = null, string? eventName = null, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                "api/internal/refunds",
                new { returnRequestId, orderId, amount, customerName, eventId, eventName },
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

    public async Task<RefundOutcome> RecordManualRefundAsync(
        int returnRequestId,
        int orderId,
        long amount,
        string reference,
        int processedByUserId,
        string? customerName = null,
        int? eventId = null,
        string? eventName = null,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                "api/internal/refunds/manual",
                new { returnRequestId, orderId, amount, reference, processedByUserId, customerName, eventId, eventName, reason,
                    isSimulation = reference.StartsWith("SIM-", StringComparison.OrdinalIgnoreCase) },
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("PaymentAPI manual refund record returned HTTP {Status} for return request {Id}", (int)response.StatusCode, returnRequestId);
                return new RefundOutcome(false, null, $"Payment service returned HTTP {(int)response.StatusCode}.");
            }

            var result = await response.Content.ReadFromJsonAsync<RefundResponse>(cancellationToken: cancellationToken);
            return result?.Status == "Refunded"
                ? new RefundOutcome(true, result.Reference, null)
                : new RefundOutcome(false, null, result?.Error ?? "Manual refund record failed.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "PaymentAPI manual refund record call failed for return request {Id}", returnRequestId);
            return new RefundOutcome(false, null, "Payment service is unavailable.");
        }
    }

    private sealed class RefundResponse
    {
        public string? Status { get; set; }
        public string? Reference { get; set; }
        public string? Error { get; set; }
    }

    public async Task<ApplyVoucherResponseDto> ApplyVoucherAsync(ApplyVoucherRequestDto request)
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
                httpRequest);

            response.EnsureSuccessStatusCode();

            var result = await response.Content
                .ReadFromJsonAsync<ApplyVoucherResponseDto>();

            return result ?? throw new InvalidOperationException("PaymentAPI không trả về kết quả áp dụng voucher.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gọi PaymentAPI áp dụng voucher");
            throw;
        }
    }

    public async Task<int> CancelPendingPaymentsAsync(int orderId)
    {
        if (orderId <= 0)
            throw new ArgumentOutOfRangeException(nameof(orderId));

        try
        {
            using var response = await _httpClient.PostAsync(
                $"api/internal/payments/orders/{orderId}/cancel-pending",
                content: null);

            response.EnsureSuccessStatusCode();

            var result = await response.Content
                .ReadFromJsonAsync<CancelPendingPaymentsResponse>();

            return result?.CancelledCount
                ?? throw new InvalidOperationException(
                    "PaymentAPI không trả về số payment đã hủy.");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Lỗi khi gọi PaymentAPI hủy payment Pending cho order {OrderId}",
                orderId);

            throw;
        }
    }

    private sealed class CancelPendingPaymentsResponse
    {
        public bool Success { get; set; }
        public int OrderId { get; set; }
        public int CancelledCount { get; set; }
    }
}
