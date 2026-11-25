using AuthenticationAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthenticationAPI.Controllers
{
    /// <summary>
    /// Internal only: AdminAPI ends every session of an account that was just blocked
    /// (BR-112). Callers must present the shared internal API key.
    /// </summary>
    [ApiController]
    [Route("api/internal/users")]
    [AllowAnonymous]
    public class InternalSessionController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IConfiguration _configuration;

        public InternalSessionController(IUserService userService, IConfiguration configuration)
        {
            _userService = userService;
            _configuration = configuration;
        }

        // POST api/internal/users/5/revoke-sessions
        [HttpPost("{userId:int}/revoke-sessions")]
        public async Task<IActionResult> RevokeSessions(int userId)
        {
            var expectedKey = _configuration["InternalApiKey"];
            var suppliedKey = Request.Headers["X-Internal-Api-Key"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(expectedKey) || suppliedKey != expectedKey)
                return Unauthorized();

            var revoked = await _userService.RevokeAllSessionsAsync(userId);
            return Ok(new { revoked });
        }
    }
}
