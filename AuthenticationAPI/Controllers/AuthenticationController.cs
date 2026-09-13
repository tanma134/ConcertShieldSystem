using AuthenticationAPI.DTOs;
using AuthenticationAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace AuthenticationAPI.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthenticationController : ControllerBase
    {
        private readonly IAuthenticationService _authService;
        private readonly ILogger<AuthenticationController> _logger;

        public AuthenticationController(
            IAuthenticationService authService,
            ILogger<AuthenticationController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDTO dto)
        {
            try
            {
                await _authService.RegisterAsync(dto);
                return Ok(new { message = "Registration successful. Please check your email for the verification OTP code." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during registration for email {Email}", dto?.Email);
                return StatusCode(500, new { message = "An unexpected error occurred while processing your registration. Please try again later." });
            }
        }

        [HttpPost("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailDTO dto)
        {
            try
            {
                await _authService.VerifyEmailAsync(dto);
                return Ok(new { message = "Email verified successfully. Your account has been activated." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during email verification for email {Email}", dto?.Email);
                return StatusCode(500, new { message = "An unexpected error occurred while verifying your email. Please try again later." });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO dto)
        {
            try
            {
                var result = await _authService.LoginAsync(dto);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during login for email {Email}", dto?.Email);
                return StatusCode(500, new { message = "An unexpected error occurred while logging in. Please try again later." });
            }
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenDTO dto)
        {
            try
            {
                var result = await _authService.RefreshTokenAsync(dto);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during token refresh");
                return StatusCode(500, new { message = "An unexpected error occurred while refreshing your session. Please try again later." });
            }
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] LogoutDTO dto)
        {
            try
            {
                await _authService.LogoutAsync(dto);
                return Ok(new { message = "Logged out successfully." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during logout");
                return StatusCode(500, new { message = "An unexpected error occurred while logging out. Please try again later." });
            }
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDTO dto)
        {
            try
            {
                await _authService.ForgotPasswordAsync(dto);
                return Ok(new { message = "If this email is registered, you will receive an OTP code to reset your password." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during forgot-password request for email {Email}", dto?.Email);
                return StatusCode(500, new { message = "An unexpected error occurred while processing your request. Please try again later." });
            }
        }

        [HttpPost("verify-reset-otp")]
        public async Task<IActionResult> VerifyResetOtp([FromBody] VerifyResetOtpDTO dto)
        {
            try
            {
                var result = await _authService.VerifyResetOtpAsync(dto);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during reset-OTP verification for email {Email}", dto?.Email);
                return StatusCode(500, new { message = "An unexpected error occurred while verifying the OTP. Please try again later." });
            }
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDTO dto)
        {
            try
            {
                await _authService.ResetPasswordAsync(dto);
                return Ok(new { message = "Password reset successfully. You can now log in with your new password." });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during password reset for email {Email}", dto?.Email);
                return StatusCode(500, new { message = "An unexpected error occurred while resetting your password. Please try again later." });
            }
        }
    }
}