namespace TicketAPI.Models;
public class AppliedEventChange
{
    public long ChangeId { get; set; }
    public int EventId { get; set; }
    public string Type { get; set; } = "";
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public int ScheduleVersion { get; set; }
    public string Reason { get; set; } = "";
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public DateTime AppliedAt { get; set; }
}
public class AffectedTicket
{
    public long Id { get; set; }
    public long ChangeId { get; set; }
    public int TicketId { get; set; }
    public int OrderId { get; set; }
    public int CustomerId { get; set; }
    public long PaidAmount { get; set; }
    public DateTime? NotifiedAt { get; set; }
}
