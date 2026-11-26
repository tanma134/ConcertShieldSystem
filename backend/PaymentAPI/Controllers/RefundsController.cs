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
        private readonly IAuditLogClient _auditLog;

        public RefundsController(IRefundService refundService, IAuditLogClient auditLog)
        {
            _refundService = refundService;
            _auditLog = auditLog;
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
                    _ = _auditLog.LogAsync(CurrentUserId, AuditActorType, "REFUND_PROCESSED", "PaymentRefund",
                        result.RefundId?.ToString(), null,
                        new { request.OrderId, request.RefundType, request.Amount, request.Reason, result.Status, result.Reference },
                        HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), HttpContext.TraceIdentifier);
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
                    _ = _auditLog.LogAsync(CurrentUserId, AuditActorType, "REFUND_RETRIED", "PaymentRefund",
                        id.ToString(), new { Status = "Failed" },
                        new { Strategy = strategy, result.Status, result.Reference },
                        HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), HttpContext.TraceIdentifier);
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
