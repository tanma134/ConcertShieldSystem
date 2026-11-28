using AuthenticationAPI.DTOs;
using AuthenticationAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthenticationAPI.Controllers
{
    [ApiController]
    [Route("api/auth")]
    [Authorize(Policy = "CanManageUsers")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IAuditLogClient _auditLog;

        public UserController(IUserService userService, IAuditLogClient auditLog)
        {
            _userService = userService;
            _auditLog = auditLog;
        }

        private int? ActorId =>
            int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value, out var id) ? id : null;

        private Task AuditAsync(string action, int targetUserId, object? newValue = null) =>
            _auditLog.LogAsync(ActorId, "admin", action, "User", targetUserId.ToString(),
                newValue: newValue,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                userAgent: Request.Headers.UserAgent.ToString());

        // View List User
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _userService.GetUsersAsync();

            return Ok(users);
        }

        // View Detail User
        [HttpGet("users/{id}")]
        public async Task<IActionResult> GetUser(int id)
        {
            var user = await _userService.GetUserAsync(id);

            if (user == null)
                return NotFound(new
                {
                    message = "User not found."
                });

            return Ok(user);
        }

        // Update User
        [HttpPut("users/{id}")]
        public async Task<IActionResult> UpdateUser(
            int id,
            [FromBody] UpdateUserDTO dto)
        {
            var currentUserIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value;
            int? currentUserId = int.TryParse(currentUserIdString, out var parsedCurrentUserId) ? parsedCurrentUserId : null;

            var result = await _userService.UpdateUserAsync(id, dto, currentUserId);

            if (!result.Success)
            {
                if (result.Message == "User not found.")
                    return NotFound(new { message = result.Message });

                if (result.Message == "Admin accounts cannot be modified.")
                    return Conflict(new { code = "ADMIN_ACCOUNT_PROTECTED", message = result.Message });

                return BadRequest(new { message = result.Message });
            }

            await AuditAsync("USER_UPDATED_BY_ADMIN", id, new { fields = dto });
            return NoContent();
        }

        [HttpPost("users/{id}/assign-role")]
        public async Task<IActionResult> AssignRole(int id, [FromBody] UserRoleAssignmentDTO dto)
        {
            var currentUserIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value;
            int? currentUserId = int.TryParse(currentUserIdString, out var parsedCurrentUserId) ? parsedCurrentUserId : null;

            var result = await _userService.AssignRoleAsync(id, dto.RoleId, currentUserId);

            if (!result.Success)
            {
                if (result.Message == "User not found.")
                    return NotFound(new { message = result.Message });

                if (result.Message == "Role assignment is not available for Admin or Organizer accounts.")
                    return Conflict(new { code = "ADMIN_ACCOUNT_PROTECTED", message = result.Message });

                return BadRequest(new { message = result.Message });
            }

            await AuditAsync("USER_ROLE_ASSIGNED", id, new { roleId = dto.RoleId });
            return Ok(result.Data);
        }

        /// <summary>Additively grants a role. Repeated grants are idempotent.</summary>
        [HttpPost("users/{id}/roles")]
        public async Task<IActionResult> AddRole(int id, [FromBody] AddRoleDTO dto)
        {
            var result = await _userService.AddRoleAsync(id, dto.RoleName);

            if (!result.Success)
            {
                if (result.Message == "User not found.")
                    return NotFound(new { message = result.Message });

                if (result.Message == "Admin accounts cannot be modified.")
                    return Conflict(new { code = "ADMIN_ACCOUNT_PROTECTED", message = result.Message });

                return BadRequest(new { message = result.Message });
            }

            await AuditAsync("USER_ROLE_ADDED", id, new { roleName = dto.RoleName });
            return Ok(new
            {
                message = result.Message,
                roles = result.Data?.Roles.Select(role => role.RoleName).ToList() ?? new List<string>()
            });
        }

        // Block / Unblock User
        [HttpPut("users/{id}/status")]
        public async Task<IActionResult> UpdateUserStatus(
            int id,
            [FromBody] int status)
        {
            var currentUserIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value;
            int? currentUserId = int.TryParse(currentUserIdString, out var parsedCurrentUserId) ? parsedCurrentUserId : null;

            var result = await _userService.UpdateUserStatusAsync(id, status, currentUserId);

            if (!result.Success)
            {
                if (result.Message == "User not found.")
                    return NotFound(new { message = result.Message });

                if (result.Message == "Admin accounts cannot be blocked or unblocked.")
                    return Conflict(new { code = "ADMIN_ACCOUNT_PROTECTED", message = result.Message });

                return BadRequest(new { message = result.Message });
            }

            await AuditAsync(result.IsActive == true ? "USER_UNLOCKED" : "USER_LOCKED", id,
                new { status, isActive = result.IsActive });
            return Ok(new
            {
                message = result.Message,
                isActive = result.IsActive
            });
        }
    }
}
