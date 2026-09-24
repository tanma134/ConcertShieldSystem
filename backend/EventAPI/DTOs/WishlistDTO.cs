namespace EventAPI.DTOs;

public sealed class MyWishlistDTO
{
    public int WishlistId { get; set; }
    public int EventId { get; set; }
    public string EventName { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string? EventImage { get; set; }
    public DateTime EventDate { get; set; }
    public DateTime EventEndDate { get; set; }
    public string? Location { get; set; }
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

public sealed class WishlistStatusDTO
{
    public int EventId { get; set; }
    public bool IsWishlisted { get; set; }
}

public sealed class AdminWishlistSummaryDTO
{
    public int EventId { get; set; }
    public string EventName { get; set; } = null!;
    public string? EventImage { get; set; }
    public string Status { get; set; } = null!;
    public int WishlistCount { get; set; }
}
