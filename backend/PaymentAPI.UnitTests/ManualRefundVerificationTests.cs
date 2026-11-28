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

namespace PaymentAPI.Tests;

public class ManualRefundVerificationTests
{
    private static (RefundService Service, PaymentDbContext Db) Create(bool allowSimulation = true)
    {
        var db = new PaymentDbContext(new DbContextOptionsBuilder<PaymentDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Refund:AllowSimulation"] = allowSimulation.ToString() }).Build();
        return (new RefundService(db, Mock.Of<IPaymentTransactionRepository>(), Mock.Of<IHttpClientFactory>(), Options.Create(new VnPayConfig()), config, NullLogger<RefundService>.Instance), db);
    }
    private static RecordManualRefundDto Input() => new() { ReturnRequestId = 18, OrderId = 54, Amount = 2250000, Reference = "SIM-RETURN-18", IsSimulation = true, ProcessedByUserId = 1 };

    [Fact] public async Task SimulationDoesNotClaimMoneyTransferredOrEnterActualTotals()
    {
        var rig = Create(); using var db = rig.Db;
        await rig.Service.RecordManualRefundAsync(Input());
        var row = Assert.Single(await db.PaymentRefunds.ToListAsync());
        Assert.Equal("SimulatedBankTransfer", row.Gateway); Assert.Equal(0, row.GatewayAmount);
        var report = await rig.Service.GetReconciliationReportAsync(new ReconciliationFilterDto());
        Assert.Equal(0, report.TotalAmount); Assert.Equal(0, report.TotalRefunds); Assert.Equal(1, report.SimulatedRefunds);
        Assert.Equal("Simulated", Assert.Single(report.Refunds).Status);
    }
    [Fact] public async Task DisabledSimulationCannotWriteARefund()
    {
        var rig = Create(false); using var db = rig.Db;
        await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Service.RecordManualRefundAsync(Input()));
        Assert.Empty(await db.PaymentRefunds.ToListAsync());
    }
    [Fact] public async Task RetryIsIdempotentButCannotChangeAmount()
    {
        var rig = Create(); using var db = rig.Db; var input = Input();
        await rig.Service.RecordManualRefundAsync(input); await rig.Service.RecordManualRefundAsync(input);
        Assert.Single(await db.PaymentRefunds.ToListAsync()); input.Amount++;
        await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Service.RecordManualRefundAsync(input));
    }
    [Fact] public async Task ARealTransferCannotUseSimulationReference()
    {
        var rig = Create(); using var db = rig.Db; var input = Input(); input.IsSimulation = false;
        await Assert.ThrowsAsync<ArgumentException>(() => rig.Service.RecordManualRefundAsync(input));
        Assert.Empty(await db.PaymentRefunds.ToListAsync());
    }
}
