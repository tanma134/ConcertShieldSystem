using AuthenticationAPI.DTOs;

namespace AuthenticationAPI.Services
{
    public interface IKycSettingService
    {
        Task<KycSettingDto> GetAsync();
        Task<KycSettingDto> UpdateAsync(UpdateKycSettingDto dto, int? adminId);
    }
}
