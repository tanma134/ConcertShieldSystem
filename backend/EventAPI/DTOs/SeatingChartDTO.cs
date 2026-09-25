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
        [MaxLength(20)]
        public string ZoneType { get; set; } = "Seated";

        // ---- Seated zones only ----
        public int Rows { get; set; } = 1;
        public int SeatsPerRow { get; set; } = 10;
        public string RowLabelPrefix { get; set; } = "A";

        // ---- Standing zones only ----
        public int? Capacity { get; set; }
    }

    public class UpdateSeatZoneDTO
    {
        [MaxLength(100)]
        public string? ZoneName { get; set; }

        public int? TicketTypeId { get; set; }
        public string? ShapeJson { get; set; }
        public int? Capacity { get; set; }
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
        public string ZoneType { get; set; } = "Seated";
        public bool IsSeated => ZoneType == "Seated";
        public int Capacity { get; set; }
        public int TotalSeats { get; set; }

        public List<SeatResponseDTO> Seats { get; set; } = new();
    }

    public class SeatResponseDTO
    {
        public int SeatId { get; set; }
        public string RowLabel { get; set; } = null!;
        public string SeatNumber { get; set; } = null!;
        public int? XCoordinate { get; set; }
        public int? YCoordinate { get; set; }
    }

    public class SeatingChartPreviewDTO
    {
        public int SeatMapId { get; set; }
        public string Name { get; set; } = null!;
        public int TotalZones { get; set; }
        public int SeatedZones { get; set; }
        public int StandingZones { get; set; }
        public int TotalCapacity { get; set; }
        public int TotalSeats { get; set; }

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
    }
}
