using System.Security.Claims;
using AdminAPI.DTOs;
using AdminAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminAPI.Controllers;

// Coordinates customer support case & dispute resolution operations
[ApiController]
[Route("api/support-cases")]
[Authorize]
public class SupportCaseController : ControllerBase
{
 private readonly ISupportCaseService _service;
 private readonly ILogger<SupportCaseController> _logger;

 public SupportCaseController(ISupportCaseService service, ILogger<SupportCaseController> logger)
 {
 _service = service;
 _logger = logger;
 }

 // Create a new customer support case (Customers can dispute their orders, Staff/Admin can create on behalf)
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

 // List support cases (Staff sees assigned or unassigned cases; Admin sees all)
 [HttpGet]
 [Authorize(Policy = "CanManageSupportCases")]
 public async Task<IActionResult> GetList([FromQuery] SupportCaseQuery query)
 {
 var userId = GetCurrentUserId();
 var isAdmin = User.IsInRole("Admin");

 var result = await _service.GetCasesAsync(query, userId, isAdmin);
 return Ok(result);
 }

 // View support case details with activity notes timeline
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

 // Retrieve support case associated with a specific order
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

 // Assign support case to staff (Admin only)
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
 // : Cannot reassign closed cases
 return BadRequest(new { message = ex.Message });
 }
 }

 // Resolve and close support case (Resolution strictly required)
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

 // Append internal investigation note or customer follow-up
 [HttpPost("{id:int}/notes")]
 public async Task<IActionResult> AddNote(int id, [FromBody] AddSupportCaseNoteRequest req)
 {
 var userId = GetCurrentUserId();
 var userName = await GetCurrentUserNameAsync(userId);
 var role = User.IsInRole("Admin") ? "Admin" : (User.IsInRole("Staff") ? "Staff" : "Customer");

 try
 {
 var result = await _service.AddNoteAsync(id, req, userId, userName, role);
 return Ok(result);
 }
 catch (KeyNotFoundException ex)
 {
 return NotFound(new { message = ex.Message });
 }
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
 using var conn = new Npgsql.NpgsqlConnection("Host=localhost;Port=5432;Database=authentication_db;Username=postgres;Password=123456");
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
}
