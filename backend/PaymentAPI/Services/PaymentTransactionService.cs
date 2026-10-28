using PaymentAPI.Models;
using PaymentAPI.Repositories;
using PaymentAPI.Risk;

namespace PaymentAPI.Services
{
    public class PaymentTransactionService : IPaymentTransactionService
    {
        private readonly IPaymentTransactionRepository _repository;
        private readonly IPaymentRiskService _risk;

        public PaymentTransactionService(IPaymentTransactionRepository repository, IPaymentRiskService risk)
        {
            _repository = repository;
            _risk = risk;
        }

        public async Task CreatePaymentTransactionAsync(
            long orderId,
            long amount,
            int? userId = null,
            string? clientIp = null,
            string? deviceFp = null,
            string? sessionId = null)
        {
            var transaction = new PaymentTransaction
            {
                OrderId = checked((int)orderId),
                Gateway = "VNPay",
                Amount = amount,
                Status = "Pending",
                RequestedAt = DateTime.UtcNow,
                UserId = userId,
                ClientIp = clientIp,
                DeviceFp = deviceFp,
                SessionId = sessionId
            };

            await _repository.AddAsync(transaction);
        }

        public async Task<bool> UpdatePaymentTransactionAsync(
            int orderId,
            long vnpAmount,
            string? gatewayTransactionRef,
            string status,
            string? responsePayload)
        {
            if (status != "Success" && status != "Failed")
                throw new ArgumentException("Trạng thái thanh toán không hợp lệ.");

            var transaction =
                await _repository.GetLatestByOrderIdAsync(orderId, "VNPay");

            if (transaction == null)
                return false;

            if (transaction.Amount > long.MaxValue / 100 ||
                transaction.Amount * 100 != vnpAmount)
            {
                return false;
            }

            if (transaction.Status == "Success")
                return status == "Success";

            if (transaction.Status == "Failed" && status == "Failed")
                return true;

            transaction.Status = status;
            transaction.GatewayTransactionRef = gatewayTransactionRef;
            transaction.WebhookPayload = responsePayload;
            transaction.RespondedAt = DateTime.UtcNow;

            await _repository.UpdateAsync(transaction);

            // Risk Engine: thanh toán thất bại nhiều lần (FRD_P01)
            if (status == "Failed")
                await _risk.OnPaymentFailedAsync(transaction);

            return true;
        }
    }
}
