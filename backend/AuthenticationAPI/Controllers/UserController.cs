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

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

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
            [FromBody] UserDTO dto)
        {
            var result = await _userService.UpdateUserAsync(id, dto);

            if (!result.Success)
            {
                if (result.Message == "User not found.")
                    return NotFound(new
                    {
                        message = result.Message
                    });

                return BadRequest(new
                {
                    message = result.Message
                });
            }

            return NoContent();
        }

        // Block / Unblock User
        [HttpPut("users/{id}/status")]
        public async Task<IActionResult> UpdateUserStatus(
            int id,
            [FromBody] int status)
        {
            var result =
                await _userService.UpdateUserStatusAsync(id, status);

            if (!result.Success)
            {
                if (result.Message == "User not found.")
                    return NotFound(new
                    {
                        message = result.Message
                    });

                return BadRequest(new
                {
                    message = result.Message
                });
            }

            return Ok(new
            {
                message = result.Message,
                isActive = result.IsActive
            });
        }
    }
}