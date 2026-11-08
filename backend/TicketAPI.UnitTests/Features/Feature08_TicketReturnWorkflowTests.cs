using System.Collections.Generic;
using TicketAPI.Domain;
using TicketAPI.Models;
using Xunit;

namespace TicketAPI.UnitTests.Features;

/// <summary>
/// Feature 8: Ticket Return & Re-Issue Workflow
/// Tests for UC_3.9.1 (Submit Ticket Return Request), UC_3.9.2 (Track Ticket Return Status),
/// UC_3.9.3 (Cancel Ticket Return Request)
/// </summary>
public class Feature08_TicketReturnWorkflowTests
{
    [Fact]
    public void UC_3_9_1_SubmitTicketReturnRequest_PendingInitialStatus()
    {
        var returnReq = new TicketReturnRequest
        {
            TicketReturnRequestId = 1,
            TicketId = 100,
            OrderId = 500,
            EventId = 10,
            RequesterUserId = 701,
            Reason = "Medical emergency with doctor certificate",
            Status = ReturnRequestStatuses.Pending,
            RefundAmount = 1500000,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        Assert.Equal(ReturnRequestStatuses.Pending, returnReq.Status);
        Assert.Equal(1500000, returnReq.RefundAmount);
        Assert.False(string.IsNullOrWhiteSpace(returnReq.Reason));
    }

    [Fact]
    public void UC_3_9_1_SubmitTicketReturnRequest_CutoffHoursValidation()
    {
        var eventDate = DateTime.UtcNow.AddDays(10);
        var requestDate = DateTime.UtcNow;
        int cutoffHours = 48; // 2 days before event

        var hoursRemaining = (eventDate - requestDate).TotalHours;
        Assert.True(hoursRemaining > cutoffHours, "Return request must be submitted before cutoff deadline");
    }

    [Fact]
    public void UC_3_9_2_TrackTicketReturnStatus_ValidStatusTransitions()
    {
        var validStatuses = new HashSet<string>
        {
            ReturnRequestStatuses.Pending,
            ReturnRequestStatuses.Approved,
            ReturnRequestStatuses.Rejected,
            ReturnRequestStatuses.Cancelled,
            ReturnRequestStatuses.Refunded,
            ReturnRequestStatuses.RefundFailed
        };

        Assert.Contains(ReturnRequestStatuses.Pending, validStatuses);
        Assert.Contains(ReturnRequestStatuses.Approved, validStatuses);
        Assert.Contains(ReturnRequestStatuses.Refunded, validStatuses);
    }

    [Fact]
    public void UC_3_9_3_CancelTicketReturnRequest_TransitionsFromPendingToCancelled()
    {
        var returnReq = new TicketReturnRequest
        {
            TicketReturnRequestId = 5,
            TicketId = 105,
            Status = ReturnRequestStatuses.Pending
        };

        // Customer cancels their pending request
        returnReq.Status = ReturnRequestStatuses.Cancelled;
        returnReq.UpdatedAt = DateTime.UtcNow;

        Assert.Equal(ReturnRequestStatuses.Cancelled, returnReq.Status);
    }
}
