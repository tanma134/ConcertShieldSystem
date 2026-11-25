using System.ComponentModel.DataAnnotations;

namespace EventAPI.DTOs
{
// Thong tin mot khu vuc trong mau so do ghe tai su dung
    public class SeatingTemplateZoneDTO
    {
        [Required]
        [MaxLength(100)]
        public string ZoneName { get; set; } = null!;

        public string? ShapeJson { get; set; }

        [MaxLength(20)]
        public string ZoneType { get; set; } = "Seated";

        // --- Seated zones only ----
        public int Rows { get; set; } = 1;
        public int SeatsPerRow { get; set; } = 10;
        public string RowLabelPrefix { get; set; } = "A";

        // --- Standing zones only ----
        public int? Capacity { get; set; }
    }

    // Yeu cau luu layout hien tai cua concert thanh mau mau de tai su dung
    public class SaveSeatingTemplateDTO
    {
        [Required]
        public int EventId { get; set; }

        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = null!;

        [MaxLength(500)]
        public string? Description { get; set; }

        // Quyen cong khai mau so do ghe (chi danh cho Admin)
        public bool IsPublic { get; set; } = false;
    }

    // Yeu cau tao mau so do ghe moi truc tiep tu canvas khong gan voi concert cu the
    public class CreateSeatingTemplateDTO
    {
        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = null!;

        [MaxLength(500)]
        public string? Description { get; set; }

        public string? LayoutJson { get; set; }

        [Required]
        public List<SeatingTemplateZoneDTO> Zones { get; set; } = new();

        // Quyen cong khai mau so do ghe (chi danh cho Admin)
        public bool IsPublic { get; set; } = false;
    }

    public class UpdateSeatingTemplateDTO
    {
        [MaxLength(150)]
        public string? Name { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public List<SeatingTemplateZoneDTO>? Zones { get; set; }

        public string? LayoutJson { get; set; }

        // Quyen cong khai mau so do ghe (chi danh cho Admin)
        public bool? IsPublic { get; set; }
    }

    // Anh xa khu vuc trong mau voi hang ve cua concert khi ap dung template
    public class TemplateZoneMappingDTO
    {
        // Chi so index cua khu vuc trong template
        [Required]
        public int ZoneIndex { get; set; }

        [Required]
        public int TicketTypeId { get; set; }

        // Cac gia tri tuy bien ghi de neu co
        [MaxLength(100)] public string? ZoneName { get; set; }
        [MaxLength(20)] public string? ZoneType { get; set; }
        public int? Rows { get; set; }
        public int? SeatsPerRow { get; set; }
        [MaxLength(3)] public string? RowLabelPrefix { get; set; }
        public int? Capacity { get; set; }
        public string? ShapeJson { get; set; }
    }

    public class ApplySeatingTemplateDTO
    {
        [Required]
        public List<TemplateZoneMappingDTO> ZoneMappings { get; set; } = new();
    }

    public class SeatingTemplateResponseDTO
    {
        public int SeatingTemplateId { get; set; }
        public int OrganizerId { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public bool IsPublic { get; set; }
        public string? LayoutJson { get; set; }
        public List<SeatingTemplateZoneDTO> Zones { get; set; } = new();
        public int TotalZones => Zones.Count;
        public int EstimatedCapacity { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    // Danh sach tom tat mau so do ghe de hien thi trong bo chon template
    public class SeatingTemplateListDTO
    {
        public int SeatingTemplateId { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public bool IsPublic { get; set; }
        public bool IsMine { get; set; }
        public int TotalZones { get; set; }
        public int EstimatedCapacity { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
