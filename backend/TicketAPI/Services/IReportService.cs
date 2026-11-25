using TicketAPI.DTOs;

namespace TicketAPI.Services
{
    // Dinh nghia cac phuong thuc tao bao cao doanh thu va thong ke check-in
    public interface IReportService
    {
        // Xem tong quan bao cao doanh thu va loc theo thoi gian, hang ve
        Task<RevenueReportDto> GetRevenueAsync(
            int eventId, int callerId, bool isAdmin, string? bearer,
            DateOnly? from, DateOnly? to, int? ticketTypeId);

        // Xuat bao cao doanh thu chi tiet cua concert ra file CSV
        Task<ExportFileDto> ExportRevenueAsync(
            int eventId, int callerId, bool isAdmin, string? bearer,
            DateOnly? from, DateOnly? to, int? ticketTypeId);

        // Xem thong ke tong quan so lieu check-in cua concert tren man hinh
        Task<CheckinReportDto> GetCheckinAsync(int eventId, int callerId, bool isAdmin, string? bearer);

        // Xuat danh sach lich su check-in cua concert ra file CSV
        Task<ExportFileDto> ExportCheckinAsync(int eventId, int callerId, bool isAdmin, string? bearer);
    }
}
