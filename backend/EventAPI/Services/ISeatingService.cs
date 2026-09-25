using EventAPI.DTOs;

namespace EventAPI.Services
{
    public interface ISeatingService
    {
        Task<SeatingChartResponseDTO?> GetByEventIdAsync(int eventId, int? callerId, bool isAdmin);
        Task<SeatingChartPreviewDTO?> GetPreviewAsync(int eventId, int? callerId, bool isAdmin);
        Task<SeatZoneResponseDTO> GetZoneSeatsAsync(int seatZoneId, int? callerId, bool isAdmin);
        Task<SeatingChartResponseDTO> BuildAsync(int eventId, BuildSeatingChartDTO dto, int callerId, bool isAdmin);
        Task<SeatZoneResponseDTO> AddZoneAsync(int eventId, CreateSeatZoneDTO dto, int callerId, bool isAdmin);

        Task<SeatZoneResponseDTO> UpdateZoneAsync(int seatZoneId, UpdateSeatZoneDTO dto, int callerId, bool isAdmin);

        Task DeleteZoneAsync(int seatZoneId, int callerId, bool isAdmin);
        Task DeleteAsync(int eventId, int callerId, bool isAdmin);
    }
}
