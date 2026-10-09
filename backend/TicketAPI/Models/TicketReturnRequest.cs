namespace TicketAPI.Models;

// One customer request to return a ticket (UC_9). Status values live in ReturnRequestStatuses.
public partial class TicketReturnRequest
{
    public int TicketReturnRequestId { get; set; }

    public int TicketId { get; set; }

    public int OrderId { get; set; }

    public int EventId { get; set; }

    public int RequesterUserId { get; set; }

    public string Reason { get; set; } = null!;

    public string Status { get; set; } = "Pending";

    public long RefundAmount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public int? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewNote { get; set; }

    public DateTime? RefundedAt { get; set; }

    public string? RefundReference { get; set; }

    public string? RefundError { get; set; }

    public virtual Ticket Ticket { get; set; } = null!;
}
