using Microsoft.EntityFrameworkCore;
using TicketAPI.Domain;
using TicketAPI.Models;

namespace TicketAPI.Repositories
{
    public class TicketReturnRepository : ITicketReturnRepository
    {
        private readonly TicketDbContext _context;

        public TicketReturnRepository(TicketDbContext context)
        {
            _context = context;
        }

        public async Task<List<Ticket>> GetPaidTicketsByOwnerAsync(int userId)
        {
            return await _context.Tickets
                .AsNoTracking()
                .Include(t => t.Order).ThenInclude(o => o.OrderDetails)
                .Where(t => t.OwnerUserId == userId && !t.IsDeleted && t.Order.Status == "Paid")
                .ToListAsync();
        }

        public async Task<List<TicketReturnRequest>> GetRequestsByTicketIdsAsync(IEnumerable<int> ticketIds)
        {
            return await _context.TicketReturnRequests
                .AsNoTracking()
                .Where(r => ticketIds.Contains(r.TicketId))
                .ToListAsync();
        }

        public async Task<Dictionary<int, AppliedEventChange>> GetLatestChangesByTicketIdsAsync(IEnumerable<int> ticketIds)
        {
            var changes = await (from affected in _context.Set<AffectedTicket>().AsNoTracking()
                                 join change in _context.Set<AppliedEventChange>().AsNoTracking() on affected.ChangeId equals change.ChangeId
                                 where ticketIds.Contains(affected.TicketId)
                                 select new { affected.TicketId, Change = change }).ToListAsync();

            return changes.GroupBy(x => x.TicketId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Change.ScheduleVersion).First().Change);
        }

        public async Task<AppliedEventChange?> GetLatestChangeForTicketAsync(int ticketId)
        {
            return await (from affected in _context.Set<AffectedTicket>().AsNoTracking()
                          join applied in _context.Set<AppliedEventChange>().AsNoTracking() on affected.ChangeId equals applied.ChangeId
                          where affected.TicketId == ticketId
                          orderby applied.ScheduleVersion descending
                          select applied).FirstOrDefaultAsync();
        }

        public async Task<Ticket?> GetTicketForReturnAsync(int ticketId, int userId)
        {
            return await _context.Tickets
                .Include(t => t.Order).ThenInclude(o => o.OrderDetails)
                .Include(t => t.TicketQrTokens)
                .FirstOrDefaultAsync(t => t.TicketId == ticketId && t.OwnerUserId == userId && !t.IsDeleted);
        }

        // Lấy danh sách vé thuộc quyền sở hữu của người dùng để xử lý hoàn theo lô
        public async Task<List<Ticket>> GetTicketsForReturnAsync(IEnumerable<int> ticketIds, int userId)
        {
            return await _context.Tickets
                .Include(t => t.Order).ThenInclude(o => o.OrderDetails)
                .Include(t => t.TicketQrTokens)
                .Where(t => ticketIds.Contains(t.TicketId) && t.OwnerUserId == userId && !t.IsDeleted)
                .ToListAsync();
        }

        // Lấy thời điểm tạo yêu cầu hoàn vé gần nhất của tài khoản để kiểm tra giãn cách (cooldown)
        public async Task<DateTime?> GetLatestRequestCreatedAtByUserAsync(int userId)
        {
            return await _context.TicketReturnRequests
                .AsNoTracking()
                .Where(r => r.RequesterUserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => (DateTime?)r.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<bool> HasPendingRequestForTicketAsync(int ticketId)
        {
            return await _context.TicketReturnRequests
                .AnyAsync(r => r.TicketId == ticketId && r.Status == ReturnRequestStatuses.Pending);
        }

        public async Task AddRequestAsync(TicketReturnRequest request)
        {
            await _context.TicketReturnRequests.AddAsync(request);
        }

        // Thêm nhiều yêu cầu hoàn vé cùng lúc trong giao dịch batch
        public async Task AddRequestsAsync(IEnumerable<TicketReturnRequest> requests)
        {
            await _context.TicketReturnRequests.AddRangeAsync(requests);
        }

        public async Task<List<TicketReturnRequest>> ListMineAsync(int userId, string? status)
        {
            var query = _context.TicketReturnRequests
                .AsNoTracking()
                .Include(r => r.Ticket).ThenInclude(t => t.Order)
                .Where(r => r.RequesterUserId == userId);

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(r => r.Status == status);

            return await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
        }

        public async Task<TicketReturnRequest?> GetMineAsync(int userId, int requestId)
        {
            return await _context.TicketReturnRequests
                .AsNoTracking()
                .Include(r => r.Ticket).ThenInclude(t => t.Order)
                .FirstOrDefaultAsync(r => r.TicketReturnRequestId == requestId && r.RequesterUserId == userId);
        }

        public async Task<TicketReturnRequest?> GetMineForCancelAsync(int userId, int requestId)
        {
            return await _context.TicketReturnRequests
                .Include(r => r.Ticket).ThenInclude(t => t.Order)
                .FirstOrDefaultAsync(r => r.TicketReturnRequestId == requestId && r.RequesterUserId == userId);
        }

        public async Task<List<TicketReturnRequest>> ListForReviewAsync(string? status, IReadOnlyCollection<int>? eventScope = null)
        {
            var query = _context.TicketReturnRequests
                .AsNoTracking()
                .Include(r => r.Ticket).ThenInclude(t => t.Order)
                .AsQueryable();

            if (eventScope != null)
            {
                var ids = eventScope.ToList();
                query = query.Where(r => ids.Contains(r.EventId));
            }

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(r => r.Status == status);

            return await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
        }

        public async Task<TicketReturnRequest?> GetForReviewAsync(int requestId)
        {
            return await _context.TicketReturnRequests
                .Include(r => r.Ticket).ThenInclude(t => t.Order)
                .FirstOrDefaultAsync(r => r.TicketReturnRequestId == requestId);
        }

        public async Task SyncOrderRefundStatusAsync(int orderId)
        {
            var hasRemainingActive = await _context.Tickets
                .AnyAsync(t => t.OrderId == orderId && !t.IsDeleted && t.Status != TicketStatuses.Returned && t.Status != TicketStatuses.Revoked);

            if (!hasRemainingActive)
            {
                var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId);
                if (order != null)
                {
                    order.Status = "Refunded";
                }
            }
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}

