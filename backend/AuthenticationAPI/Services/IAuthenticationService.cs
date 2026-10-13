using AuthenticationAPI.DTOs;

namespace AuthenticationAPI.Services
{
    public interface IAuthenticationService
    {
        Task RegisterAsync(RegisterDTO dto);
        Task VerifyEmailAsync(VerifyEmailDTO dto);
        Task<LoginResponseDTO> LoginAsync(LoginDTO dto, string? ipAddress = null, string? userAgent = null);
        Task<RefreshResponseDTO> RefreshTokenAsync(RefreshTokenDTO dto, string? ipAddress = null, string? userAgent = null);
        Task LogoutAsync(LogoutDTO dto);
        Task ForgotPasswordAsync(ForgotPasswordDTO dto);
        Task<VerifyResetOtpResponseDTO> VerifyResetOtpAsync(VerifyResetOtpDTO dto);
        Task ResetPasswordAsync(ResetPasswordDTO dto, string? ipAddress = null, string? userAgent = null);
        Task<LoginResponseDTO> GoogleLoginAsync(GoogleLoginDTO dto, string? ipAddress = null, string? userAgent = null);
        Task ChangePasswordAsync(int userId, ChangePasswordDTO dto, string? ipAddress = null, string? userAgent = null);
    }
}