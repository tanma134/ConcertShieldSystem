namespace EventAPI.Models
{
    // A zone groups one ticket class and one physical area of the venue.
    // Seated zones own Seat rows; Standing zones own only Capacity.
    public class SeatZone
    {
        public int SeatZoneId { get; set; }
        public int SeatMapId { get; set; }
        public int TicketTypeId { get; set; }
        public string ZoneName { get; set; } = null!;
        public string? ShapeJson { get; set; }
        public string ZoneType { get; set; } = "Seated";

        // For Seated this equals the number of generated Seat rows.
        // For Standing it is the organizer-entered headcount.
        public int Capacity { get; set; }

        public virtual SeatMap SeatMap { get; set; } = null!;
        public virtual TicketType TicketType { get; set; } = null!;
        public virtual ICollection<Seat> Seats { get; set; } = new List<Seat>();
    }
}
