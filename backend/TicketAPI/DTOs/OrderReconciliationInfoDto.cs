namespace TicketAPI.DTOs
{
    public class OrderReconciliationInfoDto
    {
        public int OrderId { get; set; }
        public int EventId { get; set; }
        public string? Status { get; set; }
        public long FinalAmount { get; set; }
        public int ExpectedTicketCount { get; set; }
        public int IssuedTicketCount { get; set; }
        public int TicketsWithoutQrCount { get; set; }
    }
}
