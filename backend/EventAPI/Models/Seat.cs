namespace EventAPI.Models
{
    // Physical seat definition owned by EventAPI. Runtime booking state belongs to Booking/Queue.
    public class Seat
    {
        public int SeatId { get; set; }
        public int SeatZoneId { get; set; }
        public string RowLabel { get; set; } = null!;
        public string SeatNumber { get; set; } = null!;

        // Grid coordinates are layout coordinates, not venue latitude/longitude.
        // They let every consumer render the same deterministic seat map.
        public int? XCoordinate { get; set; }
        public int? YCoordinate { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public virtual SeatZone SeatZone { get; set; } = null!;
    }
}
