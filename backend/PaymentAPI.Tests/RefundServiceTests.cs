using System;
using System.Collections.Generic;
using System.Linq;
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

namespace PaymentAPI.Tests
{
    public class RefundServiceTests
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
            // Use Ledger mode for fast deterministic testing without real gateway calls
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
        public async Task RefundAsync_ForTicketReturn_CreatesAndSucceedsWithLedgerRef()
        {
            var (service, db, paymentsMock) = CreateService();

            paymentsMock.Setup(p => p.GetLatestByOrderIdAsync(101, "VNPay"))
                .ReturnsAsync(new PaymentTransaction
                {
                    PaymentTransactionId = 1,
                    OrderId = 101,
                    Amount = 500000,
                    Status = "Success",
                    Gateway = "VNPay"
                });

            var request = new CreateRefundRequestDto
            {
                ReturnRequestId = 55,
                OrderId = 101,
                Amount = 500000
            };

            var result = await service.RefundAsync(request, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("Refunded", result.Status);
            Assert.NotNull(result.Reference);
            Assert.StartsWith("LEDGER-RFD-", result.Reference);

            var saved = await db.PaymentRefunds.FirstOrDefaultAsync(r => r.ReturnRequestId == 55);
            Assert.NotNull(saved);
            Assert.Equal("Refunded", saved.Status);
            Assert.Equal("Matched", saved.ReconciliationStatus);
        }

        [Fact]
        public async Task ProcessRefundAsync_WhenAmountExceedsLimit_ThrowsArgumentException()
        {
            var (service, db, paymentsMock) = CreateService();

            paymentsMock.Setup(p => p.GetLatestByOrderIdAsync(202, "VNPay"))
                .ReturnsAsync(new PaymentTransaction
                {
                    PaymentTransactionId = 2,
                    OrderId = 202,
                    Amount = 1000000,
                    Status = "Success"
                });

            // Already refunded 800,000
            db.PaymentRefunds.Add(new PaymentRefund
            {
                PaymentRefundId = 1,
                OrderId = 202,
                Amount = 800000,
                Status = "Refunded"
            });
            await db.SaveChangesAsync();

            var request = new ProcessRefundRequestDto
            {
                OrderId = 202,
                Amount = 300000, // Max refundable is 200,000
                RefundType = "Partial",
                Reason = "Over requested"
            };

            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                service.ProcessRefundAsync(request, 10, "Staff One", CancellationToken.None));

            Assert.Contains("exceeds the maximum refundable amount", ex.Message);
        }

        [Fact]
        public async Task ProcessRefundAsync_ValidRequest_SucceedsAndRecordsActor()
        {
            var (service, db, paymentsMock) = CreateService();

            paymentsMock.Setup(p => p.GetLatestByOrderIdAsync(303, "VNPay"))
                .ReturnsAsync(new PaymentTransaction
                {
                    PaymentTransactionId = 3,
                    OrderId = 303,
                    Amount = 250000,
                    Status = "Success"
                });

            var request = new ProcessRefundRequestDto
            {
                OrderId = 303,
                Amount = 250000,
                RefundType = "Full",
                Reason = "Concert Rescheduled"
            };

            var result = await service.ProcessRefundAsync(request, 42, "Admin Support", CancellationToken.None);

            Assert.Equal("Refunded", result.Status);
            var saved = await db.PaymentRefunds.FirstOrDefaultAsync(r => r.OrderId == 303);
            Assert.NotNull(saved);
            Assert.Equal(42, saved.ProcessedByUserId);
            Assert.Equal("Admin Support", saved.ProcessedByUserName);
            Assert.Equal("Full", saved.RefundType);
            Assert.Equal("Concert Rescheduled", saved.Reason);
        }

        [Fact]
        public async Task RetryRefundAsync_FailedRefund_RetriesAndIncrementsRetryCount()
        {
            var (service, db, paymentsMock) = CreateService();

            paymentsMock.Setup(p => p.GetLatestByOrderIdAsync(404, "VNPay"))
                .ReturnsAsync(new PaymentTransaction
                {
                    PaymentTransactionId = 4,
                    OrderId = 404,
                    Amount = 150000,
                    Status = "Success"
                });

            var failedRefund = new PaymentRefund
            {
                PaymentRefundId = 88,
                OrderId = 404,
                Amount = 150000,
                Status = "Failed",
                ErrorMessage = "Gateway timeout",
                RetryCount = 1
            };
            db.PaymentRefunds.Add(failedRefund);
            await db.SaveChangesAsync();

            var result = await service.RetryRefundAsync(88, "Immediate", 9, "Staff Two", CancellationToken.None);

            Assert.Equal("Refunded", result.Status);
            var updated = await db.PaymentRefunds.FindAsync(88);
            Assert.NotNull(updated);
            Assert.Equal(2, updated.RetryCount);
            Assert.NotNull(updated.LastRetryAt);
            Assert.Equal("Refunded", updated.Status);
        }

        [Fact]
        public async Task GetReconciliationReportAsync_CalculatesTotalsAndFiltersAccurately()
        {
            var (service, db, _) = CreateService();

            db.PaymentRefunds.AddRange(
                new PaymentRefund
                {
                    PaymentRefundId = 1,
                    OrderId = 501,
                    Amount = 100000,
                    Gateway = "VNPay",
                    Status = "Refunded",
                    ReconciliationStatus = "Matched",
                    CustomerName = "Alice",
                    CreatedAt = DateTime.UtcNow
                },
                new PaymentRefund
                {
                    PaymentRefundId = 2,
                    OrderId = 502,
                    Amount = 200000,
                    Gateway = "VNPay",
                    Status = "Refunded",
                    ReconciliationStatus = "Matched",
                    CustomerName = "Bob",
                    CreatedAt = DateTime.UtcNow
                },
                new PaymentRefund
                {
                    PaymentRefundId = 3,
                    OrderId = 503,
                    Amount = 150000,
                    Gateway = "VNPay",
                    Status = "Failed",
                    ReconciliationStatus = "Mismatched",
                    CustomerName = "Charlie",
                    CreatedAt = DateTime.UtcNow
                }
            );
            await db.SaveChangesAsync();

            var filter = new ReconciliationFilterDto();
            var report = await service.GetReconciliationReportAsync(filter, CancellationToken.None);

            Assert.NotNull(report);
            Assert.Equal(300000, report.TotalAmount); // 100,000 + 200,000 (Refunded only)
            Assert.Equal(3, report.TotalRefunds);
            Assert.Equal(1, report.MismatchedCount);
            Assert.Equal(3, report.Refunds.Count);
        }

        [Fact]
        public async Task GetOrderRefundSummaryAsync_CalculatesBalancesCorrectly()
        {
            var (service, db, paymentsMock) = CreateService();

            paymentsMock.Setup(p => p.GetLatestByOrderIdAsync(601, "VNPay"))
                .ReturnsAsync(new PaymentTransaction
                {
                    PaymentTransactionId = 6,
                    OrderId = 601,
                    Amount = 1000000,
                    Status = "Success"
                });

            db.PaymentRefunds.Add(new PaymentRefund
            {
                PaymentRefundId = 10,
                OrderId = 601,
                Amount = 300000,
                Status = "Refunded"
            });
            await db.SaveChangesAsync();

            var summary = await service.GetOrderRefundSummaryAsync(601, CancellationToken.None);

            Assert.NotNull(summary);
            Assert.Equal(1000000, summary.OriginalAmount);
            Assert.Equal(300000, summary.AlreadyRefunded);
            Assert.Equal(700000, summary.MaxRefundableAmount);
            Assert.Single(summary.PreviousRefunds);
        }
    }
}
