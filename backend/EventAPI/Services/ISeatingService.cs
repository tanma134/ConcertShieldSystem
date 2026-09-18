using EventAPI.DTOs;

namespace EventAPI.Services
{
    public interface ISeatingService
    {
        /// <summary>Full chart with every zone and seat. Null when the concert is general admission.</summary>
        Task<SeatingChartResponseDTO?> GetByEventIdAsync(int eventId);

        /// <summary>Lightweight summary (counts per zone, no seat list) for cards and previews.</summary>
        Task<SeatingChartPreviewDTO?> GetPreviewAsync(int eventId);

        /// <summary>
        /// The seats of one Seated zone, for the buyer's seat-picker. Throws for a
        /// Standing zone, which has no individual seats to choose.
        /// </summary>
        Task<SeatZoneResponseDTO> GetZoneSeatsAsync(int seatZoneId, bool availableOnly = false);

        /// <summary>
        /// Creates or replaces the concert's seating chart and generates every seat
        /// from the supplied zone definitions. Sets Event.HasSeatingChart = true.
        /// </summary>
        Task<SeatingChartResponseDTO> BuildAsync(int eventId, BuildSeatingChartDTO dto, int callerId, bool isAdmin);

        /// <summary>Adds one more zone (and its generated seats) to an existing chart.</summary>
        Task<SeatZoneResponseDTO> AddZoneAsync(int eventId, CreateSeatZoneDTO dto, int callerId, bool isAdmin);

        Task<SeatZoneResponseDTO> UpdateZoneAsync(int seatZoneId, UpdateSeatZoneDTO dto, int callerId, bool isAdmin);

        Task DeleteZoneAsync(int seatZoneId, int callerId, bool isAdmin);

        /// <summary>
        /// Removes the whole chart and flips the concert back to general admission
        /// (HasSeatingChart = false).
        /// </summary>
        Task DeleteAsync(int eventId, int callerId, bool isAdmin);
    }
}
