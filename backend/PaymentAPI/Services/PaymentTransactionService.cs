using PaymentAPI.Models;
using PaymentAPI.Repositories;

namespace PaymentAPI.Services
{
    public class PaymentTransactionService : IPaymentTransactionService
    {
        private readonly IPaymentTransactionRepository _repository;

        public PaymentTransactionService(IPaymentTransactionRepository repository)
        {
            _repository = repository;
        }

        public async Task CreatePaymentTransactionAsync(
            long orderId,
            long amount)
        {
            var transaction = new PaymentTransaction
            {
                OrderId = checked((int)orderId),
                Gateway = "VNPay",
                Amount = amount,
                Status = "Pending",
                RequestedAt = DateTime.UtcNow
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
            return true;
        }
    }
}
