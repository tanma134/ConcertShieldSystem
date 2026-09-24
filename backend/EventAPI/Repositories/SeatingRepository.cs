using EventAPI.Data;
using EventAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace EventAPI.Repositories
{
    public class SeatingRepository : ISeatingRepository
    {
        private readonly EventDbContext _context;

        public SeatingRepository(EventDbContext context)
        {
            _context = context;
        }

        public async Task<SeatMap?> GetByEventIdAsync(int eventId, bool includeSeats = true)
        {
            var query = _context.SeatMaps.Where(m => m.EventId == eventId && !m.IsDeleted);

            query = includeSeats
                ? query.Include(m => m.SeatZones).ThenInclude(z => z.Seats)
                : query.Include(m => m.SeatZones);

            return await query.FirstOrDefaultAsync();
        }

        public async Task<SeatMap?> GetSeatMapByIdAsync(int seatMapId, bool includeSeats = true)
        {
            var query = _context.SeatMaps.Where(m => m.SeatMapId == seatMapId && !m.IsDeleted);

            query = includeSeats
                ? query.Include(m => m.SeatZones).ThenInclude(z => z.Seats)
                : query.Include(m => m.SeatZones);

            return await query.FirstOrDefaultAsync();
        }

        public async Task<SeatMap> CreateSeatMapAsync(SeatMap seatMap)
        {
            _context.SeatMaps.Add(seatMap);
            await _context.SaveChangesAsync();
            return seatMap;
        }

        public async Task UpdateSeatMapAsync(SeatMap seatMap)
        {
            seatMap.UpdatedAt = DateTime.UtcNow;
            _context.SeatMaps.Update(seatMap);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteSeatMapAsync(int seatMapId, int deletedBy)
        {
            var map = await _context.SeatMaps
                .Include(m => m.SeatZones).ThenInclude(z => z.Seats)
                .FirstOrDefaultAsync(m => m.SeatMapId == seatMapId);

            if (map == null) return;

            // Seats cascade from zones, but remove explicitly so the change tracker
            // doesn't leave orphans if the cascade rule ever changes.
            foreach (var zone in map.SeatZones)
                _context.Seats.RemoveRange(zone.Seats);

            _context.SeatZones.RemoveRange(map.SeatZones);
            _context.SeatMaps.Remove(map);

            await _context.SaveChangesAsync();
        }

        public async Task<SeatZone?> GetZoneByIdAsync(int seatZoneId, bool includeSeats = true)
        {
            var query = _context.SeatZones.Where(z => z.SeatZoneId == seatZoneId);

            if (includeSeats)
                query = query.Include(z => z.Seats);

            return await query.Include(z => z.SeatMap).FirstOrDefaultAsync();
        }

        public async Task<SeatZone> CreateZoneAsync(SeatZone zone)
        {
            _context.SeatZones.Add(zone);
            await _context.SaveChangesAsync();
            return zone;
        }

        public async Task UpdateZoneAsync(SeatZone zone)
        {
            _context.SeatZones.Update(zone);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteZoneAsync(int seatZoneId)
        {
            var zone = await _context.SeatZones
                .Include(z => z.Seats)
                .FirstOrDefaultAsync(z => z.SeatZoneId == seatZoneId);

            if (zone == null) return;

            _context.Seats.RemoveRange(zone.Seats);
            _context.SeatZones.Remove(zone);
            await _context.SaveChangesAsync();
        }

        public async Task AddSeatsAsync(List<Seat> seats)
        {
            if (seats.Count == 0) return;
            _context.Seats.AddRange(seats);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteSeatsByZoneAsync(int seatZoneId)
        {
            await _context.Seats
                .Where(s => s.SeatZoneId == seatZoneId)
                .ExecuteDeleteAsync();
        }

        public async Task ReplaceSeatsAsync(int seatZoneId, List<Seat> seats, int capacity)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var zone = await _context.SeatZones.Include(z => z.Seats)
                .FirstOrDefaultAsync(z => z.SeatZoneId == seatZoneId)
                ?? throw new KeyNotFoundException($"Seat zone {seatZoneId} not found.");
            _context.Seats.RemoveRange(zone.Seats);
            zone.Seats.Clear();
            foreach (var seat in seats) zone.Seats.Add(seat);
            zone.Capacity = capacity;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        public async Task<int> CountOccupiedSeatsAsync(int seatZoneId)
        {
            return await _context.Seats
                .CountAsync(s => s.SeatZoneId == seatZoneId && s.Status != "Available");
        }

        public async Task<int> CountSeatsForEventAsync(int eventId)
        {
            return await _context.Seats
                .CountAsync(s => s.SeatZone.SeatMap.EventId == eventId && !s.SeatZone.SeatMap.IsDeleted);
        }
    }
}
