using System;

namespace EventAPI.Models
{
    public class Seat
    {
        public int SeatId { get; set; }
        public int SeatZoneId { get; set; }
        public string RowLabel { get; set; } = null!;
        public string SeatNumber { get; set; } = null!;
        public int? XCoordinate { get; set; }
        public int? YCoordinate { get; set; }
        public string Status { get; set; } = "Available"; // Available, Held, Reserved, Sold, Blocked
        public int? HeldByUserId { get; set; }
        public DateTime? HoldExpiresAt { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public virtual SeatZone SeatZone { get; set; } = null!;
    }
}
