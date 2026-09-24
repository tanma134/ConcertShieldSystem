using System.ComponentModel.DataAnnotations;

namespace EventAPI.DTOs
{
    /// <summary>
    /// One zone inside a template — the same shape/rows/capacity fields as
    /// CreateSeatZoneDTO, minus TicketTypeId (a template is event-agnostic; the
    /// ticket type is chosen again each time the template is applied).
    /// </summary>
    public class SeatingTemplateZoneDTO
    {
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

    /// <summary>UC_26.3 — Save Seating Chart as Template: snapshot an already-built concert layout.</summary>
    public class SaveSeatingTemplateDTO
    {
        [Required]
        public int EventId { get; set; }

        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = null!;

        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>Only honored when the caller is an Admin.</summary>
        public bool IsPublic { get; set; } = false;
    }

    /// <summary>Draw-from-scratch template, built directly in the editor without a source concert.</summary>
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

        /// <summary>Only honored when the caller is an Admin.</summary>
        public bool IsPublic { get; set; } = false;
    }

    public class UpdateSeatingTemplateDTO
    {
        [MaxLength(150)]
        public string? Name { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>Only honored when the caller is an Admin.</summary>
        public bool? IsPublic { get; set; }
    }

    /// <summary>One entry in an organizer's zone-to-ticket-type mapping for UC_26.2 — Apply Seating Template.</summary>
    public class TemplateZoneMappingDTO
    {
        /// <summary>Index into the template's Zones list.</summary>
        [Required]
        public int ZoneIndex { get; set; }

        [Required]
        public int TicketTypeId { get; set; }

        /// <summary>Editable copy values. Template defaults are used when omitted.</summary>
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

    /// <summary>Lightweight row for the template picker (UC_26.2's "Seating Template List" screen).</summary>
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
