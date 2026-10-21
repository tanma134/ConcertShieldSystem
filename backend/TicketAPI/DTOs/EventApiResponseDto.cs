namespace BookingAPI.DTOS
{
    public class EventApiResponseDto
    {
        public int EventId { get; set; }
        public int OrganizerId { get; set; }
        public string Title { get; set; } = null!;
        public string? PosterUrl { get; set; }
        public DateTime StartsAt { get; set; }
        public DateTime EndsAt { get; set; }
        public string Status { get; set; } = null!;
        public int HoldTimeoutMinutes { get; set; } = 10;
        public string ReEntryPolicy { get; set; } = "NoReEntry";
        public int? ReEntryDurationMinutes { get; set; }
        public List<TicketTypeApiResponseDto> TicketTypes { get; set; } = new();
    }
}
