using System;

namespace EventAPI.Models
{
    // Reusable venue layout; deliberately not linked to TicketType so geometry can be reused across concerts.
    public class SeatingTemplate
    {
        public int SeatingTemplateId { get; set; }

        // Soft reference to identity_db.users; only the owner or an Admin may edit or delete.
        public int OrganizerId { get; set; }

        public string Name { get; set; } = null!;
        public string? Description { get; set; }

        public string SeatingMode { get; set; } = Common.SeatingMode.ReservedSeating;

        // Admin-provided starter template visible to all organizers; only Admins may set this.
        public bool IsPublic { get; set; } = false;

        public string? LayoutJson { get; set; }

        public string ZonesJson { get; set; } = null!;

        public int CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
    }
}
