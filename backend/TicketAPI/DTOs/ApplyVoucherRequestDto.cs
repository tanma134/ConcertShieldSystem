using System.Text.Json.Serialization;

namespace TicketAPI.DTOs
{
    public class ApplyVoucherRequestDto
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("event_id")]
        public int EventId { get; set; }

        [JsonPropertyName("order_amount")]
        public long OrderAmount { get; set; }
    }
}
