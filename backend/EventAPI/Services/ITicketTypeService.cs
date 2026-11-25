using EventAPI.DTOs;

namespace EventAPI.Services
{
    public interface ITicketTypeService
    {
        // Lay danh sach cac hang ve da cau hinh cua concert
        Task<List<TicketTypeResponseDTO>> GetByEventIdAsync(int eventId, int? callerId, bool isAdmin);

        // Tao moi mot hang ve cho concert o trang thai Draft hoac Rejected
        Task<TicketTypeResponseDTO> CreateAsync(int eventId, CreateTicketTypeDTO dto, int callerId, bool isAdmin);

        // Cap nhat thong tin hang ve nhu ten, gia, so luong hoac gioi han mua
        Task<TicketTypeResponseDTO> UpdateAsync(int ticketTypeId, UpdateTicketTypeDTO dto, int callerId, bool isAdmin);

        // Xoa hang ve khoi concert khi chua phat sinh ve ban ra
        Task DeleteAsync(int ticketTypeId, int callerId, bool isAdmin);
    }
}
