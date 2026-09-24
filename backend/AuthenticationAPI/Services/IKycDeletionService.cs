using AuthenticationAPI.DTOs;

namespace AuthenticationAPI.Services
{
    public interface IKycDeletionService
    {
        // Người dùng
        Task<KycDeletionRequestDto> RequestAsync(int userId, string? ip);
        Task<List<KycDeletionRequestDto>> GetMineAsync(int userId);

        // Admin
        Task<KycPagedResult<KycDeletionRequestDto>> ListAsync(string? status, int page, int pageSize);
        Task<KycDeletionRequestDto> ApproveAsync(int requestId, int? adminId, string? ip);
        Task<KycDeletionRequestDto> RejectAsync(int requestId, int? adminId, string note, string? ip);
    }
}
