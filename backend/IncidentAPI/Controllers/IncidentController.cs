using System.Security.Claims;
using IncidentAPI.DTOs;
using IncidentAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IncidentAPI.Controllers;

[ApiController]
[Route("api/incidents")]
[Authorize]
public class IncidentController : ControllerBase
{
    private readonly IIncidentService _service;
    private readonly ILogger<IncidentController> _logger;

    public IncidentController(IIncidentService service, ILogger<IncidentController> logger)
    {
        _service = service;
        _logger = logger;
    }

    // Tim kiem va loc danh sach su co hien truong danh cho Staff va Admin
    [HttpGet]
    [Authorize(Policy = "CanManageIncidents")]
    public Task<IActionResult> Search([FromQuery] IncidentQuery query) =>
        Run(async () =>
        {
            var userId = GetCurrentUserId();
            var isAdmin = User.IsInRole("Admin");
            var result = await _service.SearchAsync(query, userId, isAdmin);
            return Ok(result);
        });

    // Xem thong tin chi tiet ho so su co theo id danh cho Staff va Admin
    [HttpGet("{id:int}")]
    [Authorize(Policy = "CanManageIncidents")]
    public Task<IActionResult> GetDetail(int id) =>
        Run(async () =>
        {
            var userId = GetCurrentUserId();
            var isAdmin = User.IsInRole("Admin");
            var result = await _service.GetDetailAsync(id, userId, isAdmin);
            return Ok(result);
        });

    // Bao cao su co phat sinh tai hien truong concert danh cho Staff
    [HttpPost]
    [Authorize(Policy = "CanReportIncident")]
    public Task<IActionResult> Create([FromBody] CreateIncidentRequest req) =>
        Run(async () =>
        {
            // Luu y: Tu dong lay dinh danh Staff lap bao cao va gan trang thai Open
            var staffId = GetCurrentUserId();
            var staffName = GetCurrentUserName();
            var result = await _service.CreateIncidentAsync(req, staffId, staffName);
            return CreatedAtAction(nameof(GetDetail), new { id = result.Id }, result);
        });

    // Cap nhat noi dung chi tiet ho so su co danh cho Staff va Admin
    [HttpPut("{id:int}")]
    [Authorize(Policy = "CanManageIncidents")]
    public Task<IActionResult> Update(int id, [FromBody] UpdateIncidentRequest req) =>
        Run(async () =>
        {
            var userId = GetCurrentUserId();
            var userName = GetCurrentUserName();
            var isAdmin = User.IsInRole("Admin");
            var result = await _service.UpdateIncidentAsync(id, req, userId, userName, isAdmin);
            return Ok(result);
        });

    // Cap nhat nhanh trang thai xu ly su co danh cho Staff va Admin
    [HttpPatch("{id:int}/status")]
    [Authorize(Policy = "CanManageIncidents")]
    public Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusRequest req) =>
        Run(async () =>
        {
            var userId = GetCurrentUserId();
            var userName = GetCurrentUserName();
            var isAdmin = User.IsInRole("Admin");
            var result = await _service.UpdateStatusAsync(id, req, userId, userName, isAdmin);
            return Ok(result);
        });

    // Leo thang ho so su co len Admin xu ly danh cho Staff
    [HttpPost("{id:int}/escalate")]
    [Authorize(Policy = "CanReportIncident")]
    public Task<IActionResult> Escalate(int id, [FromBody] EscalateIncidentRequest req) =>
        Run(async () =>
        {
            // Luu y: Ghi nhan ly do leo thang vao timeline va thong bao den Admin
            var staffId = GetCurrentUserId();
            var staffName = GetCurrentUserName();
            var result = await _service.EscalateIncidentAsync(id, req, staffId, staffName);
            return Ok(result);
        });

    // Giai quyet dut diem su co va dong ho so danh cho Admin
    [HttpPost("{id:int}/resolve")]
    [Authorize(Policy = "CanManageIncidents")]
    public Task<IActionResult> Resolve(int id, [FromBody] ResolveIncidentRequest req) =>
        Run(async () =>
        {
            // Luu y: Ghi nhan phuong an xu ly chinh thuc va cap nhat trang thai Resolved
            var adminId = GetCurrentUserId();
            var adminName = GetCurrentUserName();
            var result = await _service.ResolveIncidentAsync(id, req, adminId, adminName);
            return Ok(result);
        });

    // Xem ket qua va phuong an giai quyet su co danh cho Staff va Admin
    [HttpGet("{id:int}/resolution")]
    [Authorize(Policy = "CanManageIncidents")]
    public Task<IActionResult> GetResolution(int id) =>
        Run(async () =>
        {
            var result = await _service.GetResolutionAsync(id);
            return Ok(result);
        });

    // Xem tien trinh lich su xu ly su co theo dong thoi gian danh cho Staff va Admin
    [HttpGet("{id:int}/history")]
    [Authorize(Policy = "CanManageIncidents")]
    public Task<IActionResult> GetHistory(int id) =>
        Run(async () =>
        {
            var result = await _service.GetHistoryAsync(id);
            return Ok(result);
        });

    private int GetCurrentUserId()
    {
        var raw = (User.FindFirst("userId") ?? User.FindFirst(ClaimTypes.NameIdentifier))?.Value;
        return int.TryParse(raw, out var id)
            ? id
            : throw new IncidentServiceException(401, "Token does not contain a valid userId.");
    }

    private string GetCurrentUserName()
    {
        return User.FindFirst("fullName")?.Value
            ?? User.FindFirst(ClaimTypes.Name)?.Value
            ?? User.FindFirst(ClaimTypes.Email)?.Value
            ?? "Staff";
    }

    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (IncidentServiceException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in incident endpoint");
            return StatusCode(500, new { message = "An unexpected error occurred. Please try again." });
        }
    }
}
