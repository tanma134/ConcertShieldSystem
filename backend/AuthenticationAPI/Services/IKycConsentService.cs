using AuthenticationAPI.DTOs;

namespace AuthenticationAPI.Services
{
    public interface IKycConsentService
    {
        /// <summary>Nội dung đồng ý đang hiệu lực (public, đã thay placeholder).</summary>
        Task<KycConsentPublicDto> GetActiveAsync();
        Task<string> GetActiveVersionAsync();

        Task<List<KycConsentVersionDto>> ListAsync();
        Task<KycConsentVersionDto> GetAsync(int id);
        Task<KycConsentVersionDto> CreateAsync(CreateKycConsentVersionDto dto, int? adminId);
        Task ActivateAsync(int id);
        Task DeleteAsync(int id);
    }

}
