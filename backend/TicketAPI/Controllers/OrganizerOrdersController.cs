using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TicketAPI.DTOs;
using TicketAPI.Services;

namespace TicketAPI.Controllers
{
    // Quan ly va tra cuu danh sach don hang cua concert danh cho Organizer va Admin
    [Route("api/organizer/events/{eventId:int}/orders")]
    public class OrganizerOrdersController : TicketApiControllerBase
    {
        private readonly IOrderService _orderService;

        public OrganizerOrdersController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        // Lay danh sach don hang cua concert kem bo loc trang thai va tim kiem danh cho Organizer
        [HttpGet]
        public async Task<IActionResult> GetEventOrders(
            int eventId,
            [FromQuery] OrganizerOrderQueryDto query)
        {
            try
            {
                var result = await _orderService.GetOrganizerEventOrdersAsync(
                    eventId,
                    BearerToken,
                    query);

                return Success(result);
            }
            catch (Exception ex)
            {
                return Fail(ex);
            }
        }
    }
}
