using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PaymentAPI.Data;
using PaymentAPI.Models;
using PaymentAPI.Risk;
using Xunit;

namespace PaymentAPI.Tests;

/// <summary>FRD_P01: thanh toán thất bại >= 3 lần trong 10 phút (theo tài khoản).</summary>
public class PaymentRiskServiceTests : IDisposable
{
    private readonly PaymentDbContext _db;
    private readonly Mock<IRiskClient> _client = new();
    private RiskEvent? _sent;
    private int _nextId = 1;

    public PaymentRiskServiceTests()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new PaymentDbContext(options);

        _client.Setup(c => c.IngestAsync(It.IsAny<RiskEvent>(), It.IsAny<CancellationToken>()))
               .Callback<RiskEvent, CancellationToken>((e, _) => _sent = e)
               .ReturnsAsync((RiskResult?)null);
    }

    public void Dispose() => _db.Dispose();

    private PaymentRiskService Svc(params (string Key, string Value)[] config)
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(config.Select(c => new KeyValuePair<string, string?>(c.Key, c.Value)))
            .Build();
        return new PaymentRiskService(_db, _client.Object, cfg, NullLogger<PaymentRiskService>.Instance);
    }

    private async Task<PaymentTransaction> Seed(int? userId, string status, int minutesAgo, string? ip = "203.0.113.9")
    {
        var id = _nextId++;
        var tx = new PaymentTransaction
        {
            PaymentTransactionId = id,
            OrderId = 100 + id,
            Gateway = "VNPay",
            Amount = 500_000,
            Status = status,
            RequestedAt = DateTime.UtcNow.AddMinutes(-minutesAgo - 1),
            RespondedAt = DateTime.UtcNow.AddMinutes(-minutesAgo),
            UserId = userId,
            ClientIp = ip,
            DeviceFp = "fp_x",
            SessionId = "sess_x"
        };
        _db.PaymentTransactions.Add(tx);
        await _db.SaveChangesAsync();
        return tx;
    }

    [Fact]
    public async Task ThreeFailuresInTenMinutes_SendsFrdP01()
    {
        await Seed(7, "Failed", 8);
        await Seed(7, "Failed", 4);
        var last = await Seed(7, "Failed", 0);

        await Svc().OnPaymentFailedAsync(last);

        Assert.NotNull(_sent);
        Assert.Equal("FRD_P01", Assert.Single(_sent!.FraudRules).Code);
        Assert.Equal(7, _sent.UserId);
        Assert.Equal(last.OrderId, _sent.OrderId);
        Assert.Equal(500_000m, _sent.OrderAmount);
        Assert.Equal("203.0.113.9", _sent.IpAddress);
        Assert.Equal("fp_x", _sent.DeviceFp);
        Assert.Equal("sess_x", _sent.SessionId);
    }

    [Fact]
    public async Task TwoFailures_SendNothing()
    {
        await Seed(7, "Failed", 4);
        var last = await Seed(7, "Failed", 0);

        await Svc().OnPaymentFailedAsync(last);

        _client.Verify(c => c.IngestAsync(It.IsAny<RiskEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FailuresOlderThanTenMinutes_AreNotCounted()
    {
        await Seed(7, "Failed", 30);
        await Seed(7, "Failed", 20);
        var last = await Seed(7, "Failed", 0);

        await Svc().OnPaymentFailedAsync(last);

        Assert.Null(_sent);
    }

    [Fact]
    public async Task FailuresOfOtherUsers_AreNotCounted()
    {
        await Seed(8, "Failed", 5);
        await Seed(9, "Failed", 3);
        var last = await Seed(7, "Failed", 0);

        await Svc().OnPaymentFailedAsync(last);

        Assert.Null(_sent);
    }

    [Fact]
    public async Task SuccessfulPayments_AreNotCounted()
    {
        await Seed(7, "Success", 5);
        await Seed(7, "Success", 3);
        var last = await Seed(7, "Failed", 0);

        await Svc().OnPaymentFailedAsync(last);

        Assert.Null(_sent);
    }

    [Fact]
    public async Task TransactionWithoutUser_IsIgnored()
    {
        await Seed(null, "Failed", 5);
        await Seed(null, "Failed", 3);
        var last = await Seed(null, "Failed", 0);

        await Svc().OnPaymentFailedAsync(last);

        Assert.Null(_sent);
    }

    [Fact]
    public async Task InvalidIp_IsSentAsNull_SoAdminApiDoesNotReturn400()
    {
        await Seed(7, "Failed", 8, ip: "abc");
        await Seed(7, "Failed", 4, ip: "abc");
        var last = await Seed(7, "Failed", 0, ip: "abc");

        await Svc().OnPaymentFailedAsync(last);

        Assert.NotNull(_sent);
        Assert.Null(_sent!.IpAddress);
    }

    [Fact]
    public async Task WhenRiskIsDisabled_NothingIsSent()
    {
        await Seed(7, "Failed", 8);
        await Seed(7, "Failed", 4);
        var last = await Seed(7, "Failed", 0);

        await Svc(("Risk:Enabled", "false")).OnPaymentFailedAsync(last);

        Assert.Null(_sent);
    }

    [Fact]
    public async Task AdminApiFailure_NeverBreaksThePaymentFlow()
    {
        _client.Setup(c => c.IngestAsync(It.IsAny<RiskEvent>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new HttpRequestException("down"));
        await Seed(7, "Failed", 8);
        await Seed(7, "Failed", 4);
        var last = await Seed(7, "Failed", 0);

        var ex = await Record.ExceptionAsync(() => Svc().OnPaymentFailedAsync(last));

        Assert.Null(ex);
    }
}
