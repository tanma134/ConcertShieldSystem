using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentAPI.DTOs;
using PaymentAPI.Services;

namespace PaymentAPI.Controllers
{
    [ApiController]
    [Route("api/refunds")]
    [Authorize]
    public class RefundsController : BaseApiController
    {
        private readonly IRefundService _refundService;

        public RefundsController(IRefundService refundService)
        {
            _refundService = refundService;
        }

        private string CurrentUserName
        {
            get
            {
                return User.FindFirst("fullName")?.Value
                    ?? User.FindFirst(ClaimTypes.Name)?.Value
                    ?? User.FindFirst(ClaimTypes.Email)?.Value
                    ?? "Staff";
            }
        }

        private bool IsStaffOrAdmin => IsAdmin
            || User.IsInRole("Staff")
            || User.IsInRole("SupportStaff")
            || User.IsInRole("SecurityStaff")
            || User.Claims.Any(c => (c.Type == "role" || c.Type == ClaimTypes.Role || c.Type.EndsWith("/role")) &&
                (c.Value.Equals("Staff", StringComparison.OrdinalIgnoreCase) ||
                 c.Value.Equals("SupportStaff", StringComparison.OrdinalIgnoreCase) ||
                 c.Value.Equals("SecurityStaff", StringComparison.OrdinalIgnoreCase)));

        // UC 3.16.20: Process Full / Partial Refund
        [HttpPost("process")]
        public async Task<IActionResult> ProcessRefund([FromBody] ProcessRefundRequestDto request, CancellationToken ct)
        {
            if (!IsStaffOrAdmin)
                return StatusCode(403, ApiResponseDTO<object>.FailResponse("You do not have permission to process refunds."));

            try
            {
                var result = await _refundService.ProcessRefundAsync(request, CurrentUserId, CurrentUserName, ct);
                if (result.Status == "Refunded")
                {
                    return Ok(ApiResponseDTO<RefundResultDto>.SuccessResponse(result, "Refund processed successfully."));
                }
                else
                {
                    return BadRequest(ApiResponseDTO<RefundResultDto>.FailResponse(
                        result.Error ?? "Refund request failed or timed out at the payment gateway. The attempt is logged for review/retry.",
                        new List<string> { result.ErrorCode ?? "E5003" }));
                }
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponseDTO<object>.FailResponse(ex.Message));
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }

        // UC 3.16.21: Retry Failed Refund
        [HttpPost("{id:int}/retry")]
        public async Task<IActionResult> RetryRefund(int id, [FromBody] RetryRefundRequestDto? request, CancellationToken ct)
        {
            if (!IsStaffOrAdmin)
                return StatusCode(403, ApiResponseDTO<object>.FailResponse("You do not have permission to retry refunds."));

            try
            {
                var strategy = request?.Strategy ?? "Immediate";
                var result = await _refundService.RetryRefundAsync(id, strategy, CurrentUserId, CurrentUserName, ct);
                if (result.Status == "Refunded")
                {
                    return Ok(ApiResponseDTO<RefundResultDto>.SuccessResponse(result, "Refund retry succeeded."));
                }
                else
                {
                    return BadRequest(ApiResponseDTO<RefundResultDto>.FailResponse(
                        result.Error ?? "Retry failed. The refund remains available for permitted follow-up handling.",
                        new List<string> { result.ErrorCode ?? "E5003" }));
                }
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }

        // UC 3.16.22: View Refund Reconciliation Report (Platform Admin)
        [HttpGet("reconciliation")]
        public async Task<IActionResult> GetReconciliationReport([FromQuery] ReconciliationFilterDto filter, CancellationToken ct)
        {
            if (!IsAdmin)
                return StatusCode(403, ApiResponseDTO<object>.FailResponse("You do not have permission to view financial reconciliation data."));

            try
            {
                var report = await _refundService.GetReconciliationReportAsync(filter, ct);
                return Ok(ApiResponseDTO<RefundReconciliationReportDto>.SuccessResponse(report));
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }

        // Order refund summary (eligibility, balances, history)
        [HttpGet("order/{orderId:int}")]
        public async Task<IActionResult> GetOrderRefundSummary(int orderId, CancellationToken ct)
        {
            if (!IsStaffOrAdmin)
                return StatusCode(403, ApiResponseDTO<object>.FailResponse("Access denied."));

            try
            {
                var summary = await _refundService.GetOrderRefundSummaryAsync(orderId, ct);
                return Ok(ApiResponseDTO<OrderRefundSummaryDto>.SuccessResponse(summary));
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }

        // Single refund detail
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetRefundDetail(int id, CancellationToken ct)
        {
            if (!IsStaffOrAdmin)
                return StatusCode(403, ApiResponseDTO<object>.FailResponse("Access denied."));

            try
            {
                var item = await _refundService.GetRefundByIdAsync(id, ct);
                if (item == null)
                    return NotFound(ApiResponseDTO<object>.FailResponse($"Refund #{id} not found."));

                return Ok(ApiResponseDTO<RefundItemDto>.SuccessResponse(item));
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }
    }
}
