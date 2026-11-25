using EventAPI.DTOs;

namespace EventAPI.Services
{
    // Dinh nghia cac phuong thuc quan ly quy tac gia dong ap dung tren hang ve
    public interface IPricingRuleService
    {
        // Lay danh sach cac quy tac gia dong ap dung cho mot hang ve
        Task<List<PricingRuleResponseDTO>> GetByTicketTypeIdAsync(int ticketTypeId, int? callerId, bool isAdmin);

        // Tao quy tac gia dong nhu EarlyBird hoac LastMinute cho hang ve
        Task<PricingRuleResponseDTO> CreateAsync(int ticketTypeId, CreatePricingRuleDTO dto, int callerId, bool isAdmin);

        // Cap nhat thong tin quy tac gia dong cua hang ve
        Task<PricingRuleResponseDTO> UpdateAsync(int pricingRuleId, UpdatePricingRuleDTO dto, int callerId, bool isAdmin);

        // Xoa quy tac gia dong khoi hang ve
        Task DeleteAsync(int pricingRuleId, int callerId, bool isAdmin);
    }
}
