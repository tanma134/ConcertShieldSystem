using AuthenticationAPI.Models;

namespace AuthenticationAPI.Repositories
{
    public interface IEkycRepository
    {
        Task<int> CreateAsync(EkycVerification record);

        /// <summary>
        /// Lưu record. Nếu truyền userEkycStatus thì cập nhật luôn User.EkycStatus trong cùng 1 lần SaveChanges
        /// (không bao giờ hạ cấp user đang ở trạng thái "Passed").
        /// </summary>
        Task UpdateAsync(EkycVerification record, string? userEkycStatus = null);

        Task<EkycVerification?> GetByIdAsync(int ekycId);

        /// <summary>Record mới nhất của user có trạng thái Passed hoặc ManualReview (null nếu chưa có).</summary>
        Task<EkycVerification?> GetActiveByUserAsync(int userId);

        /// <summary>CCCD (theo hash) đã được tài khoản KHÁC xác thực/đang chờ duyệt chưa.</summary>
        Task<bool> IsCccdUsedByAnotherUserAsync(string cccdHash, int userId);

        /// <summary>N record mới nhất của user (mới nhất trước), dùng để đếm số lần thất bại liên tiếp (BR-03).</summary>
        Task<List<EkycVerification>> GetRecentByUserAsync(int userId, int take);
    }
}