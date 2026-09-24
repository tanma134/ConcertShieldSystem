namespace EventAPI.DTOs
{
    public class SlugAvailabilityDTO
    {
        public string Slug { get; set; } = null!;
        public bool Available { get; set; }
        public string Suggestion { get; set; } = null!;
    }
}
