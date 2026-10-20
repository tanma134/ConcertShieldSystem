using System;
using System.Collections.Generic;

namespace PaymentAPI.DTOs
{
    // Body of POST api/internal/refunds (called by TicketAPI after staff approve a return).
    public class CreateRefundRequestDto
    {
        public int ReturnRequestId { get; set; }
        public int OrderId { get; set; }
        public long Amount { get; set; }
    }

    public class RefundResultDto
    {
        public int? RefundId { get; set; }
        public string Status { get; set; } = "Failed"; // Refunded | Failed
        public string? Reference { get; set; }
        public string? Error { get; set; }
        public string? ErrorCode { get; set; }
    }

    // UC 3.16.20: Process Full / Partial Refund
    public class ProcessRefundRequestDto
    {
        public int OrderId { get; set; }
        public string RefundType { get; set; } = "Full"; // Full | Partial
        public long Amount { get; set; }
        public string Reason { get; set; } = "Customer Request";
        public string? Notes { get; set; }
    }

    // UC 3.16.21: Retry Failed Refund
    public class RetryRefundRequestDto
    {
        public string Strategy { get; set; } = "Immediate"; // Immediate | Scheduled
    }

    // Summary of Order payment & refundable balance
    public class OrderRefundSummaryDto
    {
        public int OrderId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerEmail { get; set; }
        public string? TransactionId { get; set; }
        public DateTime? PurchaseDate { get; set; }
        public long OriginalAmount { get; set; }
        public long AlreadyRefunded { get; set; }
        public long MaxRefundableAmount { get; set; }
        public string? OrderStatus { get; set; }
        public List<RefundItemDto> PreviousRefunds { get; set; } = new();
    }

    public class EventOptionDto
    {
        public int EventId { get; set; }
        public string EventName { get; set; } = "";
        public int RefundCount { get; set; }
    }

    public class RefundItemDto
    {
        public int PaymentRefundId { get; set; }
        public string RefundReference { get; set; } = "";
        public int OrderId { get; set; }
        public string? CustomerName { get; set; }
        public int? EventId { get; set; }
        public string? EventName { get; set; }
        public string RefundType { get; set; } = "Full";
        public long Amount { get; set; }
        public long? GatewayAmount { get; set; }
        public string Status { get; set; } = "Pending";
        public string MatchStatus { get; set; } = "Matched";
        public string? Reason { get; set; }
        public string? ProcessedByUserName { get; set; }
        public string? GatewayErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public int RetryCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    // UC 3.16.22: Refund Reconciliation Report
    public class RefundReconciliationReportDto
    {
        public int TotalRefunds { get; set; }
        public long TotalAmount { get; set; }
        public int PendingReconciliation { get; set; }
        public int MismatchedCount { get; set; }
        public List<RefundItemDto> Refunds { get; set; } = new();
        public List<EventOptionDto> AvailableEvents { get; set; } = new();
    }

    public class ReconciliationFilterDto
    {
        public string? Status { get; set; }
        public string? MatchStatus { get; set; }
        public string? Search { get; set; }
        public string? DateRange { get; set; }
        public int? EventId { get; set; }
    }
}
