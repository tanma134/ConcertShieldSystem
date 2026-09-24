using EventAPI.DTOs;

namespace EventAPI.Services
{
    public interface ITicketTypeService
    {
        Task<List<TicketTypeResponseDTO>> GetByEventIdAsync(int eventId, int? callerId, bool isAdmin);
        Task<TicketTypeResponseDTO> CreateAsync(int eventId, CreateTicketTypeDTO dto, int callerId, bool isAdmin);
        Task<TicketTypeResponseDTO> UpdateAsync(int ticketTypeId, UpdateTicketTypeDTO dto, int callerId, bool isAdmin);
        Task DeleteAsync(int ticketTypeId, int callerId, bool isAdmin);
    }
}
