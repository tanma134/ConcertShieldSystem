using AuthenticationAPI.Models;

namespace AuthenticationAPI.Services
{
    public interface IKycDataPurger
    {
        Task PurgeAsync(EkycVerification record, bool keepDocumentHash, CancellationToken ct = default);
    }
}
