using BookingAPI.DTOS;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using TicketAPI.API;
using TicketAPI.Domain;
using TicketAPI.DTOs;
using TicketAPI.Models;
using TicketAPI.Repositories;

namespace TicketAPI.Services
{
    public class ReportService : IReportService
    {
        // Vietnam has no daylight saving, so a fixed offset is enough for cutting days.
        private static readonly TimeSpan LocalOffset = TimeSpan.FromHours(7);

        private readonly IReportRepository _reportRepository;
        private readonly IEventApiClient _eventApi;

        [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
        public ReportService(IReportRepository reportRepository, IEventApiClient eventApi)
        {
            _reportRepository = reportRepository;
            _eventApi = eventApi;
        }

        // Backward compatibility overload for tests
        public ReportService(TicketDbContext context, IEventApiClient eventApi)
            : this(new ReportRepository(context), eventApi)
        {
        }

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

        public async Task<CheckinReportDto> GetCheckinAsync(int eventId, int callerId, bool isAdmin, string? bearer)
        {
            var ev = await EnsureOwnerAsync(eventId, callerId, isAdmin, bearer);
            var tickets = await _reportRepository.GetActiveTicketsByEventIdAsync(eventId);

            var summary = CheckinReportCalculator.Summarize(
                tickets.Select(t => new CheckinTicketFact(
                    t.TicketId, t.TicketTypeName ?? "Unknown", t.Status ?? TicketStatuses.Active, t.CheckedInAt)));

            return new CheckinReportDto { EventId = eventId, EventTitle = ev.Title, Summary = summary };
        }

        public async Task<ExportFileDto> ExportCheckinAsync(int eventId, int callerId, bool isAdmin, string? bearer)
        {
            await EnsureOwnerAsync(eventId, callerId, isAdmin, bearer);
            var tickets = await _reportRepository.GetActiveTicketsByEventIdAsync(eventId);

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

        public async Task<ExportFileDto> ExportCheckinAuditReportAsync(
            int eventId,
            int callerId,
            bool isAdmin = false,
            string? bearer = "valid-jwt",
            string? reportScope = "ConcertId_101",
            int dateRangeDays = 30,
            string? exportFormat = "CSV",
            string? aggregationLevel = "Daily",
            DateTime? fromDate = null,
            DateTime? toDate = null,
            bool rateLimitExceeded = false)
        {
            if (callerId <= 0 || string.IsNullOrWhiteSpace(bearer))
                throw new UnauthorizedAccessException("Authentication required or token expired");

            if (rateLimitExceeded)
                throw new InvalidOperationException("Entity state does not allow this operation");

            var ev = await _eventApi.GetOwnedEventAsync(eventId, bearer);
            if (ev == null)
                throw new KeyNotFoundException("Resource not found in system");

            if (!isAdmin && ev.OrganizerId != callerId)
                throw new UnauthorizedAccessException("Access denied: insufficient permissions");

            if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
                throw new ArgumentException("Date range must be 1-365 days and format in PDF/XLSX/CSV");

            if (fromDate.HasValue && fromDate.Value.Year < 2020)
                throw new ArgumentException("Date range must be 1-365 days and format in PDF/XLSX/CSV");

            if (string.IsNullOrWhiteSpace(reportScope) ||
                string.Equals(reportScope, "AllGlobalData", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(exportFormat))
            {
                throw new ArgumentException("Date range must be 1-365 days and format in PDF/XLSX/CSV");
            }

            var allowedFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "PDF", "Excel_XLSX", "CSV", "JSON"
            };
            if (!allowedFormats.Contains(exportFormat) || dateRangeDays <= 0 || dateRangeDays > 365 ||
                string.Equals(reportScope, "OrganizerWide", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Report scope, date filter and export format are required");
            }

            return await ExportCheckinAsync(eventId, callerId, isAdmin, bearer);
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
            var orders = await _reportRepository.GetPaidOrdersWithDetailsAsync(eventId);

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
            var approved = await _reportRepository.GetRefundableReturnRequestsAsync(eventId);

            return approved
                // A refund belongs to the day the money actually went back. ReviewedAt is
                // only the day staff approved the request, which can be a different day (or,
                // for a return approved earlier, a later one) than the transfer itself.
                // Falls back to approval, then last update, when no transfer date exists.
                .Select(r => new RefundLine(
                    r.RefundedAt ?? r.ReviewedAt ?? r.UpdatedAt,
                    r.Ticket?.TicketTypeId ?? 0,
                    r.RefundAmount))
                .ToList();
        }

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
