using TicketAPI.DTOs;

namespace TicketAPI.Services
{
    public interface IReportService
    {
        Task<RevenueReportDto> GetRevenueAsync(
            int eventId, int callerId, bool isAdmin, string? bearer,
            DateOnly? from, DateOnly? to, int? ticketTypeId);

        Task<ExportFileDto> ExportRevenueAsync(
            int eventId, int callerId, bool isAdmin, string? bearer,
            DateOnly? from, DateOnly? to, int? ticketTypeId);

        Task<CheckinReportDto> GetCheckinAsync(int eventId, int callerId, bool isAdmin, string? bearer);

        Task<ExportFileDto> ExportCheckinAsync(int eventId, int callerId, bool isAdmin, string? bearer);
    }
}
