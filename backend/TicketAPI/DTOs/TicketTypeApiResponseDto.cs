namespace BookingAPI.DTOS
{
    public class TicketTypeApiResponseDto
    {
        public int TicketTypeId { get; set; }
        public string TypeName { get; set; } = null!;
        public long Price { get; set; }
        public int Quantity { get; set; }
        public int SoldQuantity { get; set; }
        public string Status { get; set; } = null!;
        public int MinPerOrder { get; set; }
        public int MaxPerOrder { get; set; }
    }
}
