namespace EventAPI.DTOs
{
    public class EventResponseDTO
    {
        public int EventId { get; set; }
        public int OrganizerId { get; set; }
        public int CategoryId { get; set; }
        public string Title { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public string? ShortDescription { get; set; }
        public string? Description { get; set; }
        public string? PosterUrl { get; set; }
        public string? BannerUrl { get; set; }
        public string? LocationName { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public decimal? Longitude { get; set; }
        public decimal? Latitude { get; set; }
        public DateTime StartsAt { get; set; }
        public DateTime EndsAt { get; set; }
        public string Timezone { get; set; } = null!;
        public bool HasSeatingChart { get; set; }
        public string SeatingMode { get; set; } = Common.SeatingMode.ReservedSeating;
        public bool RequiresVirtualQueue { get; set; }
        public string Status { get; set; } = null!;
        public string? RejectedReason { get; set; }
        public bool IsFeatured { get; set; }
        public int ViewCount { get; set; }
        public int TotalTickets { get; set; }
        public int SoldTickets { get; set; }
        public int AvailableTickets => TotalTickets - SoldTickets;
        public int? MinTicketsPerAccount { get; set; }
        public int? MaxTicketsPerAccount { get; set; }
        public string? MetaTitle { get; set; }
        public string? MetaDescription { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? PublishedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? RejectedAt { get; set; }
        public int? ReviewedBy { get; set; }

        // Always "Music" in the current scope (CategoryId = 1).
        public string Category { get; set; } = "Music";

        // True when the concert is publicly visible and bookable.
        public bool IsPublic { get; set; }

        public SeatingChartResponseDTO? SeatingChart { get; set; }

        public List<TicketTypeResponseDTO> TicketTypes { get; set; } = new();
        public List<EventImageResponseDTO> Images { get; set; } = new();
        public List<RefundPolicyResponseDTO> RefundPolicies { get; set; } = new();
    }

    public class EventListDTO
    {
        public int EventId { get; set; }
        public int OrganizerId { get; set; }
        public string Title { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public string? ShortDescription { get; set; }
        public string? PosterUrl { get; set; }
        public string? LocationName { get; set; }
        public string? City { get; set; }
        public DateTime StartsAt { get; set; }
        public DateTime EndsAt { get; set; }
        public string Status { get; set; } = null!;
        public bool HasSeatingChart { get; set; }
        public string SeatingMode { get; set; } = Common.SeatingMode.ReservedSeating;
        public bool IsFeatured { get; set; }
        public int ViewCount { get; set; }
        public int TotalTickets { get; set; }
        public int SoldTickets { get; set; }
        public int AvailableTickets => TotalTickets - SoldTickets;
        public long? MinPrice { get; set; }
        public string Category { get; set; } = "Music";
        public DateTime CreatedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? RejectedAt { get; set; }
        public int? ReviewedBy { get; set; }
        public string? RejectedReason { get; set; }
    }

    public class EventFilterDTO
    {
        public string? Search { get; set; }
        public string? City { get; set; }
        public string? Status { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public long? MinPrice { get; set; }
        public long? MaxPrice { get; set; }
        public int? OrganizerId { get; set; }
        public bool? IsFeatured { get; set; }
        public bool? HasSeatingChart { get; set; }
        public string? SeatingMode { get; set; }

        // Set by the controller, never bound from the query string: when true the
        // repository hard-filters to Published only, so public endpoints can never
        // leak Draft/Pending/Rejected concerts regardless of the Status parameter.

        [System.Text.Json.Serialization.JsonIgnore]
        [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
        public bool PublishedOnly { get; set; } = false;

        public string SortBy { get; set; } = "CreatedAt";
        public string SortOrder { get; set; } = "desc";
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 12;
    }
}
