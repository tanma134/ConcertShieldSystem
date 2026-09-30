namespace BookingAPI.DTOS
{
    public class EventApiResponseDto
    {
        public int EventId { get; set; }
        public string Title { get; set; } = null!;
        public string? PosterUrl { get; set; }
        public DateTime StartsAt { get; set; }
        public DateTime EndsAt { get; set; }
        public string Status { get; set; } = null!;
        public List<TicketTypeApiResponseDto> TicketTypes { get; set; } = new();
    }
}
