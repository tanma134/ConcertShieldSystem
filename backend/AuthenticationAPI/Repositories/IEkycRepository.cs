using AuthenticationAPI.Models;

namespace AuthenticationAPI.Repositories
{
    public interface IEkycRepository
    {
        Task<int> CreateAsync(EkycVerification record);

        /// <summary>
        /// Lýu record. N?u truy?n userEkycStatus th? c?p nh?t luôn User.EkycStatus trong cùng 1 l?n SaveChanges
        /// (không bao gi? h? c?p user ðang ? tr?ng thái "Passed").
        /// </summary>
        Task UpdateAsync(EkycVerification record, string? userEkycStatus = null);

        Task<EkycVerification?> GetByIdAsync(int ekycId);

        /// <summary>Record m?i nh?t c?a user có tr?ng thái Passed ho?c ManualReview (null n?u chýa có).</summary>
        Task<EkycVerification?> GetActiveByUserAsync(int userId);

        /// <summary>CCCD (theo hash) ð? ðý?c tài kho?n KHÁC xác th?c/ðang ch? duy?t chýa.</summary>
        Task<bool> IsCccdUsedByAnotherUserAsync(string cccdHash, int userId);

        /// <summary>N record m?i nh?t c?a user (m?i nh?t trý?c), dùng ð? ð?m s? l?n th?t b?i liên ti?p .</summary>
        Task<List<EkycVerification>> GetRecentByUserAsync(int userId, int take);
    }
}