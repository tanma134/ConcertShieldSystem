namespace TicketAPI.DTOs
{
    public class OrderVoucherInfoDto
    {
        public int? VoucherId { get; set; }
        public int UserId { get; set; }
        public long DiscountAmount { get; set; }
    }
}
