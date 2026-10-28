using System.Threading;
using System.Threading.Tasks;
using PaymentAPI.DTOs;

namespace PaymentAPI.Services
{
    public interface IRefundService
    {
        // Internal ticket return refund (called by TicketAPI)
        Task<RefundResultDto> RefundAsync(CreateRefundRequestDto request, CancellationToken ct = default);

        // UC 3.16.20: Process Full / Partial Refund
        Task<RefundResultDto> ProcessRefundAsync(ProcessRefundRequestDto request, int actorId, string actorName, CancellationToken ct = default);

        // UC 3.16.21: Retry Failed Refund
        Task<RefundResultDto> RetryRefundAsync(int refundId, string strategy, int actorId, string actorName, CancellationToken ct = default);

        // Order summary and eligibility
        Task<OrderRefundSummaryDto> GetOrderRefundSummaryAsync(int orderId, CancellationToken ct = default);

        // UC 3.16.22: Refund Reconciliation Report
        Task<RefundReconciliationReportDto> GetReconciliationReportAsync(ReconciliationFilterDto filter, CancellationToken ct = default);

        // Single refund detail
        Task<RefundItemDto?> GetRefundByIdAsync(int refundId, CancellationToken ct = default);
    }
}
