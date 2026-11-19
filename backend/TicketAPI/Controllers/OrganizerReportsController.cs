using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketAPI.DTOs;
using TicketAPI.Services;

namespace TicketAPI.Controllers
{
    // Bao cao doanh thu va thong ke check-in danh cho Organizer va Admin
    [Route("api/organizer/events/{eventId:int}")]
    [Authorize(Roles = "Organizer,Admin")]
    public class OrganizerReportsController : TicketApiControllerBase
    {
        private readonly IReportService _service;

        public OrganizerReportsController(IReportService service)
        {
            _service = service;
        }

        // Xem dashboard va loc doanh thu concert theo khoang thoi gian va hang ve
        [HttpGet("revenue")]
        public async Task<IActionResult> GetRevenue(
            int eventId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] int? ticketTypeId)
        {
            try
            {
                // Luu y: Chi Organizer chu su kien hoac Admin moi co quyen xem doanh thu
                var report = await _service.GetRevenueAsync(
                    eventId, CurrentUserId, IsAdmin, BearerToken, from, to, ticketTypeId);
                return Success(report);
            }
            catch (Exception ex)
            {
                return Fail(ex);
            }
        }

        // Xuat bao cao doanh thu chi tiet cua concert ra file CSV
        [HttpGet("revenue/export")]
        public async Task<IActionResult> ExportRevenue(
            int eventId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] int? ticketTypeId)
        {
            try
            {
                // Luu y: Tra ve file CSV ma hoa UTF-8 gom day du thong tin don hang va ve da ban
                var file = await _service.ExportRevenueAsync(
                    eventId, CurrentUserId, IsAdmin, BearerToken, from, to, ticketTypeId);
                return File(file.Content, "text/csv; charset=utf-8", file.FileName);
            }
            catch (Exception ex)
            {
                return Fail(ex);
            }
        }

        // Xem thong ke tong quan so lieu check-in cua concert tren man hinh
        [HttpGet("checkin")]
        public async Task<IActionResult> GetCheckin(int eventId)
        {
            try
            {
                return Success(await _service.GetCheckinAsync(eventId, CurrentUserId, IsAdmin, BearerToken));
            }
            catch (Exception ex)
            {
                return Fail(ex);
            }
        }

        // Xuat danh sach lich su check-in cua concert ra file CSV
        [HttpGet("checkin/export")]
        public async Task<IActionResult> ExportCheckin(int eventId)
        {
            try
            {
                // Luu y: File CSV gom ma ve, hang ve, thoi diem quet va cong vao
                var file = await _service.ExportCheckinAsync(eventId, CurrentUserId, IsAdmin, BearerToken);
                return File(file.Content, "text/csv; charset=utf-8", file.FileName);
            }
            catch (Exception ex)
            {
                return Fail(ex);
            }
        }
    }
}
