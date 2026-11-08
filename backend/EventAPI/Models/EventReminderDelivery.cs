namespace EventAPI.Models;

public sealed class EventReminderDelivery
{
    public int EventId { get; set; }
    public int UserId { get; set; }
    public DateTime ScheduledStartAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
    public int AttemptCount { get; set; }
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
}
