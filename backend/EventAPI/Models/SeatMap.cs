using System;
using System.Collections.Generic;

namespace EventAPI.Models
{
    public class SeatMap
    {
        public int SeatMapId { get; set; }
        public int EventId { get; set; }
        public string Name { get; set; } = null!;
        public string? LayoutJson { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public int? DeletedBy { get; set; }

        public virtual Event Event { get; set; } = null!;
        public virtual ICollection<SeatZone> SeatZones { get; set; } = new List<SeatZone>();
    }
}
