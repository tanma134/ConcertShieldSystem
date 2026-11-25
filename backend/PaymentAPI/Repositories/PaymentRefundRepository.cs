using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PaymentAPI.Data;
using PaymentAPI.DTOs;
using PaymentAPI.Models;

namespace PaymentAPI.Repositories
{
    public class PaymentRefundRepository : IPaymentRefundRepository
    {
        private readonly PaymentDbContext _db;

        public PaymentRefundRepository(PaymentDbContext db)
        {
            _db = db;
        }

        public async Task<PaymentRefund?> GetByReturnRequestIdAsync(int returnRequestId, CancellationToken ct = default)
        {
            return await _db.PaymentRefunds.FirstOrDefaultAsync(r => r.ReturnRequestId == returnRequestId, ct);
        }

        public async Task<PaymentRefund?> GetByIdAsync(int refundId, bool asNoTracking = false, CancellationToken ct = default)
        {
            var query = _db.PaymentRefunds.AsQueryable();
            if (asNoTracking) query = query.AsNoTracking();
            return await query.FirstOrDefaultAsync(r => r.PaymentRefundId == refundId, ct);
        }

        public async Task<List<PaymentRefund>> GetByOrderIdAsync(int orderId, CancellationToken ct = default)
        {
            return await _db.PaymentRefunds
                .Where(r => r.OrderId == orderId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync(ct);
        }

        public async Task<long> GetRefundedAmountByOrderIdAsync(int orderId, CancellationToken ct = default)
        {
            return await _db.PaymentRefunds
                .Where(r => r.OrderId == orderId && r.Status == "Refunded")
                .SumAsync(r => (long?)r.Amount, ct) ?? 0;
        }

        public async Task<List<PaymentRefund>> GetReconciliationRefundsAsync(ReconciliationFilterDto filter, CancellationToken ct = default)
        {
            var query = _db.PaymentRefunds.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var term = filter.Search.Trim().ToLower();
                var cleanDigits = new string(term.Where(char.IsDigit).ToArray());
                if (int.TryParse(cleanDigits, out var numericId))
                {
                    query = query.Where(r =>
                        r.PaymentRefundId == numericId ||
                        r.OrderId == numericId ||
                        (r.CustomerName != null && r.CustomerName.ToLower().Contains(term)) ||
                        (r.GatewayRefundRef != null && r.GatewayRefundRef.ToLower().Contains(term)));
                }
                else
                {
                    query = query.Where(r =>
                        (r.CustomerName != null && r.CustomerName.ToLower().Contains(term)) ||
                        (r.GatewayRefundRef != null && r.GatewayRefundRef.ToLower().Contains(term)));
                }
            }

            if (!string.IsNullOrWhiteSpace(filter.Status) && !string.Equals(filter.Status, "All", StringComparison.OrdinalIgnoreCase))
            {
                if (filter.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase) || filter.Status.Equals("Refunded", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(r => r.Status == "Refunded");
                else
                    query = query.Where(r => r.Status == filter.Status);
            }

            if (!string.IsNullOrWhiteSpace(filter.MatchStatus) && !string.Equals(filter.MatchStatus, "All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(r => r.ReconciliationStatus == filter.MatchStatus);
            }

            if (filter.EventId.HasValue && filter.EventId.Value > 0)
            {
                query = query.Where(r => r.EventId == filter.EventId.Value);
            }

            return await query.OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
        }

        public async Task<List<EventOptionDto>> GetDistinctRefundEventsAsync(CancellationToken ct = default)
        {
            return await _db.PaymentRefunds.AsNoTracking()
                .Where(r => r.EventId != null)
                .GroupBy(r => new { r.EventId, r.EventName })
                .Select(g => new EventOptionDto
                {
                    EventId = g.Key.EventId!.Value,
                    EventName = string.IsNullOrWhiteSpace(g.Key.EventName) ? $"Event #{g.Key.EventId}" : g.Key.EventName,
                    RefundCount = g.Count()
                })
                .OrderByDescending(e => e.RefundCount)
                .ToListAsync(ct);
        }

        public async Task AddAsync(PaymentRefund refund, CancellationToken ct = default)
        {
            await _db.PaymentRefunds.AddAsync(refund, ct);
        }

        public async Task SaveChangesAsync(CancellationToken ct = default)
        {
            await _db.SaveChangesAsync(ct);
        }

        public void Detach(PaymentRefund refund)
        {
            _db.Entry(refund).State = EntityState.Detached;
        }
    }
}

