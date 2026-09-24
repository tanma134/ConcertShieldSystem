using AuthenticationAPI.DTOs;

namespace AuthenticationAPI.Services
{
    public interface IKycService
    {
        Task<EkycSubmitResponseDto> SubmitAsync(EkycSubmitRequestDto request, int userId);
        Task<EkycStatusResponseDto> GetStatusAsync(int ekycId, int userId);
    }
}
