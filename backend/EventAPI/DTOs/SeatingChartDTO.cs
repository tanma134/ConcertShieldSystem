using System.ComponentModel.DataAnnotations;

namespace EventAPI.DTOs
{
    public class BuildSeatingChartDTO
    {
        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = null!;

        public string? LayoutJson { get; set; }

        [MaxLength(30)]
        public string? SeatingMode { get; set; }

        public List<CreateSeatZoneDTO> Zones { get; set; } = new();
    }

    public class CreateSeatZoneDTO
    {
        [Required]
        public int TicketTypeId { get; set; }

        [Required]
        [MaxLength(100)]
        public string ZoneName { get; set; } = null!;

        public string? ShapeJson { get; set; }

        /// <summary>
        /// "Seated" (numbered seats, buyer picks one) or "Standing" (headcount only).
        /// A concert can contain both kinds of zone at the same time.
        /// </summary>
        [MaxLength(20)]
        public string ZoneType { get; set; } = "Seated";

        // ---- Seated zones only ----
        /// <summary>Number of rows to generate. Ignored for Standing zones.</summary>
        public int Rows { get; set; } = 1;

        /// <summary>Seats per row to generate. Ignored for Standing zones.</summary>
        public int SeatsPerRow { get; set; } = 10;

        /// <summary>First row label, e.g. "A" (then B, C... AA, AB). Ignored for Standing zones.</summary>
        public string RowLabelPrefix { get; set; } = "A";

        // ---- Standing zones only ----
        /// <summary>
        /// How many people the standing area holds. Required for Standing zones,
        /// ignored for Seated zones (their capacity is the generated seat count).
        /// </summary>
        public int? Capacity { get; set; }
    }

    public class UpdateSeatZoneDTO
    {
        [MaxLength(100)]
        public string? ZoneName { get; set; }

        public int? TicketTypeId { get; set; }
        public string? ShapeJson { get; set; }

        /// <summary>
        /// Standing zones only: change the headcount. Cannot be lowered below the
        /// number already sold. Ignored for Seated zones — resize those by deleting
        /// and re-adding the zone, so seat labels stay consistent.
        /// </summary>
        public int? Capacity { get; set; }

        /// <summary>Seated zones: regenerate the grid when no seat is held or sold.</summary>
        public int? Rows { get; set; }
        public int? SeatsPerRow { get; set; }

        [MaxLength(3)]
        public string? RowLabelPrefix { get; set; }
    }

    public class SeatingChartResponseDTO
    {
        public int SeatMapId { get; set; }
        public int EventId { get; set; }
        public string Name { get; set; } = null!;
        public string? LayoutJson { get; set; }
        public string SeatingMode { get; set; } = Common.SeatingMode.ReservedSeating;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<SeatZoneResponseDTO> Zones { get; set; } = new();
    }

    public class SeatZoneResponseDTO
    {
        public int SeatZoneId { get; set; }
        public int SeatMapId { get; set; }
        public int TicketTypeId { get; set; }
        public string ZoneName { get; set; } = null!;
        public string? ShapeJson { get; set; }

        /// <summary>"Seated" or "Standing".</summary>
        public string ZoneType { get; set; } = "Seated";

        /// <summary>True when buyers pick an individual seat in this zone.</summary>
        public bool IsSeated => ZoneType == "Seated";

        /// <summary>Total headcount: generated seats for Seated, the configured number for Standing.</summary>
        public int Capacity { get; set; }

        /// <summary>Seated zones: the seat count. Standing zones: 0 — use Capacity instead.</summary>
        public int TotalSeats { get; set; }

        public int AvailableSeats { get; set; }

        /// <summary>Empty for Standing zones, and for summary endpoints.</summary>
        public List<SeatResponseDTO> Seats { get; set; } = new();
    }

    public class SeatResponseDTO
    {
        public int SeatId { get; set; }
        public string RowLabel { get; set; } = null!;
        public string SeatNumber { get; set; } = null!;
        public int? XCoordinate { get; set; }
        public int? YCoordinate { get; set; }
        public string Status { get; set; } = null!;
    }

    public class SeatingChartPreviewDTO
    {
        public int SeatMapId { get; set; }
        public string Name { get; set; } = null!;
        public int TotalZones { get; set; }
        public int SeatedZones { get; set; }
        public int StandingZones { get; set; }

        /// <summary>Total headcount across every zone (seats + standing capacity).</summary>
        public int TotalCapacity { get; set; }

        /// <summary>Numbered seats only.</summary>
        public int TotalSeats { get; set; }

        public int AvailableSeats { get; set; }
        public List<ZonePreviewDTO> Zones { get; set; } = new();
    }

    public class ZonePreviewDTO
    {
        public int SeatZoneId { get; set; }
        public string ZoneName { get; set; } = null!;
        public string ZoneType { get; set; } = "Seated";
        public string? TicketTypeName { get; set; }
        public long? Price { get; set; }
        public int Capacity { get; set; }
        public int TotalSeats { get; set; }
        public int AvailableSeats { get; set; }
    }
}
