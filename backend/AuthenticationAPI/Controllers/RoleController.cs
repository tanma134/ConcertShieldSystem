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

        public RoleController(IRoleService roleService)
        {
            _roleService = roleService;
        }

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

            return Ok(result.Data);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _roleService.DeleteAsync(id);

            if (!result.Success)
            {
                return NotFound(new
                {
                    message = result.Message
                });
            }

            return NoContent();
        }
    }
}