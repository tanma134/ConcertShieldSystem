namespace TicketAPI.DTOs
{
    public class MyTicketResponseDto
    {
        public int TicketId { get; set; }
        public Guid TicketCode { get; set; }
        public int OrderId { get; set; }
        public int EventId { get; set; }
        public int TicketTypeId { get; set; }
        public string? TicketTypeName { get; set; }
        public int? SeatId { get; set; }
        public string? OwnerName { get; set; }
        public string? Status { get; set; }
        public DateTime? CheckedInAt { get; set; }

        public string? EventName { get; set; }
        public string? PosterUrl { get; set; }
        public DateTime? StartsAt { get; set; }
        public DateTime? OrderDate { get; set; }
        public string? OrderStatus { get; set; }
        public long? UnitPrice { get; set; }
    }
}
