using System.Collections.Generic;

namespace EventAPI.Models
{
    /// <summary>
    /// A zone inside a concert's layout. Zones are independently typed, so one
    /// concert can mix assigned seating and standing areas — e.g. numbered seats on
    /// the balcony plus a standing pit in front of the stage.
    ///
    ///   Seated   -> has one Seat row per physical seat; buyers pick their own seat.
    ///   Standing -> no Seat rows at all; only a headcount (<see cref="Capacity"/>).
    /// </summary>
    public class SeatZone
    {
        public int SeatZoneId { get; set; }
        public int SeatMapId { get; set; }
        public int TicketTypeId { get; set; }
        public string ZoneName { get; set; } = null!;
        public string? ShapeJson { get; set; }

        /// <summary>"Seated" or "Standing" — see <see cref="Common.SeatZoneType"/>.</summary>
        public string ZoneType { get; set; } = "Seated";

        /// <summary>
        /// How many people this zone holds.
        /// Standing zones: set directly by the organizer.
        /// Seated zones: maintained by the system as the number of Seat rows.
        /// Either way this is the capacity that feeds TicketType.Quantity.
        /// </summary>
        public int Capacity { get; set; } = 0;

        public virtual SeatMap SeatMap { get; set; } = null!;
        public virtual TicketType TicketType { get; set; } = null!;
        public virtual ICollection<Seat> Seats { get; set; } = new List<Seat>();
    }
}
