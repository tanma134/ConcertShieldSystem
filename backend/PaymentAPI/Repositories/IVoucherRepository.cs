using System.Collections.Generic;
using System.Threading.Tasks;
using PaymentAPI.DTOs;
using PaymentAPI.Models;

namespace PaymentAPI.Repositories
{
    public interface IVoucherRepository
    {
        Task<PagedResultDTO<Voucher>> GetPagedAsync(
            int page,
            int limit,
            string? scope,
            bool? isActive,
            string? searchCode,
            int? organizerId,
            bool isOrganizerOnly);

        Task<Voucher?> GetByIdAsync(int id, bool includeDeleted = false);
        Task<Voucher?> GetByCodeAsync(string code);
        Task<bool> CodeExistsAsync(string code, int? excludeId = null);
        Task<Voucher> CreateAsync(Voucher voucher);
        Task<Voucher> UpdateAsync(Voucher voucher);
        Task<List<VoucherUsage>> GetUsagesByVoucherIdAsync(int voucherId);
    }
}
