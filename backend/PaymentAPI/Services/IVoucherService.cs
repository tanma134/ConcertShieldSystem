using System.Collections.Generic;
using System.Threading.Tasks;
using PaymentAPI.DTOs;

namespace PaymentAPI.Services
{
    public interface IVoucherService
    {
        Task<VoucherResponseDTO> CreateVoucherAsync(CreateVoucherDTO dto, int currentUserId, bool isAdmin, bool isOrganizer);
        Task<PagedResultDTO<VoucherResponseDTO>> GetVouchersAsync(
            int page,
            int limit,
            string? scope,
            bool? isActive,
            string? searchCode,
            int? organizerId,
            int currentUserId,
            bool isAdmin,
            bool isOrganizer);
        Task<VoucherResponseDTO> GetVoucherByIdAsync(int id, int currentUserId, bool isAdmin, bool isOrganizer);
        Task<List<VoucherUsageDTO>> GetVoucherUsagesAsync(int voucherId, int currentUserId, bool isAdmin, bool isOrganizer);
        Task<VoucherResponseDTO> UpdateVoucherStatusAsync(int id, bool isActive, int currentUserId, bool isAdmin, bool isOrganizer);
        Task<VoucherResponseDTO> UpdateVoucherAsync(int id, UpdateVoucherDTO dto, int currentUserId, bool isAdmin, bool isOrganizer);
        Task<bool> SoftDeleteVoucherAsync(int id, int currentUserId, bool isAdmin, bool isOrganizer);
    }
}
