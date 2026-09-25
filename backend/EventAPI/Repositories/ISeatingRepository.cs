using EventAPI.Models;

namespace EventAPI.Repositories
{
    public interface ISeatingRepository
    {
        Task<SeatMap?> GetByEventIdAsync(int eventId, bool includeSeats = true);

        Task<SeatMap?> GetSeatMapByIdAsync(int seatMapId, bool includeSeats = true);

        Task<SeatMap> CreateSeatMapAsync(SeatMap seatMap);
        Task UpdateSeatMapAsync(SeatMap seatMap);
        Task DeleteSeatMapAsync(int seatMapId, int deletedBy);

        Task<SeatZone?> GetZoneByIdAsync(int seatZoneId, bool includeSeats = true);
        Task<SeatZone> CreateZoneAsync(SeatZone zone);
        Task UpdateZoneAsync(SeatZone zone);
        Task DeleteZoneAsync(int seatZoneId);

        Task AddSeatsAsync(List<Seat> seats);
        Task DeleteSeatsByZoneAsync(int seatZoneId);
        Task ReplaceSeatsAsync(int seatZoneId, List<Seat> seats, int capacity);
        Task<int> CountSeatsForEventAsync(int eventId);
    }
}
