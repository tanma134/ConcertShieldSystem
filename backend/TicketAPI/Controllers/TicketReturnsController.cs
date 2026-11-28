using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketAPI.API;
using TicketAPI.DTOs;
using TicketAPI.Risk;
using TicketAPI.Services;

namespace TicketAPI.Controllers
{
    [Route("api/ticket-returns")]
    [Authorize]
    public class TicketReturnsController : TicketApiControllerBase
    {
        private readonly ITicketReturnService _service;

        private readonly IEventApiClient _eventApiClient;
        private readonly ITicketRiskService _risk;
        private readonly IAuditLogClient _auditLog;

        public TicketReturnsController(ITicketReturnService service, IEventApiClient eventApiClient, ITicketRiskService risk, IAuditLogClient auditLog)
        {
            _auditLog = auditLog;
            _risk = risk;
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

        [HttpPost]
        public async Task<IActionResult> Submit([FromBody] SubmitTicketReturnDto dto)
        {
            // Check Risk Engine FRD_T02 fraud block before creating the return request.
            var verdict = await _risk.CheckReturnAsync(HttpContext, CurrentUserId, dto.TicketId);
            if (!verdict.Allowed) return verdict.ToActionResult();

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

        [HttpPost("{id:int}/review")]
        [Authorize(Roles = "Staff,Admin")]
        public async Task<IActionResult> Review(int id, [FromBody] ReviewTicketReturnDto dto)
        {
            try
            {
                var result = await _service.ReviewAsync(CurrentUserId, id, dto, await ReviewScopeAsync());

                await _auditLog.LogAsync(CurrentUserId, AuditActorType, "TICKET_RETURN_REVIEWED", "TicketReturn", id.ToString(),
                    newValue: new { approved = dto.Approve, reviewNote = dto.ReviewNote }, ipAddress: ClientIp);

                return Success(result,
                    dto.Approve ? "Return request approved." : "Return request rejected.");
            }
            catch (Exception ex)
            {
                return Fail(ex);
            }
        }

        [HttpPost("{id:int}/confirm-manual-refund")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ConfirmManualRefund(int id, [FromBody] ConfirmManualRefundDto dto)
        {
            try
            {
                var result = await _service.ConfirmManualRefundAsync(CurrentUserId, id, dto, await ReviewScopeAsync());

                await _auditLog.LogAsync(CurrentUserId, AuditActorType, "MANUAL_REFUND_CONFIRMED", "TicketReturn", id.ToString(),
                    newValue: new { amount = dto.Amount }, ipAddress: ClientIp);

                return Success(result,
                    "Manual bank refund confirmed.");
            }
            catch (Exception ex)
            {
                return Fail(ex);
            }
        }

        [HttpPost("{id:int}/verify-refund-account")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> VerifyRefundAccount(int id, [FromBody] VerifyRefundAccountDto dto)
        {
            try { return Success(await _service.VerifyRefundAccountAsync(CurrentUserId, id, dto), "Manual account checks recorded. This is not automatic bank ownership verification."); }
            catch (Exception ex) { return Fail(ex); }
        }
    }
}
