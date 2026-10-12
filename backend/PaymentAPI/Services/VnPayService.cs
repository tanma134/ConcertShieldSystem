using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PaymentAPI.Models;

namespace PaymentAPI.Services
{
    public class VnPayService : IVnPayService
    {
        // VNPay chỉ nhận mỗi giao dịch dưới 1 tỷ VND.
        private const long MaxVnPayAmountVnd = 999_999_999L;

        private readonly VnPayConfig _config;
        private readonly IPaymentTransactionService _paymentTransactionService;

        public VnPayService(
            IOptions<VnPayConfig> config,
            IHttpClientFactory factory,
            IConfiguration appConfig,
            IPaymentTransactionService paymentTransactionService)
        {
            _config = config.Value;
            _paymentTransactionService = paymentTransactionService;
        }
        public string CreatePaymentUrl(long orderId, long amount, string ipAddress)
        {
            // vnp_Amount = số tiền VND x 100. PHẢI tính bằng long: với int, 50.000.000 x 100 = 5 tỷ
            // vượt 2,147,483,647 nên bị tràn số và VNPay nhận ~7 triệu thay vì 50 triệu.
            if (amount <= 0 || amount > MaxVnPayAmountVnd)
                throw new ArgumentOutOfRangeException(nameof(amount),
                    $"Số tiền phải từ 1 đến {MaxVnPayAmountVnd:N0} VND.");

            var vnpUrl = _config.BaseUrl;
            var tmnCode = _config.TmnCode;
            var hashSecret = _config.HashSecret;
            var returnUrl = _config.ReturnUrl;

            var vnpayData = new SortedDictionary<string, string>
            {
                { "vnp_Version", "2.1.0" },
                { "vnp_Command", "pay" },
                { "vnp_TmnCode", tmnCode },
                { "vnp_Amount", checked(amount * 100L).ToString() },
                { "vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss") },
                { "vnp_CurrCode", "VND" },
                { "vnp_IpAddr", ipAddress ?? "127.0.0.1" },
                { "vnp_Locale", "en" },
                { "vnp_OrderInfo", $"Thanh toan don hang {orderId}" },
                { "vnp_OrderType", "other" },
                { "vnp_ReturnUrl", returnUrl },
                { "vnp_TxnRef", orderId.ToString() }
            };

            var query = string.Join("&",
                vnpayData.Select(kvp =>
                    $"{kvp.Key}={System.Net.WebUtility.UrlEncode(kvp.Value)}"
                )
            );

            var secureHash = HmacSHA512(hashSecret, query);

            return $"{vnpUrl}?{query}&vnp_SecureHash={secureHash}";
        }

        public bool ValidateSignature(IQueryCollection query, string rawQuery)
        {
            var hashSecret = _config.HashSecret;
            var vnp_SecureHash = query["vnp_SecureHash"].ToString();

            var queryString = rawQuery.TrimStart('?');

            var filtered = string.Join("&",
                queryString.Split('&')
                    .Where(x => !x.StartsWith("vnp_SecureHash"))
                    .OrderBy(x => x)
            );

            //var sortedParams = queryString.Split('&')
            //.Where(x => !x.StartsWith("vnp_SecureHash"))
            //.Select(x => {
            //    var parts = x.Split('=', 2);
            //    return new { Key = parts[0], Value = parts.Length > 1 ? parts[1] : "" };
            //})
            //.OrderBy(x => x.Key, StringComparer.Ordinal)  
            //.Select(x => $"{x.Key}={x.Value}");

            //var filtered = string.Join("&", sortedParams);

            var computedHash = HmacSHA512(hashSecret, filtered);

            return computedHash.Equals(vnp_SecureHash, StringComparison.OrdinalIgnoreCase);
        }

        private string HmacSHA512(string key, string inputData)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            var inputBytes = Encoding.UTF8.GetBytes(inputData);

            using (var hmac = new HMACSHA512(keyBytes))
            {
                var hashValue = hmac.ComputeHash(inputBytes);
                return BitConverter.ToString(hashValue)
                    .Replace("-", "")
                    .ToLower();
            }
        }

        public async Task<string> HandleVNPayReturn(IQueryCollection query, string rawQuery)
        {
            if (!ValidateSignature(query, rawQuery))
                return "Invalid signature";

            var responseCode = query["vnp_ResponseCode"].ToString();
            var transactionStatus = query["vnp_TransactionStatus"].ToString();
            var orderIdStr = query["vnp_TxnRef"].ToString();

            if (!long.TryParse(query["vnp_Amount"], out var vnpAmount))
                return "Invalid amount";

            var transactionNo = query["vnp_TransactionNo"].ToString();

            var isSuccess = responseCode == "00" && transactionStatus == "00";

            if (!int.TryParse(orderIdStr, out int orderId))
                return "Invalid order id";

            var callbackPayload = JsonSerializer.Serialize(
                query.ToDictionary(
                    item => item.Key,
                    item => item.Value.ToString()));

            var updated = await _paymentTransactionService.UpdatePaymentTransactionAsync(orderId, vnpAmount, transactionNo, isSuccess ? "Success" : "Failed", callbackPayload);

            if (!updated)
                return "Payment transaction not found or amount does not match";

            if (responseCode == "00" && transactionStatus == "00")
            {
                return "Payment successful";
            }

            return "Payment failed or cancelled";
        }
    }
}
