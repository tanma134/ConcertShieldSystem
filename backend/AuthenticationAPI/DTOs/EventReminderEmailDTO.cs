using System.ComponentModel.DataAnnotations;

namespace AuthenticationAPI.DTOs;

public sealed class EventReminderEmailDTO
{
    [Range(1, int.MaxValue)]
    public int EventId { get; set; }

    [Range(1, int.MaxValue)]
    public int UserId { get; set; }

    [Required]
    [StringLength(200)]
    public string EventTitle { get; set; } = null!;

    [Required]
    [StringLength(250)]
    public string EventSlug { get; set; } = null!;

    public DateTime StartsAt { get; set; }

    [Required]
    [StringLength(50)]
    public string Timezone { get; set; } = null!;
}
