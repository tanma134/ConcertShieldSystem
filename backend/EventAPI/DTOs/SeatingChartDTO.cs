using System.ComponentModel.DataAnnotations;

namespace EventAPI.DTOs
{
    public class UpdateLayoutDTO
    {
        public string? LayoutJson { get; set; }
    }

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

        // Loai khu vuc: Seated (co so ghe cu the) hoac Standing (suc chua dung)
        [MaxLength(20)]
        public string ZoneType { get; set; } = "Seated";

        // --- Seated zones only ----
        // So luong hang ghe can tao tu dong
        public int Rows { get; set; } = 1;

        // So luong ghe tren moi hang
        public int SeatsPerRow { get; set; } = 10;

        // Ky hieu hang ghe bat dau (vi du: A, B, C...)
        public string RowLabelPrefix { get; set; } = "A";

        // --- Standing zones only ----
        // Suc chua toi da cua khu vuc dung (Standing)
        public int? Capacity { get; set; }
    }

    public class UpdateSeatZoneDTO
    {
        [MaxLength(100)]
        public string? ZoneName { get; set; }

        public int? TicketTypeId { get; set; }
        public string? ShapeJson { get; set; }

        // Suc chua khu vuc dung (khong duoc nho hon so ve da ban)
        public int? Capacity { get; set; }

        // So luong hang ghe va so ghe tren hang de ve lai luoi toa do
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

        // Loai khu vuc Seated hoac Standing
        public string ZoneType { get; set; } = "Seated";

        // Co phai la khu vuc co danh so ghe hay khong
        public bool IsSeated => ZoneType == "Seated";

        // Tong suc chua cua khu vuc
        public int Capacity { get; set; }

        // Tong so ghe danh so trong khu vuc Seated
        public int TotalSeats { get; set; }

        public int AvailableSeats { get; set; }

        // Danh sach chi tiet cac ghe trong khu vuc
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

        // Tong suc chua tat ca cac khu vuc
        public int TotalCapacity { get; set; }

        // Tong so ghe co danh so
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
