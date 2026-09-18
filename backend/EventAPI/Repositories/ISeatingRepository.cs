using EventAPI.Models;

namespace EventAPI.Repositories
{
    public interface ISeatingRepository
    {
        /// <summary>The event's seat map with zones and seats eagerly loaded.</summary>
        Task<SeatMap?> GetByEventIdAsync(int eventId, bool includeSeats = true);

        Task<SeatMap?> GetSeatMapByIdAsync(int seatMapId, bool includeSeats = true);

        Task<SeatMap> CreateSeatMapAsync(SeatMap seatMap);
        Task UpdateSeatMapAsync(SeatMap seatMap);

        /// <summary>Hard-deletes the map, its zones and all their seats (cascade).</summary>
        Task DeleteSeatMapAsync(int seatMapId, int deletedBy);

        Task<SeatZone?> GetZoneByIdAsync(int seatZoneId, bool includeSeats = true);
        Task<SeatZone> CreateZoneAsync(SeatZone zone);
        Task UpdateZoneAsync(SeatZone zone);
        Task DeleteZoneAsync(int seatZoneId);

        Task AddSeatsAsync(List<Seat> seats);
        Task DeleteSeatsByZoneAsync(int seatZoneId);

        /// <summary>Counts seats in a zone that are not Available (held/reserved/sold).</summary>
        Task<int> CountOccupiedSeatsAsync(int seatZoneId);

        /// <summary>Total seats mapped for an event across every zone.</summary>
        Task<int> CountSeatsForEventAsync(int eventId);
    }
}
