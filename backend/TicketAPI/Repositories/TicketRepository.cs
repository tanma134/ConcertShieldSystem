using Microsoft.EntityFrameworkCore;
using TicketAPI.Domain;
using TicketAPI.Models;
using static TicketAPI.DTOs.AdminTicketDtos;

namespace TicketAPI.Repositories
{
    public class TicketRepository : ITicketRepository
    {
        private readonly TicketDbContext _context;

        public TicketRepository(TicketDbContext context)
        {
            _context = context;
        }

        public Task AddRangeAsync(IEnumerable<Ticket> tickets)
        {
            return _context.Tickets.AddRangeAsync(tickets);
        }

        public Task AddQrTokensAsync(IEnumerable<TicketQrToken> qrTokens)
        {
            return _context.TicketQrTokens.AddRangeAsync(qrTokens);
        }

        public async Task<Ticket?> GetTicketForQrRotationAsync(int ticketId, int userId)
        {
            return await _context.Tickets
                .Include(t => t.Order)
                .Include(t => t.TicketQrTokens)
                .FirstOrDefaultAsync(t =>
                    t.TicketId == ticketId &&
                    t.OwnerUserId == userId &&
                    !t.IsDeleted);
        }

        public async Task<List<Ticket>> GetMyTicketsAsync(int userId)
        {
            return await _context.Tickets
                .AsNoTracking()
                .Include(ticket => ticket.Order)
                    .ThenInclude(order => order.OrderDetails)
                .Where(ticket =>
                    ticket.OwnerUserId == userId &&
                    !ticket.IsDeleted &&
                    !ticket.Order.IsDeleted)
                .OrderByDescending(ticket => ticket.Order.OrderDate)
                .ToListAsync();
        }

        public async Task<Ticket?> GetMyTicketByIdAsync(int ticketId, int userId)
        {
            return await _context.Tickets
                .AsNoTracking()
                .Include(ticket => ticket.Order)
                    .ThenInclude(order => order.OrderDetails)
                .FirstOrDefaultAsync(ticket =>
                    ticket.TicketId == ticketId &&
                    ticket.OwnerUserId == userId &&
                    !ticket.IsDeleted &&
                    !ticket.Order.IsDeleted);
        }

        public async Task<Ticket?> GetTicketForAdminRevokeAsync(int ticketId)
        {
            return await _context.Tickets
                .AsNoTracking()
                .Include(ticket => ticket.TicketQrTokens)
                .FirstOrDefaultAsync(ticket =>
                    ticket.TicketId == ticketId &&
                    !ticket.IsDeleted);
        }

        public async Task RevokeTicketAsync(
            int ticketId,
            int adminUserId,
            string reason)
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            var ticket = await _context.Tickets
                .FromSqlInterpolated(
                    $"SELECT * FROM tickets WHERE ticket_id = {ticketId} AND is_deleted = false FOR UPDATE")
                .FirstOrDefaultAsync();

            if (ticket == null)
                throw new KeyNotFoundException("Không tìm thấy vé.");

            var qrTokens = await _context.TicketQrTokens
                .Where(token => token.TicketId == ticketId)
                .ToListAsync();

            if (ticket.CheckedInAt.HasValue ||
                qrTokens.Any(token => token.IsUsed))
            {
                throw new InvalidOperationException("Không thể thu hồi vé đã check-in.");
            }

            if (ticket.Status != TicketStatuses.Active)
            {
                throw new InvalidOperationException(
                    "Chỉ có thể thu hồi vé đang ở trạng thái Active.");
            }

            var now = DateTime.UtcNow;

            ticket.Status = TicketStatuses.Revoked;

            foreach (var token in qrTokens.Where(token => !token.IsRevoked))
            {
                token.IsRevoked = true;
                token.RevokedAt = now;
            }

            _context.StatusHistories.Add(new StatusHistory
            {
                TargetType = "Ticket",
                TargetId = ticket.TicketId,
                EventId = ticket.EventId,
                OrderId = ticket.OrderId,
                TicketId = ticket.TicketId,
                TicketTypeId = ticket.TicketTypeId,
                SeatId = ticket.SeatId,
                CustomerId = ticket.OwnerUserId,
                Action = "AdminRevoke",
                OldStatus = TicketStatuses.Active,
                NewStatus = TicketStatuses.Revoked,
                Quantity = 1,
                ActorUserId = adminUserId,
                ActorRole = "Admin",
                Reason = reason,
                CreatedAt = now,
                Metadata = "{}"
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        public async Task<(List<Ticket> Items, int TotalCount)> GetAdminTicketsAsync(
    AdminTicketQueryDto query)
        {
            var tickets = _context.Tickets
                .AsNoTracking()
                .Include(ticket => ticket.Order)
                    .ThenInclude(order => order.OrderDetails)
                .Where(ticket => !ticket.IsDeleted && !ticket.Order.IsDeleted);

            if (query.TicketId.HasValue)
                tickets = tickets.Where(ticket => ticket.TicketId == query.TicketId.Value);

            if (query.OrderId.HasValue)
                tickets = tickets.Where(ticket => ticket.OrderId == query.OrderId.Value);

            if (query.EventId.HasValue)
                tickets = tickets.Where(ticket => ticket.EventId == query.EventId.Value);

            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                var status = query.Status.Trim();
                tickets = tickets.Where(ticket => ticket.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();

                tickets = tickets.Where(ticket =>
                    (ticket.OwnerName != null && ticket.OwnerName.Contains(search)) ||
                    (ticket.TicketTypeName != null && ticket.TicketTypeName.Contains(search)) ||
                    (ticket.Order.Email != null && ticket.Order.Email.Contains(search)) ||
                    (ticket.Order.FullName != null && ticket.Order.FullName.Contains(search)));
            }

            var totalCount = await tickets.CountAsync();

            var items = await tickets
                .OrderByDescending(ticket => ticket.TicketId)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<Ticket?> GetAdminTicketByIdAsync(int ticketId)
        {
            return await _context.Tickets
                .AsNoTracking()
                .Include(ticket => ticket.Order)
                    .ThenInclude(order => order.OrderDetails)
                .FirstOrDefaultAsync(ticket =>
                    ticket.TicketId == ticketId &&
                    !ticket.IsDeleted &&
                    !ticket.Order.IsDeleted);
        }

        public async Task<Ticket> ReissueTicketAsync(
    int ticketId,
    int adminUserId,
    string reason)
        {
            if (ticketId <= 0)
                throw new ArgumentException("Ticket ID is invalid.");

            if (adminUserId <= 0)
                throw new ArgumentException("Admin ID is invalid.");

            var normalizedReason = reason?.Trim();

            if (string.IsNullOrWhiteSpace(normalizedReason))
                throw new ArgumentException("A reason is required.");

            if (normalizedReason.Length > 500)
                throw new ArgumentException("The reason cannot exceed 500 characters.");

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            var oldTicket = await _context.Tickets
                .FromSqlInterpolated(
                    $"SELECT * FROM tickets WHERE ticket_id = {ticketId} AND is_deleted = false FOR UPDATE")
                .FirstOrDefaultAsync();

            if (oldTicket == null)
                throw new KeyNotFoundException("Ticket not found.");

            var order = await _context.Orders
                .FirstOrDefaultAsync(item =>
                    item.OrderId == oldTicket.OrderId &&
                    !item.IsDeleted);

            if (order == null)
                throw new KeyNotFoundException("The ticket's order was not found.");

            if (order.Status != "Paid")
                throw new InvalidOperationException(
                    "Only tickets belonging to paid orders can be re-issued.");

            var oldQrTokens = await _context.TicketQrTokens
                .Where(token => token.TicketId == ticketId)
                .ToListAsync();

            if (oldTicket.CheckedInAt.HasValue ||
                oldQrTokens.Any(token => token.IsUsed))
            {
                throw new InvalidOperationException(
                    "A checked-in ticket cannot be re-issued.");
            }

            var previousStatus = oldTicket.Status;

            if (previousStatus != TicketStatuses.Active &&
                previousStatus != TicketStatuses.Revoked)
            {
                throw new InvalidOperationException(
                    "Only Active or Revoked tickets can be re-issued.");
            }

            var alreadyReissued = await _context.StatusHistories
                .AsNoTracking()
                .AnyAsync(history =>
                    history.TicketId == oldTicket.TicketId &&
                    history.Action == "TicketReissued" &&
                    history.NewStatus == TicketStatuses.Revoked);

            if (alreadyReissued)
            {
                throw new InvalidOperationException(
                    "A replacement ticket has already been issued for this ticket.");
            }

            var now = DateTime.UtcNow;

            oldTicket.Status = TicketStatuses.Revoked;

            foreach (var token in oldQrTokens.Where(token => !token.IsRevoked))
            {
                token.IsRevoked = true;
                token.RevokedAt = now;
            }

            var replacement = new Ticket
            {
                TicketCode = Guid.NewGuid(),
                OrderId = oldTicket.OrderId,
                EventId = oldTicket.EventId,
                TicketTypeId = oldTicket.TicketTypeId,
                TicketTypeName = oldTicket.TicketTypeName,
                SeatId = oldTicket.SeatId,
                OwnerUserId = oldTicket.OwnerUserId,
                OwnerName = oldTicket.OwnerName,
                Status = TicketStatuses.Active,
                IsDeleted = false,
                Order = order
            };

            _context.Tickets.Add(replacement);

            // Save once to generate the new ticket ID before writing its QR and history.
            await _context.SaveChangesAsync();

            var newQrToken =
                Convert.ToBase64String(
                        System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
                    .Replace("+", "-")
                    .Replace("/", "_")
                    .TrimEnd('=');

            _context.TicketQrTokens.Add(new TicketQrToken
            {
                TicketId = replacement.TicketId,
                QrToken = newQrToken,
                IssuedAt = now,
                ExpiresAt = now.AddSeconds(60),
                IsRevoked = false,
                IsUsed = false
            });

            _context.StatusHistories.AddRange(
                new StatusHistory
                {
                    TargetType = "Ticket",
                    TargetId = oldTicket.TicketId,
                    EventId = oldTicket.EventId,
                    OrderId = oldTicket.OrderId,
                    TicketId = oldTicket.TicketId,
                    TicketTypeId = oldTicket.TicketTypeId,
                    SeatId = oldTicket.SeatId,
                    CustomerId = oldTicket.OwnerUserId,
                    Action = "TicketReissued",
                    OldStatus = previousStatus,
                    NewStatus = TicketStatuses.Revoked,
                    Quantity = 1,
                    ActorUserId = adminUserId,
                    ActorRole = "Admin",
                    Reason = normalizedReason,
                    CreatedAt = now,
                    Metadata = System.Text.Json.JsonSerializer.Serialize(
                        new { replacementTicketId = replacement.TicketId })
                },
                new StatusHistory
                {
                    TargetType = "Ticket",
                    TargetId = replacement.TicketId,
                    EventId = replacement.EventId,
                    OrderId = replacement.OrderId,
                    TicketId = replacement.TicketId,
                    TicketTypeId = replacement.TicketTypeId,
                    SeatId = replacement.SeatId,
                    CustomerId = replacement.OwnerUserId,
                    Action = "TicketReissued",
                    OldStatus = null,
                    NewStatus = TicketStatuses.Active,
                    Quantity = 1,
                    ActorUserId = adminUserId,
                    ActorRole = "Admin",
                    Reason = normalizedReason,
                    CreatedAt = now,
                    Metadata = System.Text.Json.JsonSerializer.Serialize(
                        new { replacedTicketId = oldTicket.TicketId })
                });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return replacement;
        }
    }
}
