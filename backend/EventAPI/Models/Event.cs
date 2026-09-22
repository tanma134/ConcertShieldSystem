using System;
using System.Collections.Generic;

namespace EventAPI.Models
{
    public class Event
    {
        public int EventId { get; set; }
        public int OrganizerId { get; set; }
        public int CategoryId { get; set; } = 1; // Always 1 = Music
        public string Title { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public string? ShortDescription { get; set; }
        public string? Description { get; set; }
        public string? PosterUrl { get; set; }
        public string? PosterPublicId { get; set; }
        public string? BannerUrl { get; set; }
        public string? BannerPublicId { get; set; }
        public string? LocationName { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public decimal? Longitude { get; set; }
        public decimal? Latitude { get; set; }
        public DateTime StartsAt { get; set; }
        public DateTime EndsAt { get; set; }
        public string Timezone { get; set; } = "SE Asia Standard Time";
        public bool HasSeatingChart { get; set; } = false;
        /// <summary>GeneralAdmission | StandingZones | ReservedSeating (see SeatingMode).</summary>
        public string SeatingMode { get; set; } = Common.SeatingMode.ReservedSeating;
        public bool RequiresVirtualQueue { get; set; } = false;
        /// <summary>Draft | Pending | Published | Rejected | Cancelled (see EventStatus).</summary>
        public string Status { get; set; } = "Draft";

        /// <summary>Reason supplied by the Admin when rejecting. Cleared on resubmit.</summary>
        public string? RejectedReason { get; set; }

        // ---- Lifecycle audit timestamps ----
        /// <summary>Set when the owner submits Draft/Rejected -> Pending.</summary>
        public DateTime? SubmittedAt { get; set; }

        /// <summary>Set when an Admin approves Pending -> Published.</summary>
        public DateTime? ApprovedAt { get; set; }

        /// <summary>Set when an Admin rejects Pending -> Rejected.</summary>
        public DateTime? RejectedAt { get; set; }

        /// <summary>Admin user id that approved/rejected this concert.</summary>
        public int? ReviewedBy { get; set; }
        public bool IsFeatured { get; set; } = false;
        public int ViewCount { get; set; } = 0;
        public int TotalTickets { get; set; } = 0;
        public int SoldTickets { get; set; } = 0;
        public int? MinTicketsPerAccount { get; set; }
        public int? MaxTicketsPerAccount { get; set; }
        public string? MetaTitle { get; set; }
        public string? MetaDescription { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PublishedAt { get; set; }
        public int? CreatedBy { get; set; }
        public int? UpdatedBy { get; set; }
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public int? DeletedBy { get; set; }

        public virtual ICollection<EventImage> EventImages { get; set; } = new List<EventImage>();
        public virtual ICollection<TicketType> TicketTypes { get; set; } = new List<TicketType>();
        public virtual ICollection<RefundPolicy> RefundPolicies { get; set; } = new List<RefundPolicy>();
        public virtual ICollection<SeatMap> SeatMaps { get; set; } = new List<SeatMap>();
        public virtual ICollection<Wishlist> Wishlists { get; set; } = new List<Wishlist>();
        public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
    }
}
