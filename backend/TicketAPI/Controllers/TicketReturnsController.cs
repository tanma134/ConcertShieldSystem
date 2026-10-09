using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketAPI.API;
using TicketAPI.DTOs;
using TicketAPI.Services;

namespace TicketAPI.Controllers
{
    // UC_9: ticket return requests of the signed-in customer.
    [Route("api/ticket-returns")]
    [Authorize]
    public class TicketReturnsController : TicketApiControllerBase
    {
        private readonly ITicketReturnService _service;

        private readonly IEventApiClient _eventApiClient;

        public TicketReturnsController(ITicketReturnService service, IEventApiClient eventApiClient)
        {
            _service = service;
            _eventApiClient = eventApiClient;
        }

        // Admin sees every event (null). Staff only see events whose organizer gave them the
        // return-review duty; with no assignment the list is empty and nothing can be reviewed.
        private async Task<IReadOnlyCollection<int>?> ReviewScopeAsync()
        {
            if (IsAdmin) return null;
            return (await _eventApiClient.GetReturnReviewEventIdsAsync(CurrentUserId)).ToList();
        }

        // UC_9.1: POST api/ticket-returns
        [HttpPost]
        public async Task<IActionResult> Submit([FromBody] SubmitTicketReturnDto dto)
        {
            try
            {
                var result = await _service.SubmitAsync(CurrentUserId, dto);
                return Success(result, "Return request submitted.");
            }
            catch (Exception ex)
            {
                return Fail(ex);
            }
        }

        // UC_9.2: GET api/ticket-returns?status=Pending
        [HttpGet]
        public async Task<IActionResult> List([FromQuery] string? status)
        {
            try
            {
                return Success(await _service.ListMineAsync(CurrentUserId, status));
            }
            catch (Exception ex)
            {
                return Fail(ex);
            }
        }

        // UC_9.2: GET api/ticket-returns/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                return Success(await _service.GetMineAsync(CurrentUserId, id));
            }
            catch (Exception ex)
            {
                return Fail(ex);
            }
        }

        // UC_9.3: POST api/ticket-returns/{id}/cancel
        [HttpPost("{id:int}/cancel")]
        public async Task<IActionResult> Cancel(int id)
        {
            try
            {
                var result = await _service.CancelAsync(CurrentUserId, id);
                return Success(result, "Return request cancelled.");
            }
            catch (Exception ex)
            {
                return Fail(ex);
            }
        }

        // Support Staff/Admin: review queue.
        [HttpGet("review")]
        [Authorize(Roles = "Staff,Admin")]
        public async Task<IActionResult> ReviewQueue([FromQuery] string? status)
        {
            try
            {
                return Success(await _service.ListForReviewAsync(status, await ReviewScopeAsync()));
            }
            catch (Exception ex)
            {
                return Fail(ex);
            }
        }

        // Support Staff/Admin: approve or reject a pending return.
        [HttpPost("{id:int}/review")]
        [Authorize(Roles = "Staff,Admin")]
        public async Task<IActionResult> Review(int id, [FromBody] ReviewTicketReturnDto dto)
        {
            try
            {
                return Success(await _service.ReviewAsync(CurrentUserId, id, dto, await ReviewScopeAsync()),
                    dto.Approve ? "Return request approved." : "Return request rejected.");
            }
            catch (Exception ex)
            {
                return Fail(ex);
            }
        }

        // Support Staff/Admin: retry paying the money back after a failed refund.
        [HttpPost("{id:int}/retry-refund")]
        [Authorize(Roles = "Staff,Admin")]
        public async Task<IActionResult> RetryRefund(int id)
        {
            try
            {
                return Success(await _service.RetryRefundAsync(CurrentUserId, id, await ReviewScopeAsync()),
                    "Refund retried.");
            }
            catch (Exception ex)
            {
                return Fail(ex);
            }
        }
    }
}
