using System.Security.Claims;
using AdminAPI.DTOs;
using AdminAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminAPI.Controllers;

[ApiController]
[Route("api/support-cases")]
[Authorize]
public class SupportCaseController : ControllerBase
{
    private readonly ISupportCaseService _service;
    private readonly ILogger<SupportCaseController> _logger;
    private readonly IConfiguration? _configuration;

    public SupportCaseController(ISupportCaseService service, ILogger<SupportCaseController> logger, IConfiguration? configuration = null)
    {
        _service = service;
        _logger = logger;
        _configuration = configuration;
    }

    // Tao yeu cau ho tro moi danh cho Customer va Nguoi dung
    [HttpPost]
    public async Task<IActionResult> CreateCase([FromBody] CreateSupportCaseRequest req)
    {
        var userId = GetCurrentUserId();
        var userName = await GetCurrentUserNameAsync(userId);
        var userEmail = GetCurrentUserEmail();

        // If submitted by a Customer, auto-populate customer identity if missing
        var isInternalStaff = User.IsInRole("Admin") || User.IsInRole("Staff") || User.IsInRole("SupportStaff") || User.IsInRole("SecurityStaff");
        if (!isInternalStaff)
        {
            if (!req.CustomerId.HasValue) req.CustomerId = userId;
            if (string.IsNullOrWhiteSpace(req.CustomerName) || string.Equals(req.CustomerName, "User", StringComparison.OrdinalIgnoreCase)) req.CustomerName = userName;
            if (string.IsNullOrWhiteSpace(req.CustomerEmail)) req.CustomerEmail = userEmail;
        }

        try
        {
            var result = await _service.CreateCaseAsync(req, userId, userName);
            return CreatedAtAction(nameof(GetDetail), new { id = result.CaseId }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // Get order IDs with support reports submitted by the current customer
    [HttpGet("my-reported-orders")]
    public async Task<IActionResult> GetMyReportedOrders()
    {
        var result = await _service.GetReportedOrderIdsAsync(
            GetCurrentUserId(),
            GetCurrentUserEmail());

        return Ok(result);
    }

    // Lay danh sach cac truong hop ho tro danh cho Staff va Admin
    [HttpGet]
    [Authorize(Policy = "CanManageSupportCases")]
    public async Task<IActionResult> GetList([FromQuery] SupportCaseQuery query)
    {
        var userId = GetCurrentUserId();
        var isAdmin = User.IsInRole("Admin");

        var result = await _service.GetCasesAsync(query, userId, isAdmin);
        return Ok(result);
    }

    // Xem thong tin chi tiet truong hop ho tro theo id danh cho Customer, Staff va Admin
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDetail(int id)
    {
        var userId = GetCurrentUserId();
        var isAdmin = User.IsInRole("Admin");
        var isInternalStaff = isAdmin || User.IsInRole("Staff") || User.IsInRole("SupportStaff") || User.IsInRole("SecurityStaff");

        try
        {
            var result = await _service.GetCaseDetailAsync(id, userId, isAdmin);

            // Customers can only access their own case
            if (!isInternalStaff && result.CustomerId != userId && !string.Equals(result.CustomerEmail, GetCurrentUserEmail(), StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(403, new { message = "You do not have permission to view this support case." });
            }

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
    }

    // Lay thong tin ho so ho tro lien ket voi mot don hang cu the danh cho Customer va Staff
    [HttpGet("by-order/{orderId:int}")]
    public async Task<IActionResult> GetCaseByOrder(int orderId)
    {
        var caseItem = await _service.GetCaseByOrderIdAsync(orderId);
        if (caseItem == null)
        {
            return NotFound(new { message = "No support case found for this order." });
        }

        var userId = GetCurrentUserId();
        var isAdmin = User.IsInRole("Admin");
        var isInternalStaff = isAdmin || User.IsInRole("Staff") || User.IsInRole("SupportStaff") || User.IsInRole("SecurityStaff");

        if (!isInternalStaff && caseItem.CustomerId != userId && !string.Equals(caseItem.CustomerEmail, GetCurrentUserEmail(), StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(403, new { message = "You do not have permission to view this support case." });
        }

        return Ok(caseItem);
    }

    // Phan cong truong hop ho tro cho nhan vien xu ly danh cho Admin
    [HttpPost("{id:int}/assign")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AssignStaff(int id, [FromBody] AssignStaffRequest req)
    {
        var adminId = GetCurrentUserId();
        var adminName = await GetCurrentUserNameAsync(adminId);

        try
        {
            var result = await _service.AssignStaffAsync(id, req, adminId, adminName);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // Cannot reassign closed cases
            return BadRequest(new { message = ex.Message });
        }
    }

    // Giai quyet va dong truong hop ho tro danh cho Staff va Admin
    [HttpPost("{id:int}/resolve")]
    [Authorize(Policy = "CanManageSupportCases")]
    public async Task<IActionResult> ResolveCase(int id, [FromBody] ResolveCaseRequest req)
    {
        var userId = GetCurrentUserId();
        var userName = await GetCurrentUserNameAsync(userId);
        var role = User.IsInRole("Admin") ? "Admin" : "Staff";

        try
        {
            var result = await _service.ResolveCaseAsync(id, req, userId, userName, role);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            // Missing resolution
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/dismiss")]
    [Authorize(Policy = "CanManageSupportCases")]
    public async Task<IActionResult> DismissCase(int id, [FromBody] DismissCaseRequest req)
    {
        var userId = GetCurrentUserId();
        var userName = await GetCurrentUserNameAsync(userId);
        var role = User.IsInRole("Admin") ? "Admin" : "Staff";
        try
        {
            return Ok(await _service.DismissCaseAsync(id, req, userId, userName, role));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    // Them ghi chu trao doi vao truong hop ho tro danh cho Customer, Staff va Admin
    [HttpPost("{id:int}/notes")]
    public async Task<IActionResult> AddNote(int id, [FromBody] AddSupportCaseNoteRequest req)
    {
        var userId = GetCurrentUserId();
        var userName = await GetCurrentUserNameAsync(userId);
        var isInternalStaff = User.IsInRole("Admin") || User.IsInRole("Staff") || User.IsInRole("SupportStaff") || User.IsInRole("SecurityStaff");
        var role = User.IsInRole("Admin") ? "Admin" : (isInternalStaff ? "Staff" : "Customer");
        try
        {
            if (!isInternalStaff)
            {
                var ownCase = await _service.GetCaseDetailAsync(id, userId, false);
                if (ownCase.CustomerId != userId && !string.Equals(ownCase.CustomerEmail, GetCurrentUserEmail(), StringComparison.OrdinalIgnoreCase))
                    return StatusCode(403, new { message = "You can only add a note to your own support case." });
            }
            var result = await _service.AddNoteAsync(id, req, userId, userName, role);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
    }
    // Helper: Extract current user id from JWT claims
    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? User.FindFirst("id")?.Value;

        return int.TryParse(claim, out var id) ? id : 0;
    }

    // Helper: Extract current user name from JWT claims or fallback to authentication database
    private async Task<string> GetCurrentUserNameAsync(int userId)
    {
        var name = User.FindFirst("fullName")?.Value
        ?? User.FindFirst("name")?.Value
        ?? User.FindFirst(ClaimTypes.Name)?.Value;

        if (!string.IsNullOrWhiteSpace(name) && !string.Equals(name, "User", StringComparison.OrdinalIgnoreCase))
        {
            return name.Trim();
        }

        if (userId > 0)
        {
            try
            {
                var authConnStr = _configuration?.GetConnectionString("AuthenticationConnection")
                                   ?? _configuration?.GetConnectionString("AuthenticationDb")
                                   ?? Environment.GetEnvironmentVariable("CONCERTSHIELD_AUTH_POSTGRES")
                                   ?? Environment.GetEnvironmentVariable("CONCERTSHIELD_TEST_POSTGRES");
                if (string.IsNullOrWhiteSpace(authConnStr)) return GetCurrentUserEmail()?.Split('@')[0] ?? "User";
                using var conn = new Npgsql.NpgsqlConnection(authConnStr);
                await conn.OpenAsync();
                using var cmd = new Npgsql.NpgsqlCommand("SELECT full_name FROM users WHERE user_id = @uid LIMIT 1;", conn);
                cmd.Parameters.AddWithValue("uid", userId);
                var dbName = await cmd.ExecuteScalarAsync() as string;
                if (!string.IsNullOrWhiteSpace(dbName))
                {
                    return dbName.Trim();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch user name from authentication_db for user {UserId}", userId);
            }
        }

        var email = GetCurrentUserEmail();
        if (!string.IsNullOrWhiteSpace(email))
        {
            return email.Split('@')[0];
        }

        return "User";
    }

    // Helper: Extract current user email from JWT claims
    private string GetCurrentUserEmail()
    {
        return User.FindFirst(ClaimTypes.Email)?.Value
        ?? User.FindFirst("email")?.Value
        ?? "";
    }

    [HttpGet("ticket-disputes")]
    [Authorize(Policy = "CanManageSupportCases")]
    public async Task<IActionResult> GetTicketDisputes([FromQuery] SupportCaseQuery query)
    {
        var userId = GetCurrentUserId();
        var isAdmin = User.IsInRole("Admin");

        var result = await _service.GetTicketDisputesAsync(
            query,
            userId,
            isAdmin);

        return Ok(result);
    }
}
