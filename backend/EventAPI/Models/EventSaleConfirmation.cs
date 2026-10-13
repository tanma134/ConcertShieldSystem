namespace EventAPI.Models
{
    public class EventSaleConfirmation
    {
        public int OrderId { get; set; }
        public int EventId { get; set; }
        public DateTime ConfirmedAtUtc { get; set; }
    }
}
