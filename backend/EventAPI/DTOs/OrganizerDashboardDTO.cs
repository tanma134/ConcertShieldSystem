using System;
using System.Collections.Generic;

namespace EventAPI.DTOs
{

    // One row in the organizer's dashboard event table. Revenue is a simple
    // sum of price * soldQuantity across the concert's own ticket types - it
    // does not account for refunds, which live in RefundAPI, so it is best
    // read as "gross ticket sales", not net payout.

    public class OrganizerDashboardEventDTO
    {
        public int EventId { get; set; }
        public string Title { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public string? PosterUrl { get; set; }
        public string Status { get; set; } = null!;
        public DateTime StartsAt { get; set; }
        public int TotalTickets { get; set; }
        public int SoldTickets { get; set; }
        public long Revenue { get; set; }
    }

    public class OrganizerDashboardSummaryDTO
    {
        public int TotalEvents { get; set; }
        public int DraftEvents { get; set; }
        public int PendingEvents { get; set; }
        public int PublishedEvents { get; set; }
        public int RejectedEvents { get; set; }
        public int CancelledEvents { get; set; }

        public int TotalTicketsSold { get; set; }
        public long TotalRevenue { get; set; }

        // Newest first - same ordering as GET /events/mine.
        public List<OrganizerDashboardEventDTO> Events { get; set; } = new();
    }
}
