namespace EventAPI.DTOs
{
    public class EventImageResponseDTO
    {
        public int ImageId { get; set; }
        public int EventId { get; set; }
        public string ImageUrl { get; set; } = null!;
        public int SortOrder { get; set; }
        public bool IsMain { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ReorderImageDTO
    {
        public int ImageId { get; set; }
        public int SortOrder { get; set; }
    }

    public class UpdateImageDTO
    {
        public int? SortOrder { get; set; }
        public bool? IsMain { get; set; }
    }
}
