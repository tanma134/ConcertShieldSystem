// AdminAPI/Controllers/AuditLogAdminController.cs
using System.Security.Claims;
using AdminAPI.DTOs;
using AdminAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminAPI.Controllers
{
    [ApiController]
    [Route("api/admin/audit-logs")]
    [Authorize(Policy = "CanManageUsers")]
    public class AuditLogAdminController : ControllerBase
    {
        private readonly IAuditLogService _service;
        private readonly ILogger<AuditLogAdminController> _logger;

        public AuditLogAdminController(IAuditLogService service, ILogger<AuditLogAdminController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Search([FromQuery] AuditLogQuery query)
        {
            try
            {
                return Ok(await _service.SearchAsync(query));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while searching audit logs");
                return StatusCode(500, new { message = "An unexpected error occurred. Please try again later." });
            }
        }


        [HttpGet("export")]
        public async Task<IActionResult> Export([FromQuery] AuditLogQuery query, [FromQuery] string format = "csv")
        {
            try
            {
                var (content, contentType, fileName) = await _service.ExportAsync(query, format);

                await _service.LogAsync(
                    userId: GetCurrentUserId(),
                    actorType: "admin",
                    action: "AUDIT_LOG_EXPORTED",
                    entityType: "AuditLog",
                    newValue: new { query.From, query.To, format },
                    ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                    userAgent: Request.Headers.UserAgent.ToString());

                return File(content, contentType, fileName);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while exporting audit logs");
                return StatusCode(500, new { message = "An unexpected error occurred. Please try again later." });
            }
        }

        private int? GetCurrentUserId() =>
            int.TryParse((User.FindFirst("userId") ?? User.FindFirst(ClaimTypes.NameIdentifier))?.Value, out var id)
                ? id : null;
    }
}