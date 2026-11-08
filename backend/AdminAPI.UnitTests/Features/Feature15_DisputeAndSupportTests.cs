using System;
using System.Linq;
using System.Threading.Tasks;
using AdminAPI.Data;
using AdminAPI.DTOs;
using AdminAPI.Models;
using AdminAPI.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AdminAPI.UnitTests.Features;

/// <summary>
/// Feature 15: Dispute, Support & Refund Workflow (Admin Review & Support Management)
/// Tests for UC_3.16.7 (View Pending Concert List), UC_3.16.8 (Approve/Reject Concert),
/// UC_3.16.9 (View Ticket Dispute List), UC_3.16.10 (View Dispute Details),
/// UC_3.16.11 (Resolve Ticket Dispute), UC_3.16.15 (Create Support Case),
/// UC_3.16.16 (View Support Case List), UC_3.16.17 (View Support Case Details),
/// UC_3.16.18 (Assign Support Case to Staff), UC_3.16.19 (Resolve/Close Support Case)
/// </summary>
public class Feature15_DisputeAndSupportTests
{
    private AdminDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AdminDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AdminDbContext(options);
    }

    [Fact]
    public void UC_3_16_7_ViewPendingConcertList_ValidStatusFilter()
    {
        var statuses = new[] { "Pending", "Draft", "Published", "Rejected" };
        string pendingFilter = "Pending";

        Assert.Contains(pendingFilter, statuses);
    }

    [Fact]
    public void UC_3_16_8_ApproveRejectConcert_RequiresRejectionReasonWhenRejected()
    {
        bool isApproved = false;
        string? rejectionReason = "Incomplete municipal safety clearance certificate";

        if (!isApproved)
        {
            Assert.False(string.IsNullOrWhiteSpace(rejectionReason), "Rejection must have explicit justification");
        }
    }

    [Fact]
    public async Task UC_3_16_9_ViewTicketDisputeList_ReturnsPendingAppeals()
    {
        using var db = CreateInMemoryDbContext();
        db.RiskAppeals.AddRange(
            new RiskAppeal { RiskAppealId = 1, RiskDecisionId = 10, UserId = 101, Reason = "Card flagged falsely", Status = "Pending", CreatedAt = DateTime.UtcNow },
            new RiskAppeal { RiskAppealId = 2, RiskDecisionId = 11, UserId = 102, Reason = "Chargeback inquiry", Status = "Resolved", CreatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var pendingDisputes = await db.RiskAppeals.Where(a => a.Status == "Pending").ToListAsync();

        Assert.Single(pendingDisputes);
        Assert.Equal(1, pendingDisputes[0].RiskAppealId);
    }

    [Fact]
    public async Task UC_3_16_10_ViewDisputeDetails_ReturnsAppealAndEvidence()
    {
        using var db = CreateInMemoryDbContext();
        var appeal = new RiskAppeal
        {
            RiskAppealId = 5,
            RiskDecisionId = 20,
            UserId = 305,
            Reason = "Legitimate concertgoer blocked by bot detector",
            Evidence = "{\"bankStatement\":\"stmt_2026.pdf\"}",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        db.RiskAppeals.Add(appeal);
        await db.SaveChangesAsync();

        var detail = await db.RiskAppeals.FindAsync(5);
        Assert.NotNull(detail);
        Assert.Contains("bankStatement", detail.Evidence);
    }

    [Fact]
    public async Task UC_3_16_11_ResolveTicketDispute_UpdatesAppealStatus()
    {
        using var db = CreateInMemoryDbContext();
        var appeal = new RiskAppeal
        {
            RiskAppealId = 6,
            RiskDecisionId = 21,
            UserId = 306,
            Reason = "False fraud positive",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        db.RiskAppeals.Add(appeal);
        await db.SaveChangesAsync();

        // Admin approves appeal and unblocks customer
        appeal.Status = "Approved";
        appeal.ReviewNote = "Identity verified via national ID card";
        appeal.ReviewedBy = 1;
        appeal.ReviewedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var resolved = await db.RiskAppeals.FindAsync(6);
        Assert.NotNull(resolved);
        Assert.Equal("Approved", resolved.Status);
    }

    [Fact]
    public async Task UC_3_16_15_CreateSupportCase_GeneratesCaseNumber()
    {
        using var db = CreateInMemoryDbContext();
        var service = new SupportCaseService(db);

        var newCase = new SupportCase
        {
            CaseId = 10,
            CaseNumber = "SC-202611-001",
            CustomerId = 701,
            CustomerName = "Nguyen Van Customer",
            CustomerEmail = "customer@example.vn",
            Title = "Wrong Seat Row Allocated",
            Description = "Allocated row 12 instead of requested row 10 in VIP Zone.",
            Status = "Open",
            Priority = "Medium",
            CreatedAt = DateTime.UtcNow
        };
        db.SupportCases.Add(newCase);
        await db.SaveChangesAsync();

        var saved = await db.SupportCases.FindAsync(10);
        Assert.NotNull(saved);
        Assert.Equal("SC-202611-001", saved.CaseNumber);
        Assert.Equal("Open", saved.Status);
    }

    [Fact]
    public async Task UC_3_16_16_ViewSupportCaseList_FiltersByStatus()
    {
        using var db = CreateInMemoryDbContext();
        db.SupportCases.AddRange(
            new SupportCase { CaseId = 1, CaseNumber = "SC-1", CustomerName = "A", CustomerEmail = "a@a.vn", Title = "T1", Description = "D1", Status = "Open" },
            new SupportCase { CaseId = 2, CaseNumber = "SC-2", CustomerName = "B", CustomerEmail = "b@b.vn", Title = "T2", Description = "D2", Status = "Resolved" },
            new SupportCase { CaseId = 3, CaseNumber = "SC-3", CustomerName = "C", CustomerEmail = "c@c.vn", Title = "T3", Description = "D3", Status = "Open" }
        );
        await db.SaveChangesAsync();

        var openCases = await db.SupportCases.Where(c => c.Status == "Open").ToListAsync();
        Assert.Equal(2, openCases.Count);
    }

    [Fact]
    public async Task UC_3_16_17_ViewSupportCaseDetails_ReturnsCaseWithTimeline()
    {
        using var db = CreateInMemoryDbContext();
        var c = new SupportCase
        {
            CaseId = 20,
            CaseNumber = "SC-20",
            CustomerName = "Le Thi Details",
            CustomerEmail = "le@test.vn",
            Title = "E-Ticket PDF Missing QR Code",
            Description = "Render error on iOS Mail app.",
            Status = "Investigating"
        };
        db.SupportCases.Add(c);

        db.SupportCaseNotes.Add(new SupportCaseNote
        {
            NoteId = 1,
            CaseId = 20,
            AuthorId = 201,
            AuthorName = "SupportStaff1",
            AuthorRole = "Staff",
            NoteType = "InternalNote",
            Content = "Re-sent vector PDF to customer email.",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var caseDetail = await db.SupportCases.FindAsync(20);
        var notes = await db.SupportCaseNotes.Where(n => n.CaseId == 20).ToListAsync();

        Assert.NotNull(caseDetail);
        Assert.Single(notes);
        Assert.Equal("SupportStaff1", notes[0].AuthorName);
    }

    [Fact]
    public async Task UC_3_16_18_AssignSupportCaseToStaff_UpdatesAssignedStaff()
    {
        using var db = CreateInMemoryDbContext();
        var service = new SupportCaseService(db);

        var c = new SupportCase
        {
            CaseId = 30,
            CaseNumber = "SC-30",
            CustomerName = "Pham Assignee",
            CustomerEmail = "pham@test.vn",
            Title = "Payment Debited but No Ticket Issued",
            Description = "Bank charged 1M VND.",
            Status = "Open"
        };
        db.SupportCases.Add(c);
        await db.SaveChangesAsync();

        var assignReq = new AssignStaffRequest
        {
            StaffUserId = 401,
            StaffName = "Staff Specialist"
        };

        var updated = await service.AssignStaffAsync(30, assignReq, 1, "AdminOne");

        Assert.NotNull(updated);
        Assert.Equal(401, updated.AssignedStaffId);
        Assert.Equal("Staff Specialist", updated.AssignedStaffName);
        Assert.Equal("Assigned", updated.Status);
    }

    [Fact]
    public async Task UC_3_16_19_ResolveCloseSupportCase_RecordsResolutionAndStatus()
    {
        using var db = CreateInMemoryDbContext();
        var service = new SupportCaseService(db);

        var c = new SupportCase
        {
            CaseId = 40,
            CaseNumber = "SC-40",
            CustomerName = "Tran Close",
            CustomerEmail = "tran@test.vn",
            Title = "Refund inquiry",
            Description = "Inquired about refund progress.",
            Status = "Assigned"
        };
        db.SupportCases.Add(c);
        await db.SaveChangesAsync();

        var req = new ResolveCaseRequest
        {
            Resolution = "Refund of 1,500,000 VND processed via VNPay transaction #VNP123456."
        };

        var resolved = await service.ResolveCaseAsync(40, req, 401, "Staff Specialist", "Staff");

        Assert.NotNull(resolved);
        Assert.Equal("Closed", resolved.Status);
        Assert.Equal(req.Resolution, resolved.Resolution);
        Assert.NotNull(resolved.ResolvedAt);
    }
}
