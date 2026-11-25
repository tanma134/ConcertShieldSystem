using Microsoft.AspNetCore.Mvc;
using TicketAPI.Services;
namespace TicketAPI.Controllers;
// Dong bo va ap dung thay doi lich concert sang TicketAPI (Internal Service Communication)
[ApiController]
[Route("api/internal/event-changes")]
public class EventChangesInternalController(EventChangeService service, IConfiguration config) : ControllerBase
{
    private bool Allowed => !string.IsNullOrEmpty(config["Governance:InternalApiKey"]) && Request.Headers["X-Internal-Api-Key"] == config["Governance:InternalApiKey"];

    // Ap dung thay doi hoan hoac doi ngay vao ve va don hang cua concert
    [HttpPost("apply")]
    public async Task<IActionResult> Apply(ApplyChangeInput input, CancellationToken ct)
    {
        if (!Allowed) return Unauthorized();
        try { await service.ApplyAsync(input, ct); return Ok(new { success = true }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    // Doc danh sach don hang va ve bi anh huong hoac xem truoc tac dong cho Admin va Organizer
    [HttpGet("preview/{eventId:int}")]
    [HttpGet("{changeId:long}")]
    public async Task<IActionResult> Read(long? changeId, int? eventId, string tab = "orders", string? status = null, string? orderCode = null, int? userId = null, int page = 1, int pageSize = 10, CancellationToken ct = default)
    {
        if (!Allowed) return Unauthorized();
        try { return Ok(await service.ReadAsync(changeId, eventId, tab, status, orderCode, userId, page, pageSize, ct)); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }
}
