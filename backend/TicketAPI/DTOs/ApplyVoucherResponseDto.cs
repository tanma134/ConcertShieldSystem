using System.Text.Json.Serialization;

namespace TicketAPI.DTOs
{
    public class ApplyVoucherResponseDto
    {
        [JsonPropertyName("is_valid")]
        public bool IsValid { get; set; }

        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("discount_amount")]
        public long DiscountAmount { get; set; }

        [JsonPropertyName("final_amount")]
        public long FinalAmount { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("voucher_id")]
        public int VoucherId { get; set; }
    }
}
