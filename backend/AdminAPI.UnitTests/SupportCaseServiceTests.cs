using AdminAPI.Data;
using AdminAPI.DTOs;
using AdminAPI.Models;
using AdminAPI.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AdminAPI.Tests;

// Unit tests for SupportCaseService following TDD principles (Feature 15)
public class SupportCaseServiceTests
{
    private AdminDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AdminDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AdminDbContext(options);
    }

    // Closing a case strictly requires a recorded resolution
    [Fact]
    public async Task ResolveCaseAsync_WithoutResolution_ThrowsArgumentException()
    {
        using var db = CreateInMemoryDbContext();
        var service = new SupportCaseService(db);

        var supportCase = new SupportCase
        {
            CaseId = 1,
            CaseNumber = "SC-TEST-001",
            CustomerName = "John Doe",
            CustomerEmail = "john@example.com",
            Title = "Ticket Scanning Issue",
            Description = "Ticket cannot be scanned at gate A",
            Status = "Assigned"
        };
        db.SupportCases.Add(supportCase);
        await db.SaveChangesAsync();

        var request = new ResolveCaseRequest { Resolution = "   " };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ResolveCaseAsync(1, request, 4, "Gate Staff One", "Staff")
        );
    }

    // Closed support case cannot be reassigned
    [Fact]
    public async Task AssignStaffAsync_WhenCaseIsClosed_ThrowsInvalidOperationException()
    {
        using var db = CreateInMemoryDbContext();
        var service = new SupportCaseService(db);

        var supportCase = new SupportCase
        {
            CaseId = 2,
            CaseNumber = "SC-TEST-002",
            CustomerName = "Jane Smith",
            CustomerEmail = "jane@example.com",
            Title = "Refund Inquiry",
            Description = "Refund status check",
            Status = "Closed"
        };
        db.SupportCases.Add(supportCase);
        await db.SaveChangesAsync();

        var assignReq = new AssignStaffRequest { StaffUserId = 4, StaffName = "Staff One" };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AssignStaffAsync(2, assignReq, 1, "Admin")
        );
    }

    // Create support case successfully generates CaseNumber and status Open
    [Fact]
    public async Task CreateCaseAsync_ValidInput_CreatesCaseWithOpenStatusAndCaseNumber()
    {
        using var db = CreateInMemoryDbContext();
        var service = new SupportCaseService(db);

        var createReq = new CreateSupportCaseRequest
        {
            CustomerName = "Bob Johnson",
            CustomerEmail = "bob@example.com",
            CustomerPhone = "+1234567890",
            Title = "App Crash on Checkout",
            Description = "Application crashes when opening ticket screen",
            Category = "Account & Security",
            Priority = "High",
            OrderId = 101,
            TicketId = 202
        };

        var result = await service.CreateCaseAsync(createReq, 4, "Gate Staff One");

        Assert.NotNull(result);
        Assert.Equal("Open", result.Status);
        Assert.StartsWith("SC-", result.CaseNumber);
        Assert.Equal(101, result.OrderId);
        Assert.Equal(202, result.TicketId);

        var saved = await db.SupportCases.FirstOrDefaultAsync(c => c.CaseId == result.CaseId);
        Assert.NotNull(saved);
        Assert.Equal("Open", saved.Status);
    }

    // Resolving case successfully marks status as Closed and records resolution
    [Fact]
    public async Task ResolveCaseAsync_WithValidResolution_ClosesCaseAndRecordsResolver()
    {
        using var db = CreateInMemoryDbContext();
        var service = new SupportCaseService(db);

        var supportCase = new SupportCase
        {
            CaseId = 3,
            CaseNumber = "SC-TEST-003",
            CustomerName = "Alice Brown",
            CustomerEmail = "alice@example.com",
            Title = "Duplicate Check-in",
            Description = "Ticket was mistakenly checked in",
            Status = "Assigned",
            AssignedStaffId = 4
        };
        db.SupportCases.Add(supportCase);
        await db.SaveChangesAsync();

        var request = new ResolveCaseRequest { Resolution = "New ticket QR issued at customer desk." };

        var result = await service.ResolveCaseAsync(3, request, 4, "Gate Staff One", "Staff");

        Assert.Equal("Closed", result.Status);
        Assert.Equal("New ticket QR issued at customer desk.", result.Resolution);
        Assert.Equal(4, result.ResolvedBy);
        Assert.Equal("Gate Staff One", result.ResolvedByName);
        Assert.NotNull(result.ResolvedAt);
    }

    // Staff visibility scope only returns assigned or unassigned cases
    [Fact]
    public async Task GetCasesAsync_ForStaff_OnlyReturnsAssignedAndUnassignedCases()
    {
        using var db = CreateInMemoryDbContext();
        var service = new SupportCaseService(db);

        db.SupportCases.AddRange(
            new SupportCase { CaseId = 10, CaseNumber = "SC-10", CustomerName = "A", CustomerEmail = "a@a.com", Title = "C1", Description = "D1", AssignedStaffId = 4 },
            new SupportCase { CaseId = 11, CaseNumber = "SC-11", CustomerName = "B", CustomerEmail = "b@b.com", Title = "C2", Description = "D2", AssignedStaffId = null },
            new SupportCase { CaseId = 12, CaseNumber = "SC-12", CustomerName = "C", CustomerEmail = "c@c.com", Title = "C3", Description = "D3", AssignedStaffId = 5 }
        );
        await db.SaveChangesAsync();

        var staffResult = await service.GetCasesAsync(new SupportCaseQuery(), currentUserId: 4, isAdmin: false);
        var adminResult = await service.GetCasesAsync(new SupportCaseQuery(), currentUserId: 1, isAdmin: true);

        Assert.Equal(2, staffResult.Count); // Staff 4 sees case 10 and 11
        Assert.Equal(3, adminResult.Count); // Admin sees all 3 cases
    }

    [Fact]
    public async Task GetCaseByOrderIdAsync_WhenExists_ReturnsCase()
    {
        using var db = CreateInMemoryDbContext();
        var service = new SupportCaseService(db);

        db.SupportCases.Add(new SupportCase
        {
            CaseId = 20,
            CaseNumber = "SC-20",
            OrderId = 999,
            CustomerName = "David",
            CustomerEmail = "david@example.com",
            Title = "QR Issue",
            Description = "QR invalid",
            Status = "Open"
        });
        await db.SaveChangesAsync();

        var found = await service.GetCaseByOrderIdAsync(999);
        Assert.NotNull(found);
        Assert.Equal("SC-20", found.CaseNumber);
        Assert.Equal(999, found.OrderId);

        var notFound = await service.GetCaseByOrderIdAsync(888);
        Assert.Null(notFound);
    }

    [Fact]
    public async Task ResolveCaseAsync_TriggersEmailNotification()
    {
        using var db = CreateInMemoryDbContext();
        var emailService = new TestSupportEmailService();
        var service = new SupportCaseService(db, emailService);

        var supportCase = new SupportCase
        {
            CaseId = 30,
            CaseNumber = "SC-30",
            CustomerName = "Emma",
            CustomerEmail = "emma@example.com",
            Title = "Check-in query",
            Description = "Need confirmation",
            Status = "Assigned",
            AssignedStaffId = 2
        };
        db.SupportCases.Add(supportCase);
        await db.SaveChangesAsync();

        var request = new ResolveCaseRequest { Resolution = "All details confirmed and re-sent." };
        await service.ResolveCaseAsync(30, request, 2, "Support Agent", "Staff");

        Assert.True(emailService.EmailSent);
        Assert.Equal("emma@example.com", emailService.SentToEmail);
        Assert.Equal("SC-30", emailService.SentCaseNumber);
    }

    [Fact]
    public async Task AssignStaffAsync_TriggersEmailAndNotification()
    {
        using var db = CreateInMemoryDbContext();
        var emailService = new TestSupportEmailService();
        var service = new SupportCaseService(db, emailService);

        var supportCase = new SupportCase
        {
            CaseId = 40,
            CaseNumber = "SC-40",
            CustomerName = "Michael",
            CustomerEmail = "michael@example.com",
            Title = "Ticket exchange",
            Description = "Wants to change seats",
            Status = "Open"
        };
        db.SupportCases.Add(supportCase);
        await db.SaveChangesAsync();

        var assignReq = new AssignStaffRequest
        {
            StaffUserId = 5,
            StaffName = "Support Officer",
            StaffEmail = "officer@concertshield.com"
        };

        var result = await service.AssignStaffAsync(40, assignReq, 1, "Admin");

        Assert.Equal("Assigned", result.Status);
        Assert.Equal(5, result.AssignedStaffId);
        Assert.Equal("Support Officer", result.AssignedStaffName);
        Assert.True(emailService.AssignedEmailSent);
        Assert.Equal("officer@concertshield.com", emailService.AssignedToEmail);
        Assert.Equal("SC-40", emailService.AssignedCaseNumber);
    }

    private class TestSupportEmailService : ISupportEmailService
    {
        public bool EmailSent { get; private set; }
        public string? SentToEmail { get; private set; }
        public string? SentCaseNumber { get; private set; }

        public bool AssignedEmailSent { get; private set; }
        public string? AssignedToEmail { get; private set; }
        public string? AssignedCaseNumber { get; private set; }

        public Task SendCaseResolvedEmailAsync(string toEmail, string customerName, string caseNumber, string caseTitle, string resolution, string resolvedByName, int? orderId)
        {
            EmailSent = true;
            SentToEmail = toEmail;
            SentCaseNumber = caseNumber;
            return Task.CompletedTask;
        }

        public Task SendCaseAssignedEmailAsync(string toEmail, string staffName, string caseNumber, string caseTitle, string priority, string category, string customerName, int? orderId, int caseId)
        {
            AssignedEmailSent = true;
            AssignedToEmail = toEmail;
            AssignedCaseNumber = caseNumber;
            return Task.CompletedTask;
        }
    }
}
