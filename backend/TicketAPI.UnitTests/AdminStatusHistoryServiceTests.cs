using Moq;
using TicketAPI.Models;
using TicketAPI.Repositories;
using TicketAPI.Services;
using Xunit;

namespace TicketAPI.Tests;

public class AdminStatusHistoryServiceTests
{
    [Fact]
    public async Task GetAdminHistoryAsync_ReturnsMappedHistoryAndPagination()
    {
        var from = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 10, 31, 23, 59, 0, DateTimeKind.Utc);
        var history = CreateHistory();

        var repository = new Mock<IStatusHistoryRepository>();
        repository
            .Setup(r => r.GetAdminHistoryAsync(
                42, "Seat", from, to, 2, 20))
            .ReturnsAsync((new List<StatusHistory> { history }, 35));

        var result = await CreateService(repository.Object)
            .GetAdminHistoryAsync(42, "Seat", from, to, page: 2, pageSize: 20);

        var item = Assert.Single(result.Items);
        Assert.Equal(2, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(35, result.TotalCount);

        Assert.Equal(history.StatusHistoryId, item.StatusHistoryId);
        Assert.Equal(history.TargetType, item.TargetType);
        Assert.Equal(history.TargetId, item.TargetId);
        Assert.Equal(history.EventId, item.EventId);
        Assert.Equal(history.OrderId, item.OrderId);
        Assert.Equal(history.TicketId, item.TicketId);
        Assert.Equal(history.TicketTypeId, item.TicketTypeId);
        Assert.Equal(history.SeatId, item.SeatId);
        Assert.Equal(history.HoldId, item.HoldId);
        Assert.Equal(history.Action, item.Action);
        Assert.Equal(history.OldStatus, item.OldStatus);
        Assert.Equal(history.NewStatus, item.NewStatus);
        Assert.Equal(history.Quantity, item.Quantity);
        Assert.Equal(history.ActorUserId, item.ActorUserId);
        Assert.Equal(history.ActorRole, item.ActorRole);
        Assert.Equal(history.Reason, item.Reason);
        Assert.Equal(history.CreatedAt, item.CreatedAt);
        Assert.Equal(history.Metadata, item.Metadata);

        repository.Verify(r => r.GetAdminHistoryAsync(
            42, "Seat", from, to, 2, 20), Times.Once);
    }

    [Fact]
    public async Task GetAdminHistoryAsync_ReturnsEmptyItemsAndTotalCount()
    {
        var repository = new Mock<IStatusHistoryRepository>();
        repository
            .Setup(r => r.GetAdminHistoryAsync(
                null, null, null, null, 1, 20))
            .ReturnsAsync((new List<StatusHistory>(), 0));

        var result = await CreateService(repository.Object)
            .GetAdminHistoryAsync(null, null, null, null, page: 1, pageSize: 20);

        Assert.Empty(result.Items);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task GetAdminHistoryAsync_ClampsPageBelowOneToOne()
    {
        var repository = new Mock<IStatusHistoryRepository>();
        repository
            .Setup(r => r.GetAdminHistoryAsync(
                null, null, null, null, 1, 20))
            .ReturnsAsync((new List<StatusHistory>(), 0));

        var result = await CreateService(repository.Object)
            .GetAdminHistoryAsync(null, null, null, null, page: 0, pageSize: 20);

        Assert.Equal(1, result.Page);
        repository.Verify(r => r.GetAdminHistoryAsync(
            null, null, null, null, 1, 20), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task GetAdminHistoryAsync_ClampsPageSizeBelowOneToOne(int pageSize)
    {
        var repository = new Mock<IStatusHistoryRepository>();
        repository
            .Setup(r => r.GetAdminHistoryAsync(
                null, null, null, null, 1, 1))
            .ReturnsAsync((new List<StatusHistory>(), 0));

        var result = await CreateService(repository.Object)
            .GetAdminHistoryAsync(null, null, null, null, page: 1, pageSize: pageSize);

        Assert.Equal(1, result.PageSize);
        repository.Verify(r => r.GetAdminHistoryAsync(
            null, null, null, null, 1, 1), Times.Once);
    }

    [Theory]
    [InlineData(101)]
    [InlineData(150)]
    public async Task GetAdminHistoryAsync_ClampsPageSizeAboveOneHundredToOneHundred(int pageSize)
    {
        var repository = new Mock<IStatusHistoryRepository>();
        repository
            .Setup(r => r.GetAdminHistoryAsync(
                null, null, null, null, 1, 100))
            .ReturnsAsync((new List<StatusHistory>(), 0));

        var result = await CreateService(repository.Object)
            .GetAdminHistoryAsync(null, null, null, null, page: 1, pageSize: pageSize);

        Assert.Equal(100, result.PageSize);
        repository.Verify(r => r.GetAdminHistoryAsync(
            null, null, null, null, 1, 100), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetAdminHistoryAsync_RejectsNonPositiveEventId(int eventId)
    {
        var repository = new Mock<IStatusHistoryRepository>();
        var service = CreateService(repository.Object);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetAdminHistoryAsync(
                eventId, null, null, null, page: 1, pageSize: 20));

        repository.Verify(r => r.GetAdminHistoryAsync(
            It.IsAny<int?>(),
            It.IsAny<string?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<int>(),
            It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetAdminHistoryAsync_RejectsFromLaterThanTo()
    {
        var from = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);
        var repository = new Mock<IStatusHistoryRepository>();
        var service = CreateService(repository.Object);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetAdminHistoryAsync(
                null, null, from, to, page: 1, pageSize: 20));

        repository.Verify(r => r.GetAdminHistoryAsync(
            It.IsAny<int?>(),
            It.IsAny<string?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<int>(),
            It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetAdminHistoryAsync_PropagatesRepositoryException()
    {
        var repository = new Mock<IStatusHistoryRepository>();
        repository
            .Setup(r => r.GetAdminHistoryAsync(
                null, null, null, null, 1, 20))
            .ThrowsAsync(new InvalidOperationException("Database unavailable"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(repository.Object)
                .GetAdminHistoryAsync(null, null, null, null, page: 1, pageSize: 20));

        Assert.Equal("Database unavailable", exception.Message);
    }

    private static StatusHistoryService CreateService(
        IStatusHistoryRepository repository)
    {
        return new StatusHistoryService(repository);
    }

    private static StatusHistory CreateHistory()
    {
        return new StatusHistory
        {
            StatusHistoryId = 7,
            TargetType = "Seat",
            TargetId = 101,
            EventId = 42,
            OrderId = 501,
            TicketId = 601,
            TicketTypeId = 3,
            SeatId = 101,
            HoldId = "hold-abc",
            Action = "SeatSold",
            OldStatus = "Held",
            NewStatus = "Sold",
            Quantity = 1,
            ActorUserId = 9,
            ActorRole = "Admin",
            Reason = "Payment completed",
            CreatedAt = new DateTime(2026, 10, 15, 12, 30, 0, DateTimeKind.Utc),
            Metadata = "{\"source\":\"unit-test\"}"
        };
    }
}