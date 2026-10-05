namespace TicketAPI.DTOs
{
    public class OrderHistoryItemDto
    {
        public int OrderId { get; set; }

        public int EventId { get; set; }
        public string? EventName { get; set; }
        public string? PosterUrl { get; set; }

        public DateTime? OrderDate { get; set; }
        public DateTime? StartsAt { get; set; }

        public long TotalAmount { get; set; }
        public long DiscountAmount { get; set; }
        public long FinalAmount { get; set; }

        public string? PaymentMethod { get; set; }
        public string? Status { get; set; }
        public DateTime? ExpiresAt { get; set; }

        public int TicketCount { get; set; }
    }
}
