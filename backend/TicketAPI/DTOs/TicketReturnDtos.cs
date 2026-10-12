namespace TicketAPI.DTOs;

// Body of POST api/ticket-returns (UC_9.1).
public class SubmitTicketReturnDto
{
    public int TicketId { get; set; }

    public string? Reason { get; set; }
}

// One return request as shown to the customer (UC_9.2).
public class TicketReturnDto
{
    public int TicketReturnRequestId { get; set; }
    public int TicketId { get; set; }
    public int OrderId { get; set; }
    public int EventId { get; set; }
    public int RequesterUserId { get; set; }
    public string? EventName { get; set; }
    public string? TicketTypeName { get; set; }
    public string Reason { get; set; } = null!;
    public string Status { get; set; } = null!;
    public long RefundAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public int? ReviewedBy { get; set; }
    public string? ReviewNote { get; set; }
    public bool CanCancel { get; set; }
    public DateTime? RefundedAt { get; set; }
    public string? RefundReference { get; set; }
    public string? RefundError { get; set; }
    public bool CanRetryRefund { get; set; }
}


// Staff/Admin decision for a pending return request.
public class ReviewTicketReturnDto
{
    public bool Approve { get; set; }
    public string? ReviewNote { get; set; }
}

// One ticket in "My tickets", with whether it can be returned right now.
public class MyTicketDto
{
    public int TicketId { get; set; }
    public Guid TicketCode { get; set; }
    public int OrderId { get; set; }
    public int EventId { get; set; }
    public int RequesterUserId { get; set; }
    public string? EventName { get; set; }
    public string? PosterUrl { get; set; }
    public DateTime? StartsAt { get; set; }
    public string? TicketTypeName { get; set; }
    public int? SeatId { get; set; }
    public string Status { get; set; } = null!;
    public DateTime? CheckedInAt { get; set; }
    public bool CanReturn { get; set; }
    public string ReturnCode { get; set; } = null!;
    public string ReturnMessage { get; set; } = null!;
    public long RefundEstimate { get; set; }
    public decimal RefundPercent { get; set; }
    public TicketReturnDto? LatestReturn { get; set; }
}
