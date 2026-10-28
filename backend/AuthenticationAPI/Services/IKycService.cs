using AuthenticationAPI.DTOs;

namespace AuthenticationAPI.Services
{
    public interface IKycService
    {
        Task<EkycSubmitResponseDto> SubmitAsync(EkycSubmitRequestDto request, int userId, string? ip = null);
        Task<EkycStatusResponseDto> GetStatusAsync(int ekycId, int userId);
    }
}
