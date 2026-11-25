using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketAPI.DTOs;
using TicketAPI.Services;

namespace TicketAPI.Controllers
{
    [Route("api/organizer/events/{eventId:int}")]
    [Authorize(Roles = "Organizer,Admin")]
    public class OrganizerReportsController : TicketApiControllerBase
    {
        private readonly IReportService _service;

        public OrganizerReportsController(IReportService service)
        {
            _service = service;
        }

        [HttpGet("revenue")]
        public async Task<IActionResult> GetRevenue(
            int eventId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] int? ticketTypeId)
        {
            try
            {
                var report = await _service.GetRevenueAsync(
                    eventId, CurrentUserId, IsAdmin, BearerToken, from, to, ticketTypeId);
                return Success(report);
            }
            catch (Exception ex)
            {
                return Fail(ex);
            }
        }

        [HttpGet("revenue/export")]
        public async Task<IActionResult> ExportRevenue(
            int eventId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] int? ticketTypeId)
        {
            try
            {
                var file = await _service.ExportRevenueAsync(
                    eventId, CurrentUserId, IsAdmin, BearerToken, from, to, ticketTypeId);
                return File(file.Content, "text/csv; charset=utf-8", file.FileName);
            }
            catch (Exception ex)
            {
                return Fail(ex);
            }
        }

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

        [HttpGet("checkin/export")]
        public async Task<IActionResult> ExportCheckin(int eventId)
        {
            try
            {
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
