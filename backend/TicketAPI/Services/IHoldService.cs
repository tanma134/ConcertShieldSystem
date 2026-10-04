using TicketAPI.DTOs;

public interface IHoldService
{
    Task<bool> TryHoldSeatAsync(
        int eventId, int seatId, int userId, int durationSeconds = 600);

    Task<bool> ReleaseSeatAsync(int eventId, int seatId, int userId);

    Task<bool> TryHoldTicketsWithCapacityAsync(int ticketTypeId, int userId, int quantity, int availableCapacity, int durationSeconds);

    Task<bool> ReleaseTicketsAsync(
        int ticketTypeId, int userId, int quantity);

    Task<SeatHoldInfo?> GetHoldInfoAsync(int eventId, int seatId);
    Task<TicketHoldInfo?> GetTicketHoldInfoAsync(int ticketTypeId, int userId);

    Task<HoldSessionResponse> CreateHoldSessionAsync(
        int userId,
        CreateHoldSessionRequest request,
        int durationSeconds = 600);

    Task<HoldSessionResponse> GetHoldDetailsAsync(string holdId, int userId);

    Task<List<int>> GetHeldSeatIdsAsync(int eventId, IReadOnlyCollection<int> seatIds);

    Task<HoldSessionResponse> SaveAttendeesAsync(string holdId, int userId, List<HoldAttendeeDto> attendees);
}