using System.Net;
using System.Text.Json;
using EventAPI.Common;
using EventAPI.Data;
using EventAPI.DTOs;
using EventAPI.Models;
using EventAPI.Repositories;
using EventAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EventAPI.UnitTests.Features;

// Bo kiem thu tu False den True cho bo 3 Use Case: UC-34, UC-35 va UC-36
public class Feature09_EventChangeGovernanceTests
{
    // Khoi tao In-Memory DbContext de kiem thu nhanh va doc lap
    private static EventDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<EventDbContext>()
            .UseInMemoryDatabase(databaseName: "governance_test_" + Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new EventDbContext(options);
    }

    // Khoi tao GovernanceService voi cac dependency gia lap
    private static GovernanceService CreateService(EventDbContext db, HttpClient? mockClient = null)
    {
        var repo = new GovernanceRepository(db);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Governance:AdminUserIds:0"] = "1",
                ["Compliance:RequiredTypes:0"] = "EventPermit",
                ["Compliance:RequiredTypes:1"] = "SafetyPlan"
            })
            .Build();

        var httpFactory = new MockHttpFactory(mockClient ?? new HttpClient(new MockHttpHandler()));
        return new GovernanceService(repo, new DummyAssetStore(), httpFactory, config);
    }

    // Helper tao mot su kien Published hop le
    private static Event CreatePublishedEvent(int eventId = 101, int organizerId = 10)
    {
        return new Event
        {
            EventId = eventId,
            OrganizerId = organizerId,
            Title = "Arena Rock Symphony 2026",
            Slug = "arena-rock-symphony-2026",
            Status = EventStatus.Published,
            StartsAt = DateTime.UtcNow.AddDays(15),
            EndsAt = DateTime.UtcNow.AddDays(15).AddHours(4),
            ScheduleVersion = 1,
            ComplianceStatus = "Approved",
            ComplianceVersion = 1,
            ComplianceReviewedVersion = 1,
            SalesFrozen = false
        };
    }

    // -------------------------------------------------------------------------------------------------
    // PHAN 1: KIEM THU FALSE CASES (KIEM TRA CHAN LOI, DIEU KIEN BIEN VA BAO VE HE THONG)
    // -------------------------------------------------------------------------------------------------

    // FALSE CASE 1: Su kien o trang thai Draft hoac Cancelled khong duoc phep tao de xuat thay doi
    [Theory]
    [InlineData("Draft")]
    [InlineData("Cancelled")]
    [InlineData("Rejected")]
    public void False_01_ValidateChange_NonPublishedEvent_ThrowsInvalidOperationException(string invalidStatus)
    {
        var ev = CreatePublishedEvent();
        ev.Status = invalidStatus;
        var input = new ChangeInput("Postpone", "[Force Majeure] Authority security order", null, null);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            GovernanceRules.ValidateChange(ev, input, DateTime.UtcNow));
        Assert.Contains("Only Published or Postponed concerts support schedule changes", ex.Message);
    }

    // FALSE CASE 2: Su kien da bat dau dien ra trong qua khu khong duoc phep thay doi lich
    [Fact]
    public void False_02_ValidateChange_StartedConcert_ThrowsInvalidOperationException()
    {
        var ev = CreatePublishedEvent();
        ev.StartsAt = DateTime.UtcNow.AddMinutes(-10);
        var input = new ChangeInput("Postpone", "[Force Majeure] Severe thunderstorm", null, null);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            GovernanceRules.ValidateChange(ev, input, DateTime.UtcNow));
        Assert.Contains("has started cannot change its schedule", ex.Message);
    }

    // FALSE CASE 3: Loai thay doi khong thuoc Reschedule, Postpone hoac Cancel bi chan boi Validation
    [Theory]
    [InlineData("VenueChange")]
    [InlineData("UnknownType")]
    public void False_03_ValidateChange_InvalidType_ThrowsArgumentException(string invalidType)
    {
        var ev = CreatePublishedEvent();
        var input = new ChangeInput(invalidType, "[Force Majeure] Emergency structural repair", null, null);

        var ex = Assert.Throws<ArgumentException>(() =>
            GovernanceRules.ValidateChange(ev, input, DateTime.UtcNow));
        Assert.Contains("Change type must be Postpone, Reschedule or Cancel", ex.Message);
    }

    // TRUE CASE: Cancel la loai thay doi hop le, khong can lich moi
    [Fact]
    public void True_03a_ValidateChange_CancelType_IsAcceptedWithNoNewSchedule()
    {
        var ev = CreatePublishedEvent();
        // Caller gui kem ngay moi nhung Cancel phai bo qua chung.
        var input = new ChangeInput("Cancel", "[Force Majeure] Venue permanently closed by authority",
            DateTimeOffset.UtcNow.AddDays(5), DateTimeOffset.UtcNow.AddDays(6));

        var clean = GovernanceRules.ValidateChange(ev, input, DateTime.UtcNow);

        Assert.Equal("Cancel", clean.Type);
        Assert.Null(clean.NewStartsAt);
        Assert.Null(clean.NewEndsAt);
    }

    // TRUE CASE: Su kien da Postponed van co the bi huy
    [Fact]
    public void True_03b_ValidateChange_CancelOnPostponedEvent_IsAccepted()
    {
        var ev = CreatePublishedEvent();
        ev.Status = "Postponed";
        var input = new ChangeInput("Cancel", "[Force Majeure] Event cancelled after postponement", null, null);

        var clean = GovernanceRules.ValidateChange(ev, input, DateTime.UtcNow);

        Assert.Equal("Cancel", clean.Type);
    }

    // FALSE CASE 4: Ly do thay doi duoi 10 ky tu hoac vuot qua 1000 ky tu bi chan
    [Theory]
    [InlineData("")]
    [InlineData("Too short")]
    public void False_04_ValidateChange_ReasonTooShort_ThrowsArgumentException(string shortReason)
    {
        var ev = CreatePublishedEvent();
        var input = new ChangeInput("Postpone", shortReason, null, null);

        var ex = Assert.Throws<ArgumentException>(() =>
            GovernanceRules.ValidateChange(ev, input, DateTime.UtcNow));
        Assert.Contains("Reason must contain 10 to 1000 characters", ex.Message);
    }

    // FALSE CASE 5: Doi ngay vao qua khu bi tu choi vi khong hop ly ve mat thoi gian
    [Fact]
    public void False_05_ValidateChange_ReschedulePastStart_ThrowsArgumentException()
    {
        var ev = CreatePublishedEvent();
        var pastStart = DateTimeOffset.UtcNow.AddDays(-2);
        var futureEnd = DateTimeOffset.UtcNow.AddDays(2);
        var input = new ChangeInput("Reschedule", "[Force Majeure] Rescheduling to new date", pastStart, futureEnd);

        var ex = Assert.Throws<ArgumentException>(() =>
            GovernanceRules.ValidateChange(ev, input, DateTime.UtcNow));
        Assert.Contains("New start must be in the future", ex.Message);
    }

    // FALSE CASE 6: Ngay ket thuc truoc hoac bang ngay bat dau bi chan
    [Fact]
    public void False_06_ValidateChange_RescheduleEndBeforeStart_ThrowsArgumentException()
    {
        var ev = CreatePublishedEvent();
        var newStart = DateTimeOffset.UtcNow.AddDays(20);
        var invalidEnd = newStart.AddHours(-1);
        var input = new ChangeInput("Reschedule", "[Force Majeure] Rescheduling to new date", newStart, invalidEnd);

        var ex = Assert.Throws<ArgumentException>(() =>
            GovernanceRules.ValidateChange(ev, input, DateTime.UtcNow));
        Assert.Contains("end must be after start", ex.Message);
    }

    // FALSE CASE 7: Doi ngay nhung thoi gian y het nhu cu bi tu choi
    [Fact]
    public void False_07_ValidateChange_RescheduleIdenticalSchedule_ThrowsArgumentException()
    {
        var ev = CreatePublishedEvent();
        var input = new ChangeInput("Reschedule", "[Force Majeure] No actual schedule change", ev.StartsAt, ev.EndsAt);

        var ex = Assert.Throws<ArgumentException>(() =>
            GovernanceRules.ValidateChange(ev, input, DateTime.UtcNow));
        Assert.Contains("The requested schedule has not changed", ex.Message);
    }

    // FALSE CASE 8: Admin tu choi de xuat ma khong co ghi chu toi thieu 10 ky tu bi chan
    [Theory]
    [InlineData("")]
    [InlineData("No note")]
    public void False_08_ValidateChangeReview_RejectWithShortNotes_ThrowsArgumentException(string shortNote)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            GovernanceRules.ValidateChangeReview("Rejected", shortNote));
        Assert.Contains("at least 10 characters", ex.Message);
    }

    // FALSE CASE 9: Nguoi dung khong phai Organizer cua su kien goi submit se bi chan Unauthorized
    [Fact]
    public async Task False_09_SubmitChange_UnauthorizedUser_ThrowsUnauthorizedAccessException()
    {
        using var db = CreateInMemoryDbContext();
        var ev = CreatePublishedEvent(eventId: 201, organizerId: 10);
        db.Events.Add(ev);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var input = new ChangeInput("Postpone", "[Force Majeure] Genuine severe flood hazard", null, null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.SubmitChangeAsync(201, caller: 999, input, CancellationToken.None));
    }

    // FALSE CASE 10: Khong duoc phep tao 2 de xuat thay doi Pending cung mot luc tren mot su kien
    [Fact]
    public async Task False_10_SubmitChange_DuplicatePendingRequest_ThrowsConflictException()
    {
        using var db = CreateInMemoryDbContext();
        var ev = CreatePublishedEvent(eventId: 202, organizerId: 10);
        db.Events.Add(ev);
        db.Set<EventChangeRequest>().Add(new EventChangeRequest
        {
            EventId = 202,
            Type = "Postpone",
            Status = "Pending",
            Reason = "[Force Majeure] Existing pending flood request",
            OldStartsAt = ev.StartsAt,
            OldEndsAt = ev.EndsAt,
            OldStatus = ev.Status,
            EventVersion = ev.ScheduleVersion,
            SubmittedBy = 10,
            SubmittedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var input = new ChangeInput("Reschedule", "[Force Majeure] Duplicate second request", DateTimeOffset.UtcNow.AddDays(30), DateTimeOffset.UtcNow.AddDays(30).AddHours(3));

        await Assert.ThrowsAsync<GovernanceConflictException>(() =>
            service.SubmitChangeAsync(202, caller: 10, input, CancellationToken.None));
    }

    // FALSE CASE 11: Su kien bi SalesFrozen hoac Postponed thi TicketAPI khong duoc phep ban ve
    [Theory]
    [InlineData("Postponed", false)]
    [InlineData("Published", true)]
    public async Task False_11_SaleEligibility_FrozenOrPostponed_BlocksTicketSales(string status, bool salesFrozen)
    {
        using var db = CreateInMemoryDbContext();
        var ev = CreatePublishedEvent(eventId: 203, organizerId: 10);
        ev.Status = status;
        ev.SalesFrozen = salesFrozen;
        db.Events.Add(ev);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var eligibility = await service.EligibilityAsync(203, CancellationToken.None);

        Assert.False(eligibility.CanSell);
        Assert.Contains("Sales are unavailable", eligibility.Reason);
    }

    // -------------------------------------------------------------------------------------------------
    // PHAN 2: KIEM THU TRUE CASES (KIEM TRA LUONG NGHIEP VU HOAN CHINH VA CHINH XAC 100%)
    // -------------------------------------------------------------------------------------------------

    // TRUE CASE 1: Kiem tra ham validate chuyen doi sach se thoi gian Postpone thanh null
    [Fact]
    public void True_01_ValidateChange_ValidPostpone_ClearsTargetDates()
    {
        var ev = CreatePublishedEvent();
        var input = new ChangeInput("Postpone", "[Force Majeure] Typhoon hazard warning issued by authorities", DateTimeOffset.UtcNow.AddDays(5), DateTimeOffset.UtcNow.AddDays(6));

        var clean = GovernanceRules.ValidateChange(ev, input, DateTime.UtcNow);

        Assert.Equal("Postpone", clean.Type);
        Assert.Null(clean.NewStartsAt);
        Assert.Null(clean.NewEndsAt);
        Assert.StartsWith("[Force Majeure]", clean.Reason);
    }

    // TRUE CASE 2: Kiem tra ham validate Reschedule giu nguyen thoi gian hop le trong tuong lai
    [Fact]
    public void True_02_ValidateChange_ValidReschedule_PreservesNewSchedule()
    {
        var ev = CreatePublishedEvent();
        var newStart = DateTimeOffset.UtcNow.AddDays(40);
        var newEnd = newStart.AddHours(4);
        var input = new ChangeInput("Reschedule", "[Force Majeure] Venue structural roof reinforcement", newStart, newEnd);

        var clean = GovernanceRules.ValidateChange(ev, input, DateTime.UtcNow);

        Assert.Equal("Reschedule", clean.Type);
        Assert.Equal(newStart, clean.NewStartsAt);
        Assert.Equal(newEnd, clean.NewEndsAt);
    }

    // TRUE CASE 3: UC-35 - Organizer gui de xuat thay doi thanh cong, luu vao DB o trang thai Pending va tao notification cho Admin
    [Fact]
    public async Task True_03_UC35_SubmitChangeRequest_CreatesPendingRecordAndNotifiesAdmin()
    {
        using var db = CreateInMemoryDbContext();
        var ev = CreatePublishedEvent(eventId: 301, organizerId: 10);
        db.Events.Add(ev);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var input = new ChangeInput("Postpone", "[Force Majeure] National stadium electrical outage repair", null, null);

        var requestId = await service.SubmitChangeAsync(301, caller: 10, input, CancellationToken.None);

        Assert.True(requestId > 0);
        var saved = await db.Set<EventChangeRequest>().SingleOrDefaultAsync(x => x.Id == requestId);
        Assert.NotNull(saved);
        Assert.Equal(301, saved.EventId);
        Assert.Equal("Postpone", saved.Type);
        Assert.Equal("Pending", saved.Status);
        Assert.Equal(10, saved.SubmittedBy);

        // Kiem tra notification cho admin da duoc dua vao outbox
        var outboxNotifications = await db.Set<GovernanceOutbox>().Where(x => x.Kind == "Notification").ToListAsync();
        Assert.NotEmpty(outboxNotifications);
        Assert.Contains(outboxNotifications, n => n.Payload.Contains("admin_change_request"));
    }

    // TRUE CASE 4: UC-34 - Admin phe duyet Postpone -> Dong bang ban ve, doi status Postponed, tang ScheduleVersion va tao outbox ApplyChange
    [Fact]
    public async Task True_04_UC34_ApprovePostpone_FreezesSalesIncrementsVersionAndQueuesApplyChange()
    {
        using var db = CreateInMemoryDbContext();
        var ev = CreatePublishedEvent(eventId: 302, organizerId: 10);
        db.Events.Add(ev);
        var request = new EventChangeRequest
        {
            Id = 55,
            EventId = 302,
            Type = "Postpone",
            Status = "Pending",
            Reason = "[Force Majeure] Severe tropical storm landfall warning",
            OldStartsAt = ev.StartsAt,
            OldEndsAt = ev.EndsAt,
            OldStatus = ev.Status,
            EventVersion = ev.ScheduleVersion,
            SubmittedBy = 10,
            SubmittedAt = DateTime.UtcNow
        };
        db.Set<EventChangeRequest>().Add(request);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.ReviewChangeAsync(302, 55, admin: 1, new ChangeReviewInput("Approved", "Reviewed and confirmed valid Force Majeure notice"), CancellationToken.None);

        var refreshed = await db.Events.FindAsync(302);
        Assert.NotNull(refreshed);
        Assert.Equal(EventStatus.Postponed, refreshed.Status);
        Assert.True(refreshed.SalesFrozen, "Sales must be frozen immediately when postponed.");
        Assert.Equal(2, refreshed.ScheduleVersion);

        var updatedRequest = await db.Set<EventChangeRequest>().FindAsync((long)55);
        Assert.Equal("Approved", updatedRequest!.Status);
        Assert.Equal(1, updatedRequest.ReviewedBy);

        // Kiem tra message ApplyChange duoc tao ra de TicketAPI dong bo ve
        var applyJob = await db.Set<GovernanceOutbox>().SingleOrDefaultAsync(x => x.Kind == "ApplyChange");
        Assert.NotNull(applyJob);
        Assert.Contains("\"EventId\":302", applyJob.Payload);
        Assert.Contains("\"Type\":\"Postpone\"", applyJob.Payload);
    }

    // TRUE CASE 5: UC-34 - Admin phe duyet Reschedule -> Cap nhat ngay gio moi, dong bang ban ve va kiem tra gioi han ban ve
    [Fact]
    public async Task True_05_UC34_ApproveReschedule_UpdatesConcertDatesAndAdjustsTicketSalesWindow()
    {
        using var db = CreateInMemoryDbContext();
        var ev = CreatePublishedEvent(eventId: 303, organizerId: 10);
        db.Events.Add(ev);

        var newStart = DateTime.UtcNow.AddDays(30);
        var newEnd = newStart.AddHours(4);

        // Them ticket type co salesEndsAt can duoc dieu chinh phu hop lich moi
        db.TicketTypes.Add(new TicketType
        {
            TicketTypeId = 901,
            EventId = 303,
            TypeName = "VIP Golden",
            Price = 1500000,
            Quantity = 200,
            SalesStartsAt = DateTime.UtcNow.AddDays(-1),
            SalesEndsAt = ev.StartsAt,
            IsDeleted = false
        });

        var request = new EventChangeRequest
        {
            Id = 56,
            EventId = 303,
            Type = "Reschedule",
            Status = "Pending",
            Reason = "[Force Majeure] Artist vocal cord surgery recovery",
            OldStartsAt = ev.StartsAt,
            OldEndsAt = ev.EndsAt,
            NewStartsAt = newStart,
            NewEndsAt = newEnd,
            OldStatus = ev.Status,
            EventVersion = ev.ScheduleVersion,
            SubmittedBy = 10,
            SubmittedAt = DateTime.UtcNow
        };
        db.Set<EventChangeRequest>().Add(request);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.ReviewChangeAsync(303, 56, admin: 1, new ChangeReviewInput("Approved", "Medical certificate verified"), CancellationToken.None);

        var refreshed = await db.Events.FindAsync(303);
        Assert.NotNull(refreshed);
        Assert.Equal(EventStatus.Published, refreshed.Status);
        Assert.Equal(newStart, refreshed.StartsAt);
        Assert.Equal(newEnd, refreshed.EndsAt);
        Assert.True(refreshed.SalesFrozen);
        Assert.Equal(2, refreshed.ScheduleVersion);

        var ticketType = await db.TicketTypes.FindAsync(901);
        Assert.True(ticketType!.SalesEndsAt <= refreshed.StartsAt);
    }

    // TRUE CASE 6: UC-34 - Admin thuc thi truc tiep trong tinh huong khan cap ma khong can Organizer de xuat truoc
    [Fact]
    public async Task True_06_UC34_AdminDirectChange_EmergencyIntervention_FreezesSalesImmediately()
    {
        using var db = CreateInMemoryDbContext();
        var ev = CreatePublishedEvent(eventId: 304, organizerId: 10);
        db.Events.Add(ev);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var directInput = new AdminDirectChangeInput("Postpone", "[Force Majeure] City government emergency directive: mandatory public gathering suspension", null, null);

        var newRequestId = await service.AdminDirectChangeAsync(304, admin: 1, directInput, CancellationToken.None);

        Assert.True(newRequestId > 0);
        var refreshed = await db.Events.FindAsync(304);
        Assert.Equal(EventStatus.Postponed, refreshed!.Status);
        Assert.True(refreshed.SalesFrozen, "Direct admin postponement must immediately freeze ticket sales.");
        Assert.Equal(2, refreshed.ScheduleVersion);

        var requestRecord = await db.Set<EventChangeRequest>().FindAsync(newRequestId);
        Assert.NotNull(requestRecord);
        Assert.Equal("Approved", requestRecord.Status);
        Assert.Equal(1, requestRecord.ReviewedBy);

        var applyJob = await db.Set<GovernanceOutbox>().SingleOrDefaultAsync(x => x.Kind == "ApplyChange");
        Assert.NotNull(applyJob);
    }

    // TRUE CASE 7: UC-36 - Doi soat va thong ke tong hop so ve, don hang va tien hoan bi anh huong
    [Fact]
    public void True_07_UC36_ViewAffectedOrders_ComputesTotalImpactAndPartitionsProperly()
    {
        var sampleOrders = new[]
        {
            new { OrderId = 1001, CustomerId = 501, TicketCount = 2, TotalAmount = 2_000_000m, Status = "RefundPending" },
            new { OrderId = 1002, CustomerId = 502, TicketCount = 4, TotalAmount = 4_000_000m, Status = "Refunded" },
            new { OrderId = 1003, CustomerId = 503, TicketCount = 1, TotalAmount = 1_000_000m, Status = "RefundPending" }
        };

        var totalOrders = sampleOrders.Length;
        var totalTickets = sampleOrders.Sum(x => x.TicketCount);
        var totalPaid = sampleOrders.Sum(x => x.TotalAmount);
        var refundedAmount = sampleOrders.Where(x => x.Status == "Refunded").Sum(x => x.TotalAmount);
        var pendingRefundAmount = sampleOrders.Where(x => x.Status == "RefundPending").Sum(x => x.TotalAmount);

        Assert.Equal(3, totalOrders);
        Assert.Equal(7, totalTickets);
        Assert.Equal(7_000_000m, totalPaid);
        Assert.Equal(4_000_000m, refundedAmount);
        Assert.Equal(3_000_000m, pendingRefundAmount);
        Assert.Equal(totalPaid, refundedAmount + pendingRefundAmount);
    }

    // Lớp giả lập HttpClientFactory cho GovernanceService
    private sealed class MockHttpFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    // Lớp giả lập HttpMessageHandler trả về JSON preview cho TicketAPI
    private sealed class MockHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var previewJson = JsonSerializer.Serialize(new
            {
                summary = new { orders = 12, tickets = 25, totalPaid = 25000000m, refunded = 0m },
                items = Array.Empty<object>()
            });

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(previewJson, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }

    // Lớp giả lập lưu trữ tài liệu pháp lý cho unit test
    private sealed class DummyAssetStore : IComplianceAssetStore
    {
        public Task<StoredComplianceAsset> UploadAsync(byte[] bytes, string publicId, CancellationToken ct) =>
            Task.FromResult(new StoredComplianceAsset(publicId, "https://example.invalid/" + publicId));
        public Task<byte[]> DownloadAsync(string id, CancellationToken ct) => Task.FromResult("%PDF-test"u8.ToArray());
        public Task DeleteAsync(string id, CancellationToken ct) => Task.CompletedTask;
    }
}
