namespace TicketAPI.DTOs
{
    public class CreateOrderDTO
    {
        public int EventId { get; set; }
        public bool IsReservedSeating { get; set; }

        public List<CreateOrderDetailsDTO> OrderDetails { get; set; } = new();

        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }

        public string? VoucherCode { get; set; }

        public string HoldId { get; set; } = "";
        public List<HoldAttendeeDto> Attendees { get; set; } = new();
    }
}
