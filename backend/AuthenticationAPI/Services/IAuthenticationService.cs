using AuthenticationAPI.DTOs;

namespace AuthenticationAPI.Services
{
    public interface IAuthenticationService
    {
        Task RegisterAsync(RegisterDTO dto);
        Task VerifyEmailAsync(VerifyEmailDTO dto);
        Task<LoginResponseDTO> LoginAsync(LoginDTO dto);
        Task<RefreshResponseDTO> RefreshTokenAsync(RefreshTokenDTO dto);
        Task LogoutAsync(LogoutDTO dto);
        Task ForgotPasswordAsync(ForgotPasswordDTO dto);
        Task<VerifyResetOtpResponseDTO> VerifyResetOtpAsync(VerifyResetOtpDTO dto);
        Task ResetPasswordAsync(ResetPasswordDTO dto);
    }
}