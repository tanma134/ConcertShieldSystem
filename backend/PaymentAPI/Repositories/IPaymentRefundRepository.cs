using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PaymentAPI.DTOs;
using PaymentAPI.Models;

namespace PaymentAPI.Repositories
{
    public interface IPaymentRefundRepository
    {
        Task<PaymentRefund?> GetByReturnRequestIdAsync(int returnRequestId, CancellationToken ct = default);
        Task<PaymentRefund?> GetByIdAsync(int refundId, bool asNoTracking = false, CancellationToken ct = default);
        Task<List<PaymentRefund>> GetByOrderIdAsync(int orderId, CancellationToken ct = default);
        Task<long> GetRefundedAmountByOrderIdAsync(int orderId, CancellationToken ct = default);
        Task<List<PaymentRefund>> GetReconciliationRefundsAsync(ReconciliationFilterDto filter, CancellationToken ct = default);
        Task<List<EventOptionDto>> GetDistinctRefundEventsAsync(CancellationToken ct = default);
        Task AddAsync(PaymentRefund refund, CancellationToken ct = default);
        Task SaveChangesAsync(CancellationToken ct = default);
        void Detach(PaymentRefund refund);
    }
}
