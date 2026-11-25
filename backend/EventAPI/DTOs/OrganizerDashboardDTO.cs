using System;
using System.Collections.Generic;

namespace EventAPI.DTOs
{
// Du lieu thong ke concert va doanh thu ve ban tren dashboard cua Organizer
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

    // Tong hop cac chi so concert, tong so ve ban va tong doanh thu cua Organizer
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

        // Danh sach concert sap xep theo moi nhat truoc tien
        public List<OrganizerDashboardEventDTO> Events { get; set; } = new();
    }
}
