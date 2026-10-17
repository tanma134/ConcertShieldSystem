namespace TicketAPI.DTOs
{
    public class OrganizerOrderQueryDto
    {
        public string? Search { get; set; }
        public string? Status { get; set; }
        public DateOnly? FromDate { get; set; }
        public DateOnly? ToDate { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class OrganizerOrderItemDto
    {
        public int OrderId { get; set; }
        public int EventId { get; set; }
        public string? EventName { get; set; }

        public int CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerEmail { get; set; }

        public DateTime? OrderDate { get; set; }
        public int TicketCount { get; set; }
        public long TotalAmount { get; set; }
        public long DiscountAmount { get; set; }
        public long FinalAmount { get; set; }
        public string? PaymentMethod { get; set; }
        public string? Status { get; set; }
    }

    public class OrganizerOrderPageDto
    {
        public int EventId { get; set; }
        public string EventName { get; set; } = string.Empty;
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public List<OrganizerOrderItemDto> Items { get; set; } = new();
    }
}
