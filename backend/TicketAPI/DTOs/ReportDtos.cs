using TicketAPI.Domain;

namespace TicketAPI.DTOs;

// Response of GET api/organizer/events/{id}/revenue (UC_13.1 / UC_13.2).
public class RevenueReportDto
{
    public int EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int? TicketTypeId { get; set; }
    public RevenueSummary Summary { get; set; } = null!;
    public List<TicketTypeOptionDto> TicketTypes { get; set; } = new();
}

// A ticket type the dashboard filter can pick.
public class TicketTypeOptionDto
{
    public int TicketTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
}

// Response of GET api/organizer/events/{id}/checkin (summary shown above the export button).
public class CheckinReportDto
{
    public int EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public CheckinSummary Summary { get; set; } = null!;
}

// A generated CSV file ready to be returned by a controller.
public class ExportFileDto
{
    public string FileName { get; set; } = string.Empty;
    public byte[] Content { get; set; } = Array.Empty<byte>();
}
