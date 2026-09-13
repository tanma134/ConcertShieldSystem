using AuthenticationAPI.DTOs;
using AuthenticationAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AuthenticationAPI.Controllers
{
    [ApiController]
    [Route("api/auth")]
    [Authorize(Policy = "User")]
    public class ProfileController : ControllerBase
    {
        private readonly IProfileService _profileService;

        public ProfileController(IProfileService profileService)
        {
            _profileService = profileService;
        }

        // ==========================================
        // GET PROFILE
        // GET /api/auth/me
        // ==========================================
        [HttpGet("me")]
        public async Task<IActionResult> GetProfile()
        {
            var email =
                User.FindFirst(JwtRegisteredClaimNames.Email)?.Value
                ?? User.FindFirst(ClaimTypes.Email)?.Value
                ?? User.FindFirst("email")?.Value;

            var userId =
                User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value;

            var profile =
                await _profileService.GetProfileAsync(
                    email,
                    userId
                );

            if (profile == null)
            {
                return NotFound(new
                {
                    message = "User not found."
                });
            }

            return Ok(profile);
        }

        // ==========================================
        // UPDATE PROFILE
        // PUT /api/auth/me
        // ==========================================
        [HttpPut("me")]
        public async Task<IActionResult> UpdateProfile(
            [FromBody] UpdateProfileDTO dto)
        {
            var email =
                User.FindFirst(JwtRegisteredClaimNames.Email)?.Value
                ?? User.FindFirst(ClaimTypes.Email)?.Value
                ?? User.FindFirst("email")?.Value;

            var userId =
                User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value;

            var result =
                await _profileService.UpdateProfileAsync(
                    email,
                    userId,
                    dto
                );

            if (!result.Success)
            {
                if (result.Message == "User not found.")
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

            return Ok(result.Profile);
        }
    }
}