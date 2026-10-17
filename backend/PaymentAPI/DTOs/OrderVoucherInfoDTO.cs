namespace PaymentAPI.DTOs
{
    public class OrderVoucherInfoDTO
    {
        public int? VoucherId { get; set; }
        public int UserId { get; set; }
        public long DiscountAmount { get; set; }
    }
}
