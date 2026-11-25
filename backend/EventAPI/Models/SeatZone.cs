using System.Collections.Generic;

namespace EventAPI.Models
{
    // Zone inside a concert layout; Seated and Standing areas can coexist in the same event.
    public class SeatZone
    {
        public int SeatZoneId { get; set; }
        public int SeatMapId { get; set; }
        public int TicketTypeId { get; set; }
        public string ZoneName { get; set; } = null!;
        public string? ShapeJson { get; set; }

        public string ZoneType { get; set; } = "Seated";

        // For Standing zones, set by the organizer; for Seated zones, maintained as the Seat row count.
        public int Capacity { get; set; } = 0;

        public virtual SeatMap SeatMap { get; set; } = null!;
        public virtual TicketType TicketType { get; set; } = null!;
        public virtual ICollection<Seat> Seats { get; set; } = new List<Seat>();
    }
}
