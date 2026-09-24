using AuthenticationAPI.DTOs;

namespace AuthenticationAPI.Services
{
    public interface IKycAccessLogService
    {
        Task LogAsync(int subjectUserId, int? ekycId, int? actorUserId, string actorType,
                      string action, string? ipAddress = null, string? details = null);

        Task<KycPagedResult<KycAccessLogDto>> SearchAsync(KycAccessLogQuery query);
    }
}
