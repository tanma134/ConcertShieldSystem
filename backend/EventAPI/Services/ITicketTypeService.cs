using EventAPI.DTOs;

namespace EventAPI.Services
{
    public interface ITicketTypeService
    {
        Task<List<TicketTypeResponseDTO>> GetByEventIdAsync(int eventId, int? callerId, bool isAdmin);
        Task<TicketTypeResponseDTO> CreateAsync(int eventId, CreateTicketTypeDTO dto, int callerId, bool isAdmin);
        Task<TicketTypeResponseDTO> UpdateAsync(int ticketTypeId, UpdateTicketTypeDTO dto, int callerId, bool isAdmin);
        Task DeleteAsync(int ticketTypeId, int callerId, bool isAdmin);

        // Atomically reserves/sells <paramref name="quantity"/> units of a ticket type's
        // quota. Intended to be called by the Booking/Payment side of the system (once it
        // exists) at the moment a hold is placed or a sale is confirmed — never directly
        // by a customer-facing endpoint. Does not throw on "sold out"; returns
        // Success = false with a Reason instead, since that is an expected outcome under
        // contention, not a server error.

        Task<InventoryOperationResultDTO> ReserveInventoryAsync(int ticketTypeId, int quantity);

        // Releases previously reserved/sold quota (timeout, payment failure, refund, cancellation).
        Task<InventoryOperationResultDTO> ReleaseInventoryAsync(int ticketTypeId, int quantity);
    }
}
