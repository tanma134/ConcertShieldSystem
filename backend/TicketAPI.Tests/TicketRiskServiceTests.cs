using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TicketAPI.DTOs;
using TicketAPI.Models;
using TicketAPI.Risk;
using Xunit;

namespace TicketAPI.Tests;

/// <summary>
/// Kiểm tra các rule tính từ DB của TicketAPI (T01, T02, T03, A01, V02, B01, TRU_01).
/// Dùng EF Core InMemory và giả lập IRiskGuard để bắt RiskEvent mà service định gửi sang AdminAPI.
/// </summary>
public class TicketRiskServiceTests : IDisposable
{
    private const int UserId = 1;
    private const int EventId = 10;

    private readonly TicketDbContext _db;
    private readonly Mock<IRiskGuard> _guard = new();
    private readonly Mock<IHoldService> _holds = new();
    private readonly TicketRiskService _svc;
    private RiskEvent? _sent;
    private int _nextReturnId = 1;

    public TicketRiskServiceTests()
    {
        var options = new DbContextOptionsBuilder<TicketDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new TicketDbContext(options);

        _guard.Setup(g => g.PreCheckAsync(It.IsAny<HttpContext>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(RiskVerdict.Allow());
        _guard.Setup(g => g.EvaluateAsync(It.IsAny<HttpContext>(), It.IsAny<RiskEvent>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
              .Callback<HttpContext, RiskEvent, bool, CancellationToken>((_, e, _, _) => _sent = e)
              .ReturnsAsync(RiskVerdict.Allow());

        _svc = new TicketRiskService(_db, _guard.Object, _holds.Object, NullLogger<TicketRiskService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    // ------------------------------------------------------------ dữ liệu mẫu

    private async Task SeedOrder(int id, int customer, int eventId, string status, int qty,
        string? email = null, string? phone = null, DateTime? date = null, DateTime? expiresAt = null)
    {
        _db.Orders.Add(new Order
        {
            OrderId = id, CustomerId = customer, EventId = eventId, Status = status,
            Email = email, Phone = phone, OrderDate = date ?? DateTime.UtcNow,
            ExpiresAt = expiresAt, IsDeleted = false
        });
        _db.OrderDetails.Add(new OrderDetail { OrderDetailId = id, OrderId = id, TicketTypeId = 1, Quantity = qty });
        await _db.SaveChangesAsync();
    }

    private async Task SeedReturns(int count, DateTime createdAt, string status = "Approved")
    {
        for (var i = 1; i <= count; i++)
            _db.TicketReturnRequests.Add(new TicketReturnRequest
            {
                TicketReturnRequestId = _nextReturnId++, TicketId = i, OrderId = 1, EventId = EventId,
                RequesterUserId = UserId, Reason = "x", Status = status, CreatedAt = createdAt, UpdatedAt = createdAt
            });
        await _db.SaveChangesAsync();
    }

    private static CreateHoldSessionRequest HoldFor(int quantity) => new()
    {
        EventId = EventId,
        Tickets = { new TicketHoldItem { TicketTypeId = 1, Quantity = quantity } }
    };

    private static CreateOrderDTO OrderFor(int quantity, string holdId = "h1", string email = "a@b.com", string phone = "0900000001") => new()
    {
        EventId = EventId, HoldId = holdId, Email = email, Phone = phone, FullName = "A",
        OrderDetails = { new CreateOrderDetailsDTO { TicketTypeId = 1, Quantity = quantity } }
    };

    private static DefaultHttpContext HttpWithAccountCreated(DateTime createdUtc)
    {
        var ctx = TestHttp.Browser();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("account_created_at", createdUtc.ToString("O")) }, "test"));
        return ctx;
    }

    private static string[] Fraud(RiskEvent? e) => e!.FraudRules.Select(r => r.Code).ToArray();
    private static string[] Bot(RiskEvent? e) => e!.BotRules.Select(r => r.Code).ToArray();

    // ------------------------------------------------------------ FRD_T01

    [Fact]
    public async Task T01_NotRaised_WhenTotalIsExactlyTheLimit()
    {
        await SeedOrder(1, UserId, EventId, "Paid", 5);

        await _svc.CheckHoldAsync(TestHttp.Browser(), UserId, HoldFor(1));     // 5 + 1 = 6, chưa vượt

        Assert.DoesNotContain("FRD_T01", Fraud(_sent));
    }

    [Fact]
    public async Task T01_Raised_WhenPurchasedPlusRequestedExceedsSix()
    {
        await SeedOrder(1, UserId, EventId, "Paid", 5);

        await _svc.CheckHoldAsync(TestHttp.Browser(), UserId, HoldFor(2));     // 5 + 2 = 7

        Assert.Contains("FRD_T01", Fraud(_sent));
    }

    [Fact]
    public async Task T01_ExpiredPendingOrder_IsNotCounted()
    {
        await SeedOrder(1, UserId, EventId, "Pending", 6, expiresAt: DateTime.UtcNow.AddMinutes(-5));

        await _svc.CheckHoldAsync(TestHttp.Browser(), UserId, HoldFor(1));

        Assert.DoesNotContain("FRD_T01", Fraud(_sent));
    }

    [Fact]
    public async Task T01_ActivePendingOrder_IsCounted()
    {
        await SeedOrder(1, UserId, EventId, "Pending", 6, expiresAt: DateTime.UtcNow.AddMinutes(5));

        await _svc.CheckHoldAsync(TestHttp.Browser(), UserId, HoldFor(1));

        Assert.Contains("FRD_T01", Fraud(_sent));
    }

    [Fact]
    public async Task T01_OtherEventsAndOtherUsers_AreNotCounted()
    {
        await SeedOrder(1, UserId, 99, "Paid", 6);          // sự kiện khác
        await SeedOrder(2, 2, EventId, "Paid", 6);          // người khác

        await _svc.CheckHoldAsync(TestHttp.Browser(), UserId, HoldFor(1));

        Assert.DoesNotContain("FRD_T01", Fraud(_sent));
    }

    [Fact]
    public async Task T01_UsesSeatCount_WhenMoreThanTicketQuantities()
    {
        var req = new CreateHoldSessionRequest { EventId = EventId, SeatIds = { 1, 2, 3, 4, 5, 6, 7 } };

        await _svc.CheckHoldAsync(TestHttp.Browser(), UserId, req);

        Assert.Contains("FRD_T01", Fraud(_sent));
    }

    // ------------------------------------------------------------ FRD_T02

    [Fact]
    public async Task T02_Raised_WithThreeReturnsInSevenDays()
    {
        await SeedReturns(3, DateTime.UtcNow.AddDays(-2));

        await _svc.CheckHoldAsync(TestHttp.Browser(), UserId, HoldFor(1));

        Assert.Contains("FRD_T02", Fraud(_sent));
    }

    [Fact]
    public async Task T02_NotRaised_WithTwoReturns()
    {
        await SeedReturns(2, DateTime.UtcNow.AddDays(-2));

        await _svc.CheckHoldAsync(TestHttp.Browser(), UserId, HoldFor(1));

        Assert.DoesNotContain("FRD_T02", Fraud(_sent));
    }

    [Fact]
    public async Task T02_IgnoresOldAndRejectedReturns()
    {
        await SeedReturns(3, DateTime.UtcNow.AddDays(-10));                   // quá 7 ngày
        await SeedReturns(3, DateTime.UtcNow.AddDays(-1), status: "Rejected");  // bị từ chối

        await _svc.CheckHoldAsync(TestHttp.Browser(), UserId, HoldFor(1));

        Assert.DoesNotContain("FRD_T02", Fraud(_sent));
    }

    [Fact]
    public async Task T02_OnReturnSubmit_CountsTheRequestBeingSent()
    {
        await SeedReturns(2, DateTime.UtcNow.AddDays(-1));    // 2 lần trước + lần đang gửi = 3

        await _svc.CheckReturnAsync(TestHttp.Browser(), UserId, ticketId: 5);

        Assert.Contains("FRD_T02", Fraud(_sent));
        Assert.Equal(5, _sent!.TicketId);
    }

    // ------------------------------------------------------------ FRD_T03

    [Fact]
    public async Task T03_Raised_WhenThreeAccountsShareTheSameEmailWithin24h()
    {
        await SeedOrder(1, 2, 99, "Paid", 1, email: "Shared@Mail.com");
        await SeedOrder(2, 3, 99, "Paid", 1, email: "shared@mail.com");

        await _svc.CheckOrderAsync(TestHttp.Browser(), UserId, OrderFor(1, email: "SHARED@mail.com"));

        Assert.Contains("FRD_T03", Fraud(_sent));
    }

    [Fact]
    public async Task T03_Raised_WhenThreeAccountsShareThePhone()
    {
        await SeedOrder(1, 2, 99, "Paid", 1, phone: "0911111111");
        await SeedOrder(2, 3, 99, "Paid", 1, phone: "0911111111");

        await _svc.CheckOrderAsync(TestHttp.Browser(), UserId, OrderFor(1, phone: "0911111111"));

        Assert.Contains("FRD_T03", Fraud(_sent));
    }

    [Fact]
    public async Task T03_NotRaised_WithOnlyTwoAccounts()
    {
        await SeedOrder(1, 2, 99, "Paid", 1, email: "shared@mail.com");

        await _svc.CheckOrderAsync(TestHttp.Browser(), UserId, OrderFor(1, email: "shared@mail.com"));

        Assert.DoesNotContain("FRD_T03", Fraud(_sent));
    }

    [Fact]
    public async Task T03_IgnoresOrdersOlderThan24h()
    {
        await SeedOrder(1, 2, 99, "Paid", 1, email: "shared@mail.com", date: DateTime.UtcNow.AddHours(-30));
        await SeedOrder(2, 3, 99, "Paid", 1, email: "shared@mail.com", date: DateTime.UtcNow.AddHours(-30));

        await _svc.CheckOrderAsync(TestHttp.Browser(), UserId, OrderFor(1, email: "shared@mail.com"));

        Assert.DoesNotContain("FRD_T03", Fraud(_sent));
    }

    [Fact]
    public async Task T03_SameAccountOrderingTwice_IsNotThreeAccounts()
    {
        await SeedOrder(1, UserId, 99, "Paid", 1, email: "me@mail.com");
        await SeedOrder(2, UserId, 99, "Paid", 1, email: "me@mail.com");

        await _svc.CheckOrderAsync(TestHttp.Browser(), UserId, OrderFor(1, email: "me@mail.com"));

        Assert.DoesNotContain("FRD_T03", Fraud(_sent));
    }

    // ------------------------------------------------------------ BOT_B01, BOT_V02

    [Fact]
    public async Task B01_Raised_WhenOrderHasNoHoldSession()
    {
        await _svc.CheckOrderAsync(TestHttp.Browser(), UserId, OrderFor(1, holdId: ""));

        Assert.Contains("BOT_B01", Bot(_sent));
    }

    [Fact]
    public async Task V02_Raised_WhenOrderSubmittedWithin3SecondsOfHold()
    {
        _holds.Setup(h => h.GetHoldDetailsAsync("h1", UserId))
              .ReturnsAsync(new HoldSessionResponse { ExpiresAtUtc = DateTime.UtcNow.AddSeconds(599) });   // giữ chỗ cách đây ~1s

        await _svc.CheckOrderAsync(TestHttp.Browser(), UserId, OrderFor(1));

        Assert.Contains("BOT_V02", Bot(_sent));
        Assert.DoesNotContain("BOT_B01", Bot(_sent));
    }

    [Fact]
    public async Task V02_NotRaised_WhenUserTookTheirTime()
    {
        _holds.Setup(h => h.GetHoldDetailsAsync("h1", UserId))
              .ReturnsAsync(new HoldSessionResponse { ExpiresAtUtc = DateTime.UtcNow.AddSeconds(300) });   // giữ chỗ cách đây ~5 phút

        await _svc.CheckOrderAsync(TestHttp.Browser(), UserId, OrderFor(1));

        Assert.DoesNotContain("BOT_V02", Bot(_sent));
    }

    [Fact]
    public async Task V02_HoldLookupFailure_DoesNotBreakTheOrder()
    {
        _holds.Setup(h => h.GetHoldDetailsAsync("h1", UserId)).ThrowsAsync(new InvalidOperationException("hold not found"));

        var v = await _svc.CheckOrderAsync(TestHttp.Browser(), UserId, OrderFor(1));

        Assert.True(v.Allowed);
        Assert.DoesNotContain("BOT_V02", Bot(_sent));
    }

    // ------------------------------------------------------------ claim account_created_at: A01, TRU_01

    [Fact]
    public async Task A01_Raised_ForBrandNewAccountBuyingFourTickets()
    {
        await _svc.CheckHoldAsync(HttpWithAccountCreated(DateTime.UtcNow.AddHours(-2)), UserId, HoldFor(4));

        Assert.Contains("FRD_A01", Fraud(_sent));
    }

    [Fact]
    public async Task A01_NotRaised_ForOldAccountOrFewTickets()
    {
        await _svc.CheckHoldAsync(HttpWithAccountCreated(DateTime.UtcNow.AddDays(-30)), UserId, HoldFor(4));
        Assert.DoesNotContain("FRD_A01", Fraud(_sent));

        await _svc.CheckHoldAsync(HttpWithAccountCreated(DateTime.UtcNow.AddHours(-2)), UserId, HoldFor(3));
        Assert.DoesNotContain("FRD_A01", Fraud(_sent));
    }

    [Fact]
    public async Task Tru01_Granted_ForSixMonthOldAccountWithThreePaidOrders()
    {
        await SeedOrder(1, UserId, 99, "Paid", 1);
        await SeedOrder(2, UserId, 99, "Paid", 1);
        await SeedOrder(3, UserId, 99, "Paid", 1);

        await _svc.CheckHoldAsync(HttpWithAccountCreated(DateTime.UtcNow.AddDays(-400)), UserId, HoldFor(1));

        Assert.Contains(_sent!.TrustRules, r => r.Code == "TRU_01");
    }

    [Fact]
    public async Task Tru01_NotGranted_WithTooFewPaidOrders()
    {
        await SeedOrder(1, UserId, 99, "Paid", 1);

        await _svc.CheckHoldAsync(HttpWithAccountCreated(DateTime.UtcNow.AddDays(-400)), UserId, HoldFor(1));

        Assert.Empty(_sent!.TrustRules);
    }

    [Fact]
    public async Task WithoutTheClaim_A01AndTru01AreSkipped_AndReportedAsMissingSignal()
    {
        await _svc.CheckHoldAsync(TestHttp.Browser(), UserId, HoldFor(4));

        Assert.DoesNotContain("FRD_A01", Fraud(_sent));
        Assert.Empty(_sent!.TrustRules);
        Assert.Contains("account_created_at", _sent.SignalsMissing);
    }

    // ------------------------------------------------------------ block và bền vững

    [Fact]
    public async Task ActiveBlock_StopsBeforeAnyRuleIsEvaluated()
    {
        _guard.Setup(g => g.PreCheckAsync(It.IsAny<HttpContext>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new RiskVerdict(false, 403, "RISK_BLOCKED", "blocked", null));

        var v = await _svc.CheckOrderAsync(TestHttp.Browser(), UserId, OrderFor(1));

        Assert.False(v.Allowed);
        Assert.Equal("RISK_BLOCKED", v.Code);
        _guard.Verify(g => g.EvaluateAsync(It.IsAny<HttpContext>(), It.IsAny<RiskEvent>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EventAndUserAreAlwaysFilledIn()
    {
        await _svc.CheckHoldAsync(TestHttp.Browser(), UserId, HoldFor(1));

        Assert.Equal(UserId, _sent!.UserId);
        Assert.Equal(EventId, _sent.EventId);
    }
}
