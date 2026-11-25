using Moq;
using PaymentAPI.API;
using PaymentAPI.DTOs;
using PaymentAPI.Models;
using PaymentAPI.Repositories;
using PaymentAPI.Services;
using Xunit;

namespace PaymentAPI.Tests;

public class ApplyVoucherServiceTests
{
    private const int UserId = 7;
    private const int EventId = 42;
    private const int OrganizerId = 9;

    [Theory]
    [InlineData("   ", 42, 400_000)]
    [InlineData("SAVE50", 0, 400_000)]
    [InlineData("SAVE50", 42, 0)]
    [InlineData("SAVE50", 42, -100)]
    public async Task ApplyVoucherAsync_RejectsInvalidRequest(
        string code,
        int eventId,
        long orderAmount)
    {
        var (service, repository, _) = CreateService();

        var result = await service.ApplyVoucherAsync(
            new ApplyVoucherRequestDTO
            {
                Code = code,
                EventId = eventId,
                OrderAmount = orderAmount
            },
            UserId);

        Assert.False(result.IsValid);
        repository.Verify(r => r.GetByCodeAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ApplyVoucherAsync_RejectsVoucherThatDoesNotExist()
    {
        var (service, repository, _) = CreateService();
        repository
            .Setup(r => r.GetByCodeAsync("MISSING"))
            .ReturnsAsync((Voucher?)null);

        var result = await service.ApplyVoucherAsync(
            CreateRequest("MISSING"),
            UserId);

        Assert.False(result.IsValid);
        Assert.Equal(0, result.DiscountAmount);
        Assert.Equal(400_000, result.FinalAmount);
    }

    [Fact]
    public async Task ApplyVoucherAsync_RejectsInactiveVoucher()
    {
        var voucher = CreateVoucher();
        voucher.IsActive = false;

        var (service, _, _) = CreateService(voucher);

        var result = await service.ApplyVoucherAsync(
            CreateRequest(),
            UserId);

        Assert.False(result.IsValid);
        Assert.Equal(0, result.DiscountAmount);
    }

    [Fact]
    public async Task ApplyVoucherAsync_RejectsVoucherThatHasNotStarted()
    {
        var voucher = CreateVoucher();
        voucher.StartsAt = DateTime.UtcNow.AddDays(1);
        voucher.EndsAt = DateTime.UtcNow.AddDays(2);

        var (service, _, _) = CreateService(voucher);

        var result = await service.ApplyVoucherAsync(
            CreateRequest(),
            UserId);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ApplyVoucherAsync_RejectsExpiredVoucher()
    {
        var voucher = CreateVoucher();
        voucher.StartsAt = DateTime.UtcNow.AddDays(-2);
        voucher.EndsAt = DateTime.UtcNow.AddDays(-1);

        var (service, _, _) = CreateService(voucher);

        var result = await service.ApplyVoucherAsync(
            CreateRequest(),
            UserId);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ApplyVoucherAsync_RejectsVoucherWithNoRemainingQuantity()
    {
        var voucher = CreateVoucher();
        voucher.TotalQuantity = 10;
        voucher.UsedQuantity = 10;

        var (service, _, _) = CreateService(voucher);

        var result = await service.ApplyVoucherAsync(
            CreateRequest(),
            UserId);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ApplyVoucherAsync_RejectsOrderBelowMinimumAmount()
    {
        var voucher = CreateVoucher();
        voucher.MinOrderAmount = 500_000;

        var (service, _, _) = CreateService(voucher);

        var result = await service.ApplyVoucherAsync(
            CreateRequest(orderAmount: 400_000),
            UserId);

        Assert.False(result.IsValid);
        Assert.Equal(0, result.DiscountAmount);
        Assert.Equal(400_000, result.FinalAmount);
    }

    [Fact]
    public async Task ApplyVoucherAsync_RejectsEventVoucherForDifferentEvent()
    {
        var voucher = CreateVoucher();
        voucher.Scope = "EVENT";
        voucher.EventId = 99;

        var (service, _, eventClient) = CreateService(voucher);

        var result = await service.ApplyVoucherAsync(
            CreateRequest(eventId: EventId),
            UserId);

        Assert.False(result.IsValid);
        eventClient.Verify(
            c => c.GetOrganizerIdByEventIdAsync(It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task ApplyVoucherAsync_RejectsOrganizerVoucherForDifferentOrganizer()
    {
        var voucher = CreateVoucher();
        voucher.Scope = "ORGANIZER";
        voucher.OrganizerId = 100;

        var (service, _, _) = CreateService(
            voucher,
            eventOrganizerId: OrganizerId);

        var result = await service.ApplyVoucherAsync(
            CreateRequest(),
            UserId);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ApplyVoucherAsync_RejectsVoucherWhenUserReachedUsageLimit()
    {
        var voucher = CreateVoucher();
        voucher.MaxUsagePerUser = 2;

        var (service, repository, _) = CreateService(
            voucher,
            userUsageCount: 2);

        var result = await service.ApplyVoucherAsync(
            CreateRequest(),
            UserId);

        Assert.False(result.IsValid);
        repository.Verify(
            r => r.CountUserUsagesAsync(voucher.VoucherId, UserId),
            Times.Once);
    }

    [Fact]
    public async Task ApplyVoucherAsync_RejectsPercentVoucherWithoutPercentConfiguration()
    {
        var voucher = CreateVoucher();
        voucher.DiscountType = "PERCENT";
        voucher.DiscountPercent = null;

        var (service, _, _) = CreateService(voucher);

        var result = await service.ApplyVoucherAsync(
            CreateRequest(),
            UserId);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ApplyVoucherAsync_AppliesPercentDiscountAndMaximumCap()
    {
        var voucher = CreateVoucher();
        voucher.DiscountType = "PERCENT";
        voucher.DiscountPercent = 50;
        voucher.MaxDiscountAmount = 100_000;

        var (service, _, _) = CreateService(voucher);

        var result = await service.ApplyVoucherAsync(
            CreateRequest(orderAmount: 400_000),
            UserId);

        Assert.True(result.IsValid);
        Assert.Equal(100_000, result.DiscountAmount);
        Assert.Equal(300_000, result.FinalAmount);
        Assert.Equal("SAVE50", result.Code);
        Assert.Equal(voucher.VoucherId, result.VoucherId);
    }

    [Fact]
    public async Task ApplyVoucherAsync_RoundsPercentDiscountAwayFromZero()
    {
        var voucher = CreateVoucher();
        voucher.DiscountType = "PERCENT";
        voucher.DiscountPercent = 25;

        var (service, _, _) = CreateService(voucher);

        var result = await service.ApplyVoucherAsync(
            CreateRequest(orderAmount: 10),
            UserId);

        Assert.True(result.IsValid);
        Assert.Equal(3, result.DiscountAmount);
        Assert.Equal(7, result.FinalAmount);
    }

    [Fact]
    public async Task ApplyVoucherAsync_RejectsFixedVoucherWithoutAmountConfiguration()
    {
        var voucher = CreateVoucher();
        voucher.DiscountType = "FIXED";
        voucher.DiscountAmount = null;

        var (service, _, _) = CreateService(voucher);

        var result = await service.ApplyVoucherAsync(
            CreateRequest(),
            UserId);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ApplyVoucherAsync_AppliesFixedDiscount()
    {
        var voucher = CreateVoucher();
        voucher.DiscountType = "FIXED";
        voucher.DiscountAmount = 50_000;

        var (service, _, _) = CreateService(voucher);

        var result = await service.ApplyVoucherAsync(
            CreateRequest(orderAmount: 400_000),
            UserId);

        Assert.True(result.IsValid);
        Assert.Equal(50_000, result.DiscountAmount);
        Assert.Equal(350_000, result.FinalAmount);
    }

    [Fact]
    public async Task ApplyVoucherAsync_DoesNotReduceFinalAmountBelowZero()
    {
        var voucher = CreateVoucher();
        voucher.DiscountType = "FIXED";
        voucher.DiscountAmount = 500_000;

        var (service, _, _) = CreateService(voucher);

        var result = await service.ApplyVoucherAsync(
            CreateRequest(orderAmount: 400_000),
            UserId);

        Assert.True(result.IsValid);
        Assert.Equal(400_000, result.DiscountAmount);
        Assert.Equal(0, result.FinalAmount);
    }

    [Fact]
    public async Task ApplyVoucherAsync_RejectsUnknownDiscountType()
    {
        var voucher = CreateVoucher();
        voucher.DiscountType = "UNKNOWN";

        var (service, _, _) = CreateService(voucher);

        var result = await service.ApplyVoucherAsync(
            CreateRequest(),
            UserId);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ApplyVoucherAsync_AcceptsSystemVoucher()
    {
        var voucher = CreateVoucher();
        voucher.Scope = "SYSTEM";
        voucher.DiscountType = "FIXED";
        voucher.DiscountAmount = 50_000;

        var (service, repository, eventClient) = CreateService(voucher);

        var result = await service.ApplyVoucherAsync(
            CreateRequest(),
            UserId);

        Assert.True(result.IsValid);
        Assert.Equal(50_000, result.DiscountAmount);
        Assert.Equal(350_000, result.FinalAmount);
        eventClient.Verify(
            c => c.GetOrganizerIdByEventIdAsync(EventId),
            Times.Once);

        // Applying a voucher checks eligibility; it does not record voucher usage.
        repository.Verify(
            r => r.RecordUsageAsync(It.IsAny<VoucherUsage>()),
            Times.Never);
    }

    [Fact]
    public async Task ApplyVoucherAsync_AcceptsEventVoucherForMatchingEvent()
    {
        var voucher = CreateVoucher();
        voucher.Scope = "EVENT";
        voucher.EventId = EventId;
        voucher.DiscountType = "FIXED";
        voucher.DiscountAmount = 50_000;

        var (service, _, _) = CreateService(voucher);

        var result = await service.ApplyVoucherAsync(
            CreateRequest(eventId: EventId),
            UserId);

        Assert.True(result.IsValid);
        Assert.Equal(50_000, result.DiscountAmount);
        Assert.Equal(350_000, result.FinalAmount);
    }

    [Fact]
    public async Task ApplyVoucherAsync_AcceptsOrganizerVoucherForMatchingOrganizer()
    {
        var voucher = CreateVoucher();
        voucher.Scope = "ORGANIZER";
        voucher.OrganizerId = OrganizerId;
        voucher.DiscountType = "FIXED";
        voucher.DiscountAmount = 50_000;

        var (service, _, _) = CreateService(
            voucher,
            eventOrganizerId: OrganizerId);

        var result = await service.ApplyVoucherAsync(
            CreateRequest(),
            UserId);

        Assert.True(result.IsValid);
        Assert.Equal(50_000, result.DiscountAmount);
        Assert.Equal(350_000, result.FinalAmount);
    }

    private static (
        VoucherService Service,
        Mock<IVoucherRepository> Repository,
        Mock<IEventApiClient> EventClient)
        CreateService(
            Voucher? voucher = null,
            int eventOrganizerId = OrganizerId,
            int userUsageCount = 0)
    {
        var repository = new Mock<IVoucherRepository>();
        var eventClient = new Mock<IEventApiClient>();
        var ticketClient = new Mock<ITicketApiClient>();

        if (voucher != null)
        {
            repository
                .Setup(r => r.GetByCodeAsync(voucher.Code))
                .ReturnsAsync(voucher);

            repository
                .Setup(r => r.CountUserUsagesAsync(
                    voucher.VoucherId,
                    It.IsAny<int>()))
                .ReturnsAsync(userUsageCount);
        }

        eventClient
            .Setup(c => c.GetOrganizerIdByEventIdAsync(It.IsAny<int>()))
            .ReturnsAsync(eventOrganizerId);

        var service = new VoucherService(
            repository.Object,
            eventClient.Object,
            ticketClient.Object);

        return (service, repository, eventClient);
    }

    private static ApplyVoucherRequestDTO CreateRequest(
        string code = "SAVE50",
        int eventId = EventId,
        long orderAmount = 400_000)
    {
        return new ApplyVoucherRequestDTO
        {
            Code = code,
            EventId = eventId,
            OrderAmount = orderAmount
        };
    }

    private static Voucher CreateVoucher()
    {
        return new Voucher
        {
            VoucherId = 12,
            Code = "SAVE50",
            Scope = "SYSTEM",
            DiscountType = "FIXED",
            DiscountAmount = 50_000,
            DiscountPercent = null,
            MaxDiscountAmount = null,
            MinOrderAmount = 0,
            TotalQuantity = 100,
            UsedQuantity = 0,
            MaxUsagePerUser = 5,
            StartsAt = DateTime.UtcNow.AddDays(-1),
            EndsAt = DateTime.UtcNow.AddDays(1),
            IsActive = true
        };
    }
}