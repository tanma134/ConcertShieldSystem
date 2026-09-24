using System;

namespace EventAPI.Models
{
    /// <summary>
    /// A reusable venue layout an organizer can apply to a new concert instead of
    /// drawing zones from scratch (UC_26.2 Apply Seating Template / UC_26.3 Save
    /// Seating Chart as Template).
    ///
    /// Deliberately NOT linked to any TicketType — a template is reused across
    /// concerts whose ticket types differ each time, so only the geometry (zone
    /// shapes, row/seat counts, standing capacity) survives. The ticket type for
    /// each zone is chosen again at apply-time.
    /// </summary>
    public class SeatingTemplate
    {
        public int SeatingTemplateId { get; set; }

        /// <summary>Owner — soft ref -> identity_db.users. Only the owner (or an Admin) may edit/delete it.</summary>
        public int OrganizerId { get; set; }

        public string Name { get; set; } = null!;
        public string? Description { get; set; }

        /// <summary>StandingZones | ReservedSeating (see SeatingMode).</summary>
        public string SeatingMode { get; set; } = Common.SeatingMode.ReservedSeating;

        /// <summary>
        /// True for admin-provided starter templates visible to every organizer
        /// (e.g. "Standard theater", "Round arena"). Only an Admin caller may set this.
        /// </summary>
        public bool IsPublic { get; set; } = false;

        /// <summary>Free-form canvas/stage metadata (mirrors SeatMap.LayoutJson).</summary>
        public string? LayoutJson { get; set; }

        /// <summary>Serialized List&lt;SeatingTemplateZoneDTO&gt; — the zone shapes/rows/capacity.</summary>
        public string ZonesJson { get; set; } = null!;

        public int CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
    }
}
