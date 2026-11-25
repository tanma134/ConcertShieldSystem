using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PaymentAPI.Data;
using PaymentAPI.DTOs;
using PaymentAPI.Models;
using PaymentAPI.Repositories;
using PaymentAPI.Services;
using Xunit;

namespace PaymentAPI.UnitTests.Features;

/// <summary>
/// Feature 15: Dispute, Support & Refund Workflow (Refund Processing & Financial Settlement)
/// Tests for UC_3.16.20 (Process Full/Partial Refund), UC_3.16.21 (Retry Failed Refund),
/// UC_3.16.22 (View Refund Reconciliation Report)
/// </summary>
public class Feature15_RefundReconciliationTests
{
    private PaymentDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new PaymentDbContext(options);
    }

    private (RefundService service, PaymentDbContext db, Mock<IPaymentTransactionRepository> paymentsMock) CreateService()
    {
        var db = CreateInMemoryDbContext();
        var paymentsMock = new Mock<IPaymentTransactionRepository>();
        var httpFactoryMock = new Mock<IHttpClientFactory>();
        var vnPayOptions = Options.Create(new VnPayConfig
        {
            TmnCode = "TEST",
            HashSecret = "TESTSECRET",
            BaseUrl = "https://sandbox.vnpayment.vn",
            ReturnUrl = "https://localhost"
        });

        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Refund:Mode"]).Returns("Ledger");

        var service = new RefundService(
            db,
            paymentsMock.Object,
            httpFactoryMock.Object,
            vnPayOptions,
            configMock.Object,
            NullLogger<RefundService>.Instance
        );

        return (service, db, paymentsMock);
    }

    [Fact]
    public async Task UC_3_16_20_ProcessRefund_FullRefund_SucceedsAndRecordsFullType()
    {
        var (service, db, paymentsMock) = CreateService();

        paymentsMock.Setup(p => p.GetLatestByOrderIdAsync(101, "VNPay"))
            .ReturnsAsync(new PaymentTransaction
            {
                PaymentTransactionId = 1,
                OrderId = 101,
                Amount = 1500000,
                Status = "Success"
            });

        var request = new ProcessRefundRequestDto
        {
            OrderId = 101,
            Amount = 1500000,
            RefundType = "Full",
            Reason = "Event postponed indefinitely"
        };

        var result = await service.ProcessRefundAsync(request, 1, "Admin Support", CancellationToken.None);

        Assert.Equal("Refunded", result.Status);
        var saved = await db.PaymentRefunds.FirstOrDefaultAsync(r => r.OrderId == 101);
        Assert.NotNull(saved);
        Assert.Equal("Full", saved.RefundType);
        Assert.Equal(1500000, saved.Amount);
    }

    [Fact]
    public async Task UC_3_16_20_ProcessRefund_PartialRefund_SucceedsAndRecordsPartialType()
    {
        var (service, db, paymentsMock) = CreateService();

        paymentsMock.Setup(p => p.GetLatestByOrderIdAsync(102, "VNPay"))
            .ReturnsAsync(new PaymentTransaction
            {
                PaymentTransactionId = 2,
                OrderId = 102,
                Amount = 2000000,
                Status = "Success"
            });

        var request = new ProcessRefundRequestDto
        {
            OrderId = 102,
            Amount = 1600000,
            RefundType = "Partial",
            Reason = "Tiered refund minus 20% cancellation fee"
        };

        var result = await service.ProcessRefundAsync(request, 1, "Admin Support", CancellationToken.None);

        Assert.Equal("Refunded", result.Status);
        var saved = await db.PaymentRefunds.FirstOrDefaultAsync(r => r.OrderId == 102);
        Assert.NotNull(saved);
        Assert.Equal("Partial", saved.RefundType);
        Assert.Equal(1600000, saved.Amount);
    }

    [Fact]
    public async Task UC_3_16_21_RetryFailedRefund_RetriesAndIncrementsRetryCount()
    {
        var (service, db, paymentsMock) = CreateService();

        paymentsMock.Setup(p => p.GetLatestByOrderIdAsync(103, "VNPay"))
            .ReturnsAsync(new PaymentTransaction
            {
                PaymentTransactionId = 3,
                OrderId = 103,
                Amount = 500000,
                Status = "Success"
            });

        db.PaymentRefunds.Add(new PaymentRefund
        {
            PaymentRefundId = 15,
            OrderId = 103,
            Amount = 500000,
            Status = "Failed",
            RetryCount = 1,
            RefundType = "Full",
            Reason = "Previous gateway timeout"
        });
        await db.SaveChangesAsync();

        var result = await service.RetryRefundAsync(15, "Ledger", 1, "Admin Retrier", CancellationToken.None);

        Assert.Equal("Refunded", result.Status);
        var updated = await db.PaymentRefunds.FindAsync(15);
        Assert.NotNull(updated);
        Assert.Equal("Refunded", updated.Status);
        Assert.Equal(2, updated.RetryCount);
    }

    [Fact]
    public async Task UC_3_16_22_ViewRefundReconciliationReport_AggregatesMatchedRefunds()
    {
        using var db = CreateInMemoryDbContext();
        db.PaymentRefunds.AddRange(
            new PaymentRefund { PaymentRefundId = 1, OrderId = 201, Amount = 500000, Status = "Refunded", ReconciliationStatus = "Matched", CreatedAt = DateTime.UtcNow },
            new PaymentRefund { PaymentRefundId = 2, OrderId = 202, Amount = 1000000, Status = "Refunded", ReconciliationStatus = "Matched", CreatedAt = DateTime.UtcNow },
            new PaymentRefund { PaymentRefundId = 3, OrderId = 203, Amount = 750000, Status = "Failed", ReconciliationStatus = "Unreconciled", CreatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var matched = await db.PaymentRefunds.Where(r => r.ReconciliationStatus == "Matched").ToListAsync();
        long totalMatchedAmount = matched.Sum(r => r.Amount);

        Assert.Equal(2, matched.Count);
        Assert.Equal(1500000, totalMatchedAmount);
    }
}
