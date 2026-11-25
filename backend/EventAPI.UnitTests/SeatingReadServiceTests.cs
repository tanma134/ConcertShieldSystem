using EventAPI.Common;
using EventAPI.Models;
using EventAPI.Repositories;
using EventAPI.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EventAPI.UnitTests;

public class SeatingReadServiceTests
{
    [Fact]
    public async Task T01_GetByEventIdAsync_ReturnsMixedChartWithSortedZonesSeatsAndCoordinates()
    {
        var chart = CreateMixedChart();
        var seating = new Mock<ISeatingRepository>();
        seating.Setup(repository => repository.GetByEventIdAsync(42, true))
            .ReturnsAsync(chart);

        var access = CreateAllowedAccessMock();
        var service = CreateService(seating.Object, access.Object);

        var result = await service.GetByEventIdAsync(42, null, false);

        Assert.NotNull(result);
        Assert.Equal(5, result.SeatMapId);
        Assert.Equal(42, result.EventId);
        Assert.Equal("Main Hall", result.Name);
        Assert.Equal("{\"stage\":\"front\"}", result.LayoutJson);
        Assert.Equal(SeatingMode.ReservedSeating, result.SeatingMode);

        Assert.Collection(
            result.Zones,
            zone => Assert.Equal("Balcony", zone.ZoneName),
            zone => Assert.Equal("Floor A", zone.ZoneName),
            zone => Assert.Equal("Standing Pit", zone.ZoneName));

        var seatedZone = result.Zones.Single(zone => zone.ZoneName == "Floor A");

        Assert.Equal("Seated", seatedZone.ZoneType);
        Assert.Equal("{\"type\":\"rectangle\"}", seatedZone.ShapeJson);
        Assert.Equal(4, seatedZone.Capacity);
        Assert.Equal(4, seatedZone.TotalSeats);
        Assert.Equal(1, seatedZone.AvailableSeats);

        Assert.Equal(
            new[] { 101, 102, 103, 104 },
            seatedZone.Seats.Select(seat => seat.SeatId));

        Assert.Equal(
            new[] { "Available", "Sold", "Held", "Blocked" },
            seatedZone.Seats.Select(seat => seat.Status));

        Assert.Equal((int?)10, seatedZone.Seats[0].XCoordinate);
        Assert.Equal((int?)10, seatedZone.Seats[0].YCoordinate);
        Assert.Equal((int?)20, seatedZone.Seats[1].XCoordinate);
        Assert.Equal((int?)10, seatedZone.Seats[1].YCoordinate);
        Assert.Equal((int?)10, seatedZone.Seats[2].XCoordinate);
        Assert.Equal((int?)20, seatedZone.Seats[2].YCoordinate);

        var standingZone = result.Zones.Single(zone => zone.ZoneName == "Standing Pit");

        Assert.Equal("Standing", standingZone.ZoneType);
        Assert.Equal(50, standingZone.Capacity);
        Assert.Equal(0, standingZone.TotalSeats);
        Assert.Equal(50, standingZone.AvailableSeats);
        Assert.Empty(standingZone.Seats);

        access.Verify(
            item => item.EnsureVisibleAsync(42, null, false),
            Times.Once);

        seating.Verify(
            repository => repository.GetByEventIdAsync(42, true),
            Times.Once);
    }

    [Fact]
    public async Task T02_GetByEventIdAsync_AllStandingZonesReturnsStandingMode()
    {
        var map = new SeatMap
        {
            SeatMapId = 5,
            EventId = 42,
            Name = "Standing Concert"
        };

        map.SeatZones.Add(CreateZone(
            map,
            zoneId: 20,
            name: "General Admission",
            zoneType: "Standing",
            capacity: 200));

        var seating = new Mock<ISeatingRepository>();
        seating.Setup(repository => repository.GetByEventIdAsync(42, true))
            .ReturnsAsync(map);

        var service = CreateService(
            seating.Object,
            CreateAllowedAccessMock().Object);

        var result = await service.GetByEventIdAsync(42, null, false);

        Assert.NotNull(result);
        Assert.Equal(SeatingMode.StandingZones, result.SeatingMode);

        var zone = Assert.Single(result.Zones);
        Assert.Equal(200, zone.Capacity);
        Assert.Equal(0, zone.TotalSeats);
        Assert.Equal(200, zone.AvailableSeats);
        Assert.Empty(zone.Seats);
    }

    [Fact]
    public async Task T03_GetByEventIdAsync_ReturnsNullWhenChartDoesNotExist()
    {
        var seating = new Mock<ISeatingRepository>();
        seating.Setup(repository => repository.GetByEventIdAsync(42, true))
            .ReturnsAsync((SeatMap?)null);

        var access = CreateAllowedAccessMock();
        var service = CreateService(seating.Object, access.Object);

        var result = await service.GetByEventIdAsync(42, null, false);

        Assert.Null(result);

        access.Verify(
            item => item.EnsureVisibleAsync(42, null, false),
            Times.Once);

        seating.Verify(
            repository => repository.GetByEventIdAsync(42, true),
            Times.Once);
    }

    [Fact]
    public async Task T04_GetByEventIdAsync_WhenAccessIsDeniedDoesNotReadChart()
    {
        var seating = new Mock<ISeatingRepository>();
        var access = new Mock<IEventAccessService>();

        access.Setup(item => item.EnsureVisibleAsync(42, null, false))
            .ThrowsAsync(new InvalidOperationException("Access denied"));

        var service = CreateService(seating.Object, access.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetByEventIdAsync(42, null, false));

        seating.Verify(
            repository => repository.GetByEventIdAsync(
                It.IsAny<int>(),
                It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public async Task T05_GetByEventIdAsync_PropagatesRepositoryFailure()
    {
        var seating = new Mock<ISeatingRepository>();
        seating.Setup(repository => repository.GetByEventIdAsync(42, true))
            .ThrowsAsync(new InvalidOperationException("Database unavailable"));

        var access = CreateAllowedAccessMock();
        var service = CreateService(seating.Object, access.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetByEventIdAsync(42, null, false));

        access.Verify(
            item => item.EnsureVisibleAsync(42, null, false),
            Times.Once);
    }

    [Fact]
    public async Task T06_GetByEventIdAsync_ForwardsCallerAndAdminToAccessCheck()
    {
        var seating = new Mock<ISeatingRepository>();
        seating.Setup(repository => repository.GetByEventIdAsync(42, true))
            .ReturnsAsync((SeatMap?)null);

        var access = new Mock<IEventAccessService>();
        access.Setup(item => item.EnsureVisibleAsync(42, 7, true))
            .ReturnsAsync(new Event { EventId = 42 });

        var service = CreateService(seating.Object, access.Object);

        var result = await service.GetByEventIdAsync(42, 7, true);

        Assert.Null(result);

        access.Verify(
            item => item.EnsureVisibleAsync(42, 7, true),
            Times.Once);
    }

    [Fact]
    public async Task T07_GetZoneSeatsAsync_AvailableOnlyReturnsOnlyAvailableSeats()
    {
        var zone = GetFloorZoneFromMixedChart();

        var seating = new Mock<ISeatingRepository>();
        seating.Setup(repository => repository.GetZoneByIdAsync(9, true))
            .ReturnsAsync(zone);

        var access = CreateAllowedAccessMock();
        var service = CreateService(seating.Object, access.Object);

        var result = await service.GetZoneSeatsAsync(
            9,
            callerId: null,
            isAdmin: false,
            availableOnly: true);

        var seat = Assert.Single(result.Seats);
        Assert.Equal(101, seat.SeatId);
        Assert.Equal("Available", seat.Status);
        Assert.Equal("A", seat.RowLabel);
        Assert.Equal("1", seat.SeatNumber);
        Assert.Equal((int?)10, seat.XCoordinate);
        Assert.Equal((int?)10, seat.YCoordinate);

        access.Verify(
            item => item.EnsureVisibleAsync(42, null, false),
            Times.Once);
    }

    [Fact]
    public async Task T08_GetZoneSeatsAsync_WithoutAvailableOnlyReturnsAllSeatsInCoordinateOrder()
    {
        var zone = GetFloorZoneFromMixedChart();

        var seating = new Mock<ISeatingRepository>();
        seating.Setup(repository => repository.GetZoneByIdAsync(9, true))
            .ReturnsAsync(zone);

        var service = CreateService(
            seating.Object,
            CreateAllowedAccessMock().Object);

        var result = await service.GetZoneSeatsAsync(
            9,
            callerId: null,
            isAdmin: false,
            availableOnly: false);

        Assert.Equal(4, result.Seats.Count);

        Assert.Equal(
            new[] { 101, 102, 103, 104 },
            result.Seats.Select(seat => seat.SeatId));

        Assert.Equal(
            new[] { "Available", "Sold", "Held", "Blocked" },
            result.Seats.Select(seat => seat.Status));
    }

    [Fact]
    public async Task T09_GetZoneSeatsAsync_WhenZoneDoesNotExistThrowsAndDoesNotCheckAccess()
    {
        var seating = new Mock<ISeatingRepository>();
        seating.Setup(repository => repository.GetZoneByIdAsync(999, true))
            .ReturnsAsync((SeatZone?)null);

        var access = new Mock<IEventAccessService>();
        var service = CreateService(seating.Object, access.Object);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.GetZoneSeatsAsync(999, null, false));

        access.Verify(
            item => item.EnsureVisibleAsync(
                It.IsAny<int>(),
                It.IsAny<int?>(),
                It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public async Task T10_GetZoneSeatsAsync_ForStandingZoneThrowsInvalidOperationException()
    {
        var map = new SeatMap
        {
            SeatMapId = 5,
            EventId = 42,
            Name = "Main Hall"
        };

        var standingZone = CreateZone(
            map,
            zoneId: 30,
            name: "Standing Pit",
            zoneType: "Standing",
            capacity: 50);

        var seating = new Mock<ISeatingRepository>();
        seating.Setup(repository => repository.GetZoneByIdAsync(30, true))
            .ReturnsAsync(standingZone);

        var access = CreateAllowedAccessMock();
        var service = CreateService(seating.Object, access.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetZoneSeatsAsync(30, null, false));

        access.Verify(
            item => item.EnsureVisibleAsync(42, null, false),
            Times.Once);
    }

    [Fact]
    public async Task T11_GetZoneSeatsAsync_WhenAccessIsDeniedDoesNotReturnZoneSeats()
    {
        var zone = GetFloorZoneFromMixedChart();
        var seating = new Mock<ISeatingRepository>();
        seating.Setup(repository => repository.GetZoneByIdAsync(9, true))
            .ReturnsAsync(zone);

        var access = new Mock<IEventAccessService>();
        access.Setup(item => item.EnsureVisibleAsync(42, null, false))
            .ThrowsAsync(new InvalidOperationException("Access denied"));

        var service = CreateService(seating.Object, access.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetZoneSeatsAsync(9, null, false));

        access.Verify(
            item => item.EnsureVisibleAsync(42, null, false),
            Times.Once);
    }

    [Fact]
    public async Task T12_GetZoneSeatsAsync_PropagatesRepositoryFailureBeforeAccessCheck()
    {
        var seating = new Mock<ISeatingRepository>();
        seating.Setup(repository => repository.GetZoneByIdAsync(9, true))
            .ThrowsAsync(new InvalidOperationException("Database unavailable"));

        var access = new Mock<IEventAccessService>();
        var service = CreateService(seating.Object, access.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetZoneSeatsAsync(9, null, false));

        access.Verify(
            item => item.EnsureVisibleAsync(
                It.IsAny<int>(),
                It.IsAny<int?>(),
                It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public async Task T13_GetZoneSeatsAsync_UsesParentEventIdAndForwardsCallerAndAdmin()
    {
        var zone = GetFloorZoneFromMixedChart();
        var seating = new Mock<ISeatingRepository>();
        seating.Setup(repository => repository.GetZoneByIdAsync(9, true))
            .ReturnsAsync(zone);

        var access = new Mock<IEventAccessService>();
        access.Setup(item => item.EnsureVisibleAsync(42, 7, true))
            .ReturnsAsync(new Event { EventId = 42 });

        var service = CreateService(seating.Object, access.Object);

        var result = await service.GetZoneSeatsAsync(
            9,
            callerId: 7,
            isAdmin: true,
            availableOnly: false);

        Assert.Equal(4, result.Seats.Count);

        access.Verify(
            item => item.EnsureVisibleAsync(42, 7, true),
            Times.Once);
    }

    [Fact]
    public async Task T14_GetZoneSeatsAsync_SeatedZoneWithoutSeatRowsUsesConfiguredCapacity()
    {
        var map = new SeatMap
        {
            SeatMapId = 5,
            EventId = 42,
            Name = "Main Hall"
        };

        var emptySeatedZone = CreateZone(
            map,
            zoneId: 40,
            name: "Empty Seated Zone",
            zoneType: "Seated",
            capacity: 5);

        var seating = new Mock<ISeatingRepository>();
        seating.Setup(repository => repository.GetZoneByIdAsync(40, true))
            .ReturnsAsync(emptySeatedZone);

        var service = CreateService(
            seating.Object,
            CreateAllowedAccessMock().Object);

        var result = await service.GetZoneSeatsAsync(40, null, false);

        Assert.Equal(5, result.Capacity);
        Assert.Equal(5, result.TotalSeats);
        Assert.Equal(0, result.AvailableSeats);
        Assert.Empty(result.Seats);
    }

    private static SeatingService CreateService(
        ISeatingRepository seating,
        IEventAccessService access)
    {
        return new SeatingService(
            seating,
            Mock.Of<IEventRepository>(),
            Mock.Of<ITicketTypeRepository>(),
            access,
            Mock.Of<ILogger<SeatingService>>());
    }

    private static Mock<IEventAccessService> CreateAllowedAccessMock()
    {
        var access = new Mock<IEventAccessService>();

        access.Setup(item => item.EnsureVisibleAsync(42, null, false))
            .ReturnsAsync(new Event { EventId = 42 });

        return access;
    }

    private static SeatMap CreateMixedChart()
    {
        var map = new SeatMap
        {
            SeatMapId = 5,
            EventId = 42,
            Name = "Main Hall",
            LayoutJson = "{\"stage\":\"front\"}"
        };

        // Deliberately add zones out of name order to verify chart sorting.
        map.SeatZones.Add(CreateZone(
            map,
            zoneId: 30,
            name: "Standing Pit",
            zoneType: "Standing",
            capacity: 50));

        map.SeatZones.Add(CreateZone(
            map,
            zoneId: 9,
            name: "Floor A",
            zoneType: "Seated",
            capacity: 99,
            CreateSeat(104, "B", "2", 20, 20, "Blocked"),
            CreateSeat(102, "A", "2", 20, 10, "Sold"),
            CreateSeat(103, "B", "1", 10, 20, "Held"),
            CreateSeat(101, "A", "1", 10, 10, "Available")));

        map.SeatZones.Add(CreateZone(
            map,
            zoneId: 15,
            name: "Balcony",
            zoneType: "Seated",
            capacity: 2,
            CreateSeat(201, "A", "1", 10, 10, "Available"),
            CreateSeat(202, "A", "2", 20, 10, "Available")));

        return map;
    }

    private static SeatZone GetFloorZoneFromMixedChart()
    {
        return CreateMixedChart()
            .SeatZones
            .Single(zone => zone.SeatZoneId == 9);
    }

    private static SeatZone CreateZone(
        SeatMap map,
        int zoneId,
        string name,
        string zoneType,
        int capacity,
        params Seat[] seats)
    {
        var zone = new SeatZone
        {
            SeatZoneId = zoneId,
            SeatMapId = map.SeatMapId,
            TicketTypeId = 3,
            ZoneName = name,
            ShapeJson = "{\"type\":\"rectangle\"}",
            ZoneType = zoneType,
            Capacity = capacity,
            SeatMap = map
        };

        foreach (var seat in seats)
        {
            seat.SeatZoneId = zoneId;
            zone.Seats.Add(seat);
        }

        return zone;
    }

    private static Seat CreateSeat(
        int seatId,
        string rowLabel,
        string seatNumber,
        int x,
        int y,
        string status)
    {
        return new Seat
        {
            SeatId = seatId,
            RowLabel = rowLabel,
            SeatNumber = seatNumber,
            XCoordinate = x,
            YCoordinate = y,
            Status = status
        };
    }
}