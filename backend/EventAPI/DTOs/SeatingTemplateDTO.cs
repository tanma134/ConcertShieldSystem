using System.ComponentModel.DataAnnotations;

namespace EventAPI.DTOs
{
    public class SeatingTemplateZoneDTO
    {
        [Required]
        [MaxLength(100)]
        public string ZoneName { get; set; } = null!;

        public string? ShapeJson { get; set; }

        [MaxLength(20)]
        public string ZoneType { get; set; } = "Seated";

        public int Rows { get; set; } = 1;
        public int SeatsPerRow { get; set; } = 10;
        public string RowLabelPrefix { get; set; } = "A";

        public int? Capacity { get; set; }
    }

    public class SaveSeatingTemplateDTO
    {
        [Required]
        public int EventId { get; set; }

        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = null!;

        [MaxLength(500)]
        public string? Description { get; set; }

        public bool IsPublic { get; set; } = false;
    }

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

        public bool? IsPublic { get; set; }
    }

    public class TemplateZoneMappingDTO
    {
        [Required]
        public int ZoneIndex { get; set; }

        [Required]
        public int TicketTypeId { get; set; }

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
