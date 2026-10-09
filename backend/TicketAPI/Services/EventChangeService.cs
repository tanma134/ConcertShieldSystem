using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TicketAPI.Models;
namespace TicketAPI.Services;
public record ApplyChangeInput(long ChangeId, int EventId, string Type, DateTime? StartsAt, DateTime? EndsAt, int ScheduleVersion, string Reason, string Title, string Slug);
public record AffectedRow(int OrderId, int TicketId, int CustomerId, string TicketCode, string TicketStatus, long PaidAmount, bool Notified, string? ReturnStatus, long RefundedAmount);
public class EventChangeService(TicketDbContext db)
{
    // Payment callback và apply dùng cùng advisory lock trong TicketDB, kể cả khi chạy nhiều instance.
    public async Task LockEventAsync(int eventId, CancellationToken ct) { await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(7421, {eventId})", ct); }
    public async Task ApplyAsync(ApplyChangeInput input, CancellationToken ct)
    {
        if (input.ChangeId <= 0 || input.EventId <= 0 || input.ScheduleVersion < 1 || (input.Type != "Postpone" && input.Type != "Reschedule")) throw new ArgumentException("Invalid event change.");
        if (input.Type == "Reschedule" && (!input.StartsAt.HasValue || !input.EndsAt.HasValue || input.EndsAt <= input.StartsAt)) throw new ArgumentException("Invalid new schedule.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockEventAsync(input.EventId, ct);
        var existing = await db.Set<AppliedEventChange>().FindAsync(new object[] { input.ChangeId }, ct);
        if (existing != null)
        {
            if (existing.EventId != input.EventId || existing.ScheduleVersion != input.ScheduleVersion || existing.Type != input.Type || existing.StartsAt != input.StartsAt || existing.EndsAt != input.EndsAt) throw new InvalidOperationException("Idempotency key was reused for a different change.");
            return;
        }
        if (await db.Set<AppliedEventChange>().AnyAsync(x => x.EventId == input.EventId && x.ScheduleVersion >= input.ScheduleVersion, ct)) throw new InvalidOperationException("Stale schedule change.");
        var change = new AppliedEventChange { ChangeId = input.ChangeId, EventId = input.EventId, Type = input.Type, StartsAt = input.StartsAt, EndsAt = input.EndsAt, ScheduleVersion = input.ScheduleVersion, Reason = input.Reason, Title = input.Title, Slug = input.Slug, AppliedAt = DateTime.UtcNow };
        db.Set<AppliedEventChange>().Add(change);
        await db.SaveChangesAsync(ct);
        var orders = await db.Orders.Where(x => x.EventId == input.EventId && !x.IsDeleted && (x.Status == "Paid" || x.Status == "Pending")).ToListAsync(ct);
        foreach (var order in orders)
        {
            // Null biểu thị chưa có lịch mới, không giữ lịch cũ trên My Tickets như lịch hiệu lực.
            order.StartsAt = input.Type == "Postpone" ? null : input.StartsAt;
            order.EndsAt = input.Type == "Postpone" ? null : input.EndsAt;
        }
        var tickets = await db.Tickets.Include(x => x.Order).Where(x => x.EventId == input.EventId && !x.IsDeleted && !x.Order.IsDeleted && x.Order.Status == "Paid" && (x.Status == "Active" || x.Status == "ReturnPending")).ToListAsync(ct);
        foreach (var ticket in tickets) AddAffected(change, ticket);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    private void AddAffected(AppliedEventChange change, Ticket ticket) => db.Set<AffectedTicket>().Add(new() { ChangeId = change.ChangeId, TicketId = ticket.TicketId, OrderId = ticket.OrderId, CustomerId = ticket.Order.CustomerId, PaidAmount = ticket.Order.FinalAmount });
    // Callback đến sau snapshot vẫn giữ Paid và bổ sung vé của đơn đó vào change liên quan.
    public async Task AttachLatePaymentAsync(Order order, List<Ticket> tickets, CancellationToken ct)
    {
        var change = await db.Set<AppliedEventChange>().Where(x => x.EventId == order.EventId).OrderByDescending(x => x.ScheduleVersion).FirstOrDefaultAsync(ct);
        if (change == null || order.OrderDate > change.AppliedAt) return;
        order.StartsAt = change.Type == "Postpone" ? null : change.StartsAt;
        order.EndsAt = change.Type == "Postpone" ? null : change.EndsAt;
        foreach (var ticket in tickets) { ticket.Order = order; AddAffected(change, ticket); }
    }
    public async Task<object> ReadAsync(long? changeId, int? eventId, string tab, string? status, string? orderCode, int? userId, int page, int pageSize, CancellationToken ct)
    {
        var orderId = AffectedFilterRules.Validate(tab, status, orderCode, userId, page, pageSize);
        List<AffectedRow> rows;
        if (changeId.HasValue)
        {
            if (!await db.Set<AppliedEventChange>().AnyAsync(x => x.ChangeId == changeId, ct)) throw new KeyNotFoundException("Synchronization is pending.");
            var facts = await db.Set<AffectedTicket>().AsNoTracking().Where(x => x.ChangeId == changeId).ToListAsync(ct);
            var ids = facts.Select(x => x.TicketId).ToArray();
            var tickets = await db.Tickets.AsNoTracking().Where(x => ids.Contains(x.TicketId)).ToListAsync(ct);
            var returns = await db.TicketReturnRequests.AsNoTracking().Where(x => ids.Contains(x.TicketId)).OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
            rows = facts.Select(x => { var ticket = tickets.First(t => t.TicketId == x.TicketId); var ret = returns.FirstOrDefault(r => r.TicketId == x.TicketId); return new AffectedRow(x.OrderId, x.TicketId, x.CustomerId, ticket.TicketCode.ToString(), ticket.Status ?? "Unknown", x.PaidAmount, x.NotifiedAt.HasValue, ret?.Status, ret?.Status == "Refunded" ? ret.RefundAmount : 0); }).ToList();
        }
        else
        {
            var tickets = await db.Tickets.AsNoTracking().Include(x => x.Order).Where(x => x.EventId == eventId && !x.IsDeleted && !x.Order.IsDeleted && x.Order.Status == "Paid" && (x.Status == "Active" || x.Status == "ReturnPending")).ToListAsync(ct);
            rows = tickets.Select(x => new AffectedRow(x.OrderId, x.TicketId, x.Order.CustomerId, x.TicketCode.ToString(), x.Status ?? "Unknown", x.Order.FinalAmount, false, null, 0)).ToList();
        }
        string Processing(AffectedRow row) => row.ReturnStatus switch { "Refunded" => "Refunded", "RefundFailed" => "RefundFailed", "Approved" => "RefundPending", "Pending" => "RefundPending", _ => row.TicketStatus == "Returned" ? "Returned" : row.Notified ? "Notified" : "Pending" };
        var summary = new { orders = rows.Select(x => x.OrderId).Distinct().Count(), tickets = rows.Count, totalPaid = rows.GroupBy(x => x.OrderId).Sum(x => x.First().PaidAmount), refunded = rows.Sum(x => x.RefundedAmount) };
        var groups = rows.GroupBy(x => x.OrderId).OrderBy(x => x.Key);
        var orders = groups.Select(g => new { orderId = g.Key, orderCode = "ORD-" + g.Key, customerId = g.First().CustomerId, tickets = g.Count(), amount = g.First().PaidAmount, refundedAmount = g.Sum(x => x.RefundedAmount), status = g.All(x => Processing(x) == "Refunded") ? "Refunded" : g.Any(x => Processing(x) == "RefundFailed") ? "RefundFailed" : g.Any(x => Processing(x) == "RefundPending") ? "RefundPending" : g.All(x => x.Notified) ? "Notified" : "Pending" });
        IEnumerable<object> selected;
        if (tab == "orders") selected = orders.Where(x => (!userId.HasValue || x.customerId == userId) && (!orderId.HasValue || x.orderId == orderId) && (string.IsNullOrEmpty(status) || x.status == status));
        else selected = rows.Where(x => (!userId.HasValue || x.CustomerId == userId) && (!orderId.HasValue || x.OrderId == orderId) && (string.IsNullOrEmpty(status) || Processing(x) == status || x.TicketStatus == status)).OrderBy(x => x.OrderId).ThenBy(x => x.TicketId).Select(x => new { x.OrderId, x.TicketId, x.CustomerId, x.TicketCode, x.TicketStatus, status = Processing(x), x.RefundedAmount });
        var result = selected.ToList(); var totalPages = Math.Max(1, (int)Math.Ceiling(result.Count / (double)pageSize)); page = Math.Min(page, totalPages);
        return new { isPreview = !changeId.HasValue, summary, items = result.Skip((page - 1) * pageSize).Take(pageSize), page, pageSize, totalPages, totalCount = result.Count };
    }
}
