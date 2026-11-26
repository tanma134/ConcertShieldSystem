using AuthenticationAPI.DTOs;
using AuthenticationAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthenticationAPI.Controllers
{
    [ApiController]
    [Route("api/roles")]
    [Authorize(Policy = "CanManageUsers")]
    public class RoleController : ControllerBase
    {
        private readonly IRoleService _roleService;
        private readonly IAuditLogClient _auditLog;

        public RoleController(IRoleService roleService, IAuditLogClient auditLog)
        {
            _roleService = roleService;
            _auditLog = auditLog;
        }

        private Task AuditAsync(string action, string? roleId, object? newValue = null) =>
            _auditLog.LogAsync(
                int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("sub")?.Value, out var uid) ? uid : null,
                "admin", action, "Role", roleId, newValue: newValue,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                userAgent: Request.Headers.UserAgent.ToString());

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var roles = await _roleService.GetAllAsync();

            return Ok(roles);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var role = await _roleService.GetByIdAsync(id);

            if (role == null)
                return NotFound(new
                {
                    message = "Role not found."
                });

            return Ok(role);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] RoleDTO dto)
        {
            var result = await _roleService.CreateAsync(dto);

            if (!result.Success)
            {
                return BadRequest(new
                {
                    message = result.Message
                });
            }

            if (result.Data is not null)
            {
                var roleId = ((dynamic)result.Data).RoleId;
                await AuditAsync("ROLE_CREATED", roleId.ToString(), dto);
                return CreatedAtAction(nameof(Get), new { id = roleId }, result.Data);
            }

            return Ok(result.Data);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(
            int id,
            [FromBody] RoleDTO dto)
        {
            var result = await _roleService.UpdateAsync(id, dto);

            if (!result.Success)
            {
                if (result.Message == "Role not found.")
                {
                    return NotFound(new
                    {
                        message = result.Message
                    });
                }

                return BadRequest(new
                {
                    message = result.Message
                });
            }

            await AuditAsync("ROLE_UPDATED", id.ToString(), dto);
            return Ok(result.Data);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _roleService.DeleteAsync(id);

            if (!result.Success)
            {
                if (result.Message == "Role not found.")
                {
                    return NotFound(new
                    {
                        message = result.Message
                    });
                }

                return Conflict(new
                {
                    message = result.Message
                });
            }

            await AuditAsync("ROLE_DELETED", id.ToString());
            return NoContent();
        }
    }
}