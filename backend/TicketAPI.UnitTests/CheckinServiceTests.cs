using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BookingAPI.DTOS;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TicketAPI.API;
using TicketAPI.DTOs;
using TicketAPI.Models;
using TicketAPI.Services;
using static TicketAPI.DTOs.ConfirmEventSaleDto;
using Xunit;

namespace TicketAPI.UnitTests
{
    public class FakeEventApiClient : IEventApiClient
    {
        public EventApiResponseDto? EventToReturn { get; set; }

        public Task<EventApiResponseDto?> GetEventByIdAsync(int eventId) =>
            Task.FromResult(EventToReturn);

        public Task<EventApiResponseDto?> GetOwnedEventAsync(int eventId, string? bearerToken) =>
            Task.FromResult(EventToReturn);

        public Task<bool> ConfirmPaidOrderSaleAsync(int eventId, ConfirmEventSaleRequestDto request) =>
            Task.FromResult(true);

        public Task<bool> ReleaseReturnedTicketAsync(int eventId, int ticketTypeId, int quantity) =>
            Task.FromResult(true);

        public Task<InventoryReleaseOutcome> TryReleaseReturnedTicketAsync(int eventId, int ticketTypeId, int quantity) =>
            Task.FromResult(new InventoryReleaseOutcome(true, false, null));

        public Task<IReadOnlyList<Domain.RefundTier>> GetRefundTiersAsync(int eventId) =>
            Task.FromResult<IReadOnlyList<Domain.RefundTier>>(new List<Domain.RefundTier>());

        public Task<IReadOnlyList<int>> GetReturnReviewEventIdsAsync(int staffUserId) =>
            Task.FromResult<IReadOnlyList<int>>(new List<int>());
    }

    public class CheckinServiceTests
    {
        private static TicketDbContext CreateInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<TicketDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new TicketDbContext(options);
        }

        [Fact]
        public async Task FirstCheckin_ShouldSucceed_AndMarkInside()
        {
            using var context = CreateInMemoryContext();
            var ticketCode = Guid.NewGuid();
            var order = new Order { OrderId = 1, Status = "Paid", TotalAmount = 1000, FinalAmount = 1000 };
            context.Orders.Add(order);
            context.Tickets.Add(new Ticket
            {
                TicketId = 1,
                TicketCode = ticketCode,
                OrderId = 1,
                EventId = 100,
                Status = "Active",
                OwnerName = "Nguyen Van A"
            });
            await context.SaveChangesAsync();

            var fakeClient = new FakeEventApiClient
            {
                EventToReturn = new EventApiResponseDto { EventId = 100, ReEntryPolicy = "NoReEntry" }
            };

            var service = new CheckinService(context, fakeClient, NullLogger<CheckinService>.Instance);

            var result = await service.ProcessScanAsync(new CheckinScanRequestDto
            {
                TicketCode = ticketCode,
                GateName = "Gate A",
                ScanType = "Entry"
            }, staffUserId: 99);

            Assert.True(result.Success);
            Assert.Equal("Valid", result.ResultCode);

            var updatedTicket = await context.Tickets.FindAsync(1);
            Assert.NotNull(updatedTicket!.CheckedInAt);
            Assert.Equal(99, updatedTicket.CheckedInBy);
            Assert.Equal("Gate A", updatedTicket.CheckedInGate);
            Assert.True(updatedTicket.IsInside);
        }

        [Fact]
        public async Task DoubleScan_WhileInside_ShouldRejectAsAntiPassback()
        {
            using var context = CreateInMemoryContext();
            var ticketCode = Guid.NewGuid();
            var order = new Order { OrderId = 1, Status = "Paid", TotalAmount = 1000, FinalAmount = 1000 };
            context.Orders.Add(order);
            context.Tickets.Add(new Ticket
            {
                TicketId = 1,
                TicketCode = ticketCode,
                OrderId = 1,
                EventId = 100,
                Status = "Active",
                CheckedInAt = DateTime.UtcNow.AddMinutes(-30),
                CheckedInGate = "Gate A",
                IsInside = true
            });
            await context.SaveChangesAsync();

            var fakeClient = new FakeEventApiClient
            {
                EventToReturn = new EventApiResponseDto { EventId = 100, ReEntryPolicy = "NoReEntry" }
            };

            var service = new CheckinService(context, fakeClient, NullLogger<CheckinService>.Instance);

            var result = await service.ProcessScanAsync(new CheckinScanRequestDto
            {
                TicketCode = ticketCode,
                GateName = "Gate A",
                ScanType = "Entry"
            }, staffUserId: 99);

            Assert.False(result.Success);
            Assert.Equal("AntiPassback", result.ResultCode);
        }

        [Fact]
        public async Task ExitScan_ShouldMarkTicketOutside()
        {
            using var context = CreateInMemoryContext();
            var ticketCode = Guid.NewGuid();
            var order = new Order { OrderId = 1, Status = "Paid", TotalAmount = 1000, FinalAmount = 1000 };
            context.Orders.Add(order);
            context.Tickets.Add(new Ticket
            {
                TicketId = 1,
                TicketCode = ticketCode,
                OrderId = 1,
                EventId = 100,
                Status = "Active",
                CheckedInAt = DateTime.UtcNow.AddMinutes(-30),
                CheckedInGate = "Gate A",
                IsInside = true
            });
            await context.SaveChangesAsync();

            var fakeClient = new FakeEventApiClient
            {
                EventToReturn = new EventApiResponseDto
                {
                    EventId = 100,
                    ReEntryPolicy = "TimeLimited",
                    ReEntryDurationMinutes = 20
                }
            };

            var service = new CheckinService(context, fakeClient, NullLogger<CheckinService>.Instance);

            var result = await service.ProcessScanAsync(new CheckinScanRequestDto
            {
                TicketCode = ticketCode,
                GateName = "Gate A",
                ScanType = "Exit"
            }, staffUserId: 99);

            Assert.True(result.Success);
            Assert.Equal("ExitSuccess", result.ResultCode);

            var updatedTicket = await context.Tickets.FindAsync(1);
            Assert.False(updatedTicket!.IsInside);
            Assert.NotNull(updatedTicket.LastExitAt);
        }

        [Fact]
        public async Task ReEntry_WithNoReEntryPolicy_ShouldRejectAsAlreadyUsed()
        {
            using var context = CreateInMemoryContext();
            var ticketCode = Guid.NewGuid();
            var order = new Order { OrderId = 1, Status = "Paid", TotalAmount = 1000, FinalAmount = 1000 };
            context.Orders.Add(order);
            context.Tickets.Add(new Ticket
            {
                TicketId = 1,
                TicketCode = ticketCode,
                OrderId = 1,
                EventId = 100,
                Status = "Active",
                CheckedInAt = DateTime.UtcNow.AddMinutes(-40),
                LastExitAt = DateTime.UtcNow.AddMinutes(-10),
                CheckedInGate = "Gate A",
                IsInside = false
            });
            await context.SaveChangesAsync();

            var fakeClient = new FakeEventApiClient
            {
                EventToReturn = new EventApiResponseDto { EventId = 100, ReEntryPolicy = "NoReEntry" }
            };

            var service = new CheckinService(context, fakeClient, NullLogger<CheckinService>.Instance);

            var result = await service.ProcessScanAsync(new CheckinScanRequestDto
            {
                TicketCode = ticketCode,
                GateName = "Gate A",
                ScanType = "Entry"
            }, staffUserId: 99);

            Assert.False(result.Success);
            Assert.Equal("AlreadyUsed", result.ResultCode);
        }

        [Fact]
        public async Task ReEntry_WithUnlimitedPolicy_ShouldSucceed()
        {
            using var context = CreateInMemoryContext();
            var ticketCode = Guid.NewGuid();
            var order = new Order { OrderId = 1, Status = "Paid", TotalAmount = 1000, FinalAmount = 1000 };
            context.Orders.Add(order);
            context.Tickets.Add(new Ticket
            {
                TicketId = 1,
                TicketCode = ticketCode,
                OrderId = 1,
                EventId = 100,
                Status = "Active",
                CheckedInAt = DateTime.UtcNow.AddHours(-2),
                LastExitAt = DateTime.UtcNow.AddMinutes(-10),
                CheckedInGate = "Gate A",
                IsInside = false,
                ReEntryCount = 0
            });
            await context.SaveChangesAsync();

            var fakeClient = new FakeEventApiClient
            {
                EventToReturn = new EventApiResponseDto { EventId = 100, ReEntryPolicy = "Unlimited" }
            };

            var service = new CheckinService(context, fakeClient, NullLogger<CheckinService>.Instance);

            var result = await service.ProcessScanAsync(new CheckinScanRequestDto
            {
                TicketCode = ticketCode,
                GateName = "Gate A",
                ScanType = "Entry"
            }, staffUserId: 99);

            Assert.True(result.Success);
            Assert.Equal("ValidReEntry", result.ResultCode);

            var updatedTicket = await context.Tickets.FindAsync(1);
            Assert.True(updatedTicket!.IsInside);
            Assert.Equal(1, updatedTicket.ReEntryCount);
        }

        [Fact]
        public async Task ReEntry_WithTimeLimitedPolicy_WithinDuration_ShouldSucceed()
        {
            using var context = CreateInMemoryContext();
            var ticketCode = Guid.NewGuid();
            var order = new Order { OrderId = 1, Status = "Paid", TotalAmount = 1000, FinalAmount = 1000 };
            context.Orders.Add(order);
            context.Tickets.Add(new Ticket
            {
                TicketId = 1,
                TicketCode = ticketCode,
                OrderId = 1,
                EventId = 100,
                Status = "Active",
                CheckedInAt = DateTime.UtcNow.AddHours(-1),
                LastExitAt = DateTime.UtcNow.AddMinutes(-15), // Exited 15 mins ago
                CheckedInGate = "Gate A",
                IsInside = false
            });
            await context.SaveChangesAsync();

            var fakeClient = new FakeEventApiClient
            {
                EventToReturn = new EventApiResponseDto
                {
                    EventId = 100,
                    ReEntryPolicy = "TimeLimited",
                    ReEntryDurationMinutes = 30 // 30 mins allowed
                }
            };

            var service = new CheckinService(context, fakeClient, NullLogger<CheckinService>.Instance);

            var result = await service.ProcessScanAsync(new CheckinScanRequestDto
            {
                TicketCode = ticketCode,
                GateName = "Gate A",
                ScanType = "Entry"
            }, staffUserId: 99);

            Assert.True(result.Success);
            Assert.Equal("ValidReEntry", result.ResultCode);
            Assert.True(result.RemainingMinutes >= 10);
        }

        [Fact]
        public async Task ReEntry_WithTimeLimitedPolicy_ExpiredDuration_ShouldReject()
        {
            using var context = CreateInMemoryContext();
            var ticketCode = Guid.NewGuid();
            var order = new Order { OrderId = 1, Status = "Paid", TotalAmount = 1000, FinalAmount = 1000 };
            context.Orders.Add(order);
            context.Tickets.Add(new Ticket
            {
                TicketId = 1,
                TicketCode = ticketCode,
                OrderId = 1,
                EventId = 100,
                Status = "Active",
                CheckedInAt = DateTime.UtcNow.AddHours(-2),
                LastExitAt = DateTime.UtcNow.AddMinutes(-45), // Exited 45 mins ago
                CheckedInGate = "Gate A",
                IsInside = false
            });
            await context.SaveChangesAsync();

            var fakeClient = new FakeEventApiClient
            {
                EventToReturn = new EventApiResponseDto
                {
                    EventId = 100,
                    ReEntryPolicy = "TimeLimited",
                    ReEntryDurationMinutes = 30 // 30 mins allowed
                }
            };

            var service = new CheckinService(context, fakeClient, NullLogger<CheckinService>.Instance);

            var result = await service.ProcessScanAsync(new CheckinScanRequestDto
            {
                TicketCode = ticketCode,
                GateName = "Gate A",
                ScanType = "Entry"
            }, staffUserId: 99);

            Assert.False(result.Success);
            Assert.Equal("OutsideTimeLimit", result.ResultCode);
        }
    }
}
