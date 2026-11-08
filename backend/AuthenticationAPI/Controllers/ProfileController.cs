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
        private readonly ILogger<ProfileController> _logger;

        public ProfileController(
            IProfileService profileService,
            ILogger<ProfileController> logger)
        {
            _profileService = profileService;
            _logger = logger;
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
            if (dto == null)
            {
                return BadRequest(new
                {
                    message = "Profile update request is required."
                });
            }

            var email =
                User.FindFirst(JwtRegisteredClaimNames.Email)?.Value
                ?? User.FindFirst(ClaimTypes.Email)?.Value
                ?? User.FindFirst("email")?.Value;

            var userId =
                User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value;

            try
            {
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while updating profile");
                return StatusCode(500, new
                {
                    message = "An unexpected error occurred while updating the profile. Please try again later."
                });
            }
        }

        [HttpPost("me/avatar")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(5 * 1024 * 1024)]
        public async Task<IActionResult> UploadAvatar(IFormFile file)
        {
            var value = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(value, out var userId)) return Unauthorized(new { message = "User id claim is missing." });
            try { return Ok(await _profileService.UploadAvatarAsync(userId, file)); }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        }
    }
}