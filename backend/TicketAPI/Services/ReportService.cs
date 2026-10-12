using BookingAPI.DTOS;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using TicketAPI.API;
using TicketAPI.Domain;
using TicketAPI.DTOs;
using TicketAPI.Models;

namespace TicketAPI.Services
{
    // UC_13 (revenue dashboard, filter, export) and UC_14.2 (check-in report export).
    // Every method first proves that the caller owns the event.
    public class ReportService : IReportService
    {
        // Vietnam has no daylight saving, so a fixed offset is enough for cutting days.
        private static readonly TimeSpan LocalOffset = TimeSpan.FromHours(7);

        private readonly TicketDbContext _context;
        private readonly IEventApiClient _eventApi;

        public ReportService(TicketDbContext context, IEventApiClient eventApi)
        {
            _context = context;
            _eventApi = eventApi;
        }

        // UC_13.1 / UC_13.2: totals and breakdowns for the chosen filters.
        public async Task<RevenueReportDto> GetRevenueAsync(
            int eventId, int callerId, bool isAdmin, string? bearer,
            DateOnly? from, DateOnly? to, int? ticketTypeId)
        {
            var ev = await EnsureOwnerAsync(eventId, callerId, isAdmin, bearer);
            var range = RevenueCalculator.ValidateRange(from, to);
            if (!range.IsValid)
                throw new ArgumentException(range.Error);

            var orders = await LoadPaidOrdersAsync(eventId);
            var refunds = await LoadRefundsAsync(eventId);

            var lines = RevenueCalculator.BuildLines(orders);
            var summary = RevenueCalculator.Summarize(lines, refunds, from, to, ticketTypeId, LocalOffset);

            return new RevenueReportDto
            {
                EventId = eventId,
                EventTitle = ev.Title,
                From = from,
                To = to,
                TicketTypeId = ticketTypeId,
                Summary = summary,
                TicketTypes = lines
                    .GroupBy(l => l.TicketTypeId)
                    .Select(g => new TicketTypeOptionDto { TicketTypeId = g.Key, Name = g.First().TicketTypeName })
                    .OrderBy(t => t.Name)
                    .ToList()
            };
        }

        // UC_13.3: the same report as a CSV file (one table: totals, then days, then ticket types).
        public async Task<ExportFileDto> ExportRevenueAsync(
            int eventId, int callerId, bool isAdmin, string? bearer,
            DateOnly? from, DateOnly? to, int? ticketTypeId)
        {
            var report = await GetRevenueAsync(eventId, callerId, isAdmin, bearer, from, to, ticketTypeId);
            var summary = report.Summary;

            var rows = new List<IReadOnlyList<string?>>
            {
                Row("Total", "All", summary.OrdersCount, summary.TicketsSold,
                    summary.GrossRevenue, summary.Discounts, summary.Refunded, summary.NetRevenue)
            };

            rows.AddRange(summary.ByDay.Select(d => Row("Day", d.Date.ToString("yyyy-MM-dd"),
                d.Orders, d.Tickets, d.Gross, d.Discounts, d.Refunded, d.Net)));

            rows.AddRange(summary.ByTicketType.Select(t => Row("TicketType", t.TicketTypeName,
                null, t.Tickets, t.Gross, t.Discounts, t.Refunded, t.Net)));

            var csv = CsvExporter.Build(
                new[] { "Section", "Name", "Orders", "Tickets", "Gross", "Discounts", "Refunded", "Net" },
                rows);

            return ToFile($"revenue-event-{eventId}-{DateTime.UtcNow:yyyyMMdd}.csv", csv);
        }

        // UC_14.2 (screen): check-in figures shown next to the export button.
        public async Task<CheckinReportDto> GetCheckinAsync(int eventId, int callerId, bool isAdmin, string? bearer)
        {
            var ev = await EnsureOwnerAsync(eventId, callerId, isAdmin, bearer);
            var tickets = await LoadTicketsAsync(eventId);

            var summary = CheckinReportCalculator.Summarize(
                tickets.Select(t => new CheckinTicketFact(
                    t.TicketId, t.TicketTypeName ?? "Unknown", t.Status ?? TicketStatuses.Active, t.CheckedInAt)));

            return new CheckinReportDto { EventId = eventId, EventTitle = ev.Title, Summary = summary };
        }

        // UC_14.2: one CSV line per ticket with its check-in time and the staff member who scanned it.
        public async Task<ExportFileDto> ExportCheckinAsync(int eventId, int callerId, bool isAdmin, string? bearer)
        {
            await EnsureOwnerAsync(eventId, callerId, isAdmin, bearer);
            var tickets = await LoadTicketsAsync(eventId);

            var rows = tickets.Select(t => (IReadOnlyList<string?>)new string?[]
            {
                t.TicketCode.ToString(),
                t.TicketTypeName,
                t.OwnerName,
                t.SeatId?.ToString(CultureInfo.InvariantCulture),
                t.Status,
                t.CheckedInAt.HasValue ? t.CheckedInAt.Value.Add(LocalOffset).ToString("yyyy-MM-dd HH:mm:ss") : null,
                t.CheckedInBy?.ToString(CultureInfo.InvariantCulture)
            });

            var csv = CsvExporter.Build(
                new[] { "TicketCode", "TicketType", "Owner", "SeatId", "Status", "CheckedInAt", "CheckedInByStaffId" },
                rows);

            return ToFile($"checkin-event-{eventId}-{DateTime.UtcNow:yyyyMMdd}.csv", csv);
        }

        // Loads the event through the caller's own token and compares OrganizerId.
        // Not found and not owned look the same so nobody can probe other events.
        private async Task<EventApiResponseDto> EnsureOwnerAsync(int eventId, int callerId, bool isAdmin, string? bearer)
        {
            var ev = await _eventApi.GetOwnedEventAsync(eventId, bearer)
                ?? throw new KeyNotFoundException("Event not found.");

            if (!isAdmin && ev.OrganizerId != callerId)
                throw new KeyNotFoundException("Event not found.");

            return ev;
        }

        // Paid, not deleted orders of the event, as plain facts for the calculator.
        private async Task<List<OrderFact>> LoadPaidOrdersAsync(int eventId)
        {
            var orders = await _context.Orders
                .AsNoTracking()
                .Include(o => o.OrderDetails)
                .Where(o => o.EventId == eventId && o.Status == "Paid" && !o.IsDeleted)
                .ToListAsync();

            return orders.Select(o => new OrderFact(
                o.OrderId,
                o.OrderDate ?? DateTime.UnixEpoch,
                o.DiscountAmount ?? 0,
                o.OrderDetails.Select(d => new OrderDetailFact(
                    d.TicketTypeId, d.TicketTypeName ?? $"Ticket type {d.TicketTypeId}", d.Quantity, d.UnitPrice)).ToList()))
                .ToList();
        }

        // Approved ticket returns count as refunds on the day they were approved.
        private async Task<List<RefundLine>> LoadRefundsAsync(int eventId)
        {
            var approved = await _context.TicketReturnRequests
                .AsNoTracking()
                .Where(r => r.EventId == eventId && (r.Status == ReturnRequestStatuses.Approved || r.Status == ReturnRequestStatuses.Refunded || r.Status == ReturnRequestStatuses.RefundFailed))
                .Select(r => new { r.Ticket.TicketTypeId, r.RefundAmount, r.ReviewedAt, r.UpdatedAt })
                .ToListAsync();

            return approved
                .Select(r => new RefundLine(r.ReviewedAt ?? r.UpdatedAt, r.TicketTypeId, r.RefundAmount))
                .ToList();
        }

        // Every non-deleted ticket of the event, oldest first.
        private async Task<List<Ticket>> LoadTicketsAsync(int eventId)
        {
            return await _context.Tickets
                .AsNoTracking()
                .Where(t => t.EventId == eventId && !t.IsDeleted)
                .OrderBy(t => t.TicketId)
                .ToListAsync();
        }

        // Builds one CSV row for the revenue export.
        private static IReadOnlyList<string?> Row(
            string section, string name, int? orders, int tickets,
            long gross, long discounts, long refunded, long net)
        {
            return new string?[]
            {
                section, name,
                orders?.ToString(CultureInfo.InvariantCulture),
                tickets.ToString(CultureInfo.InvariantCulture),
                gross.ToString(CultureInfo.InvariantCulture),
                discounts.ToString(CultureInfo.InvariantCulture),
                refunded.ToString(CultureInfo.InvariantCulture),
                net.ToString(CultureInfo.InvariantCulture)
            };
        }

        // Adds a UTF-8 byte order mark so Excel shows Vietnamese names correctly.
        private static ExportFileDto ToFile(string fileName, string csv)
        {
            var content = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
            return new ExportFileDto { FileName = fileName, Content = content };
        }
    }
}
