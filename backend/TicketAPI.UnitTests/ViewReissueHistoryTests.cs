using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using TicketAPI.API;
using TicketAPI.Models;
using TicketAPI.Repositories;
using TicketAPI.Services;
using Xunit;

namespace TicketAPI.UnitTests
{
    /// <summary>
    /// Unit Tests for Function_060_ViewReissueHistory (GetReissueHistoryAsync)
    /// Total Test Cases: 9 (UTCID01..UTCID09: N=3, B=3, A=3)
    /// </summary>
    public class ViewReissueHistoryTests
    {
        private static List<TicketReissue> CreateSampleReissues(int eventId = 1001, int count = 3)
        {
            var list = new List<TicketReissue>();
            for (int i = 1; i <= count; i++)
            {
                list.Add(new TicketReissue
                {
                    ReissueId = i,
                    OldTicketCode = i == 1 ? "TK-OLD-999" : $"TK-OLD-{100 + i}",
                    NewTicketCode = i == 2 ? "TK-NEW-888" : $"TK-NEW-{200 + i}",
                    EventId = eventId,
                    EventTitle = $"Concert Event #{eventId}",
                    ReissuedByAdminId = 1,
                    ReissuedByName = "Platform Admin",
                    ReissuedAt = DateTime.UtcNow.AddMinutes(-10 * i),
                    Reason = "Reissued after security investigation",
                    CustomerName = $"Attendee {i}",
                    CustomerEmail = $"attendee{i}@example.com",
                    SeatOrZone = "VIP Zone",
                    DynamicQrStatus = "ACTIVE"
                });
            }
            return list;
        }

        private static TicketGovernanceService CreateService(Mock<ITicketGovernanceRepository> repoMock)
        {
            var services = new ServiceCollection().BuildServiceProvider();
            return new TicketGovernanceService(
                repoMock.Object,
                Mock.Of<IEventApiClient>(),
                Mock.Of<IStatusHistoryService>(),
                Mock.Of<IHttpClientFactory>(),
                Mock.Of<ILogger<TicketGovernanceService>>(),
                services);
        }

        [Fact]
        public async Task ViewReissueHistory_UTCID01_ValidEventIdAndNullSearch_ReturnsReissueHistoryItems()
        {
            var repo = new Mock<ITicketGovernanceRepository>();
            var sample = CreateSampleReissues(1001, 3);
            repo.Setup(r => r.GetReissueHistoryAsync(1001, null, 1, 10))
                .ReturnsAsync((sample, 3));
            var service = CreateService(repo);

            var result = await service.GetReissueHistoryAsync(1001, null, 1, 10, callerRole: "Admin");

            Assert.NotNull(result);
            Assert.True(result.Items.Count > 0);
            Assert.True(result.TotalCount >= result.Items.Count);
            Assert.Equal(3, result.Items.Count);
        }

        [Fact]
        public async Task ViewReissueHistory_UTCID02_SearchByOldTicketCode_ReturnsMatchingReissueRecord()
        {
            var repo = new Mock<ITicketGovernanceRepository>();
            var sample = new List<TicketReissue> { CreateSampleReissues(1001, 3)[0] };
            repo.Setup(r => r.GetReissueHistoryAsync(1001, "TK-OLD-999", 1, 10))
                .ReturnsAsync((sample, 1));
            var service = CreateService(repo);

            var result = await service.GetReissueHistoryAsync(1001, "TK-OLD-999", 1, 10, callerRole: "Admin");

            Assert.NotNull(result);
            var item = Assert.Single(result.Items);
            Assert.Equal("TK-OLD-999", item.OldTicketCode);
            Assert.Equal(1, result.TotalCount);
        }

        [Fact]
        public async Task ViewReissueHistory_UTCID03_SearchByNewTicketCode_ReturnsMatchingReissueRecord()
        {
            var repo = new Mock<ITicketGovernanceRepository>();
            var sample = new List<TicketReissue> { CreateSampleReissues(1001, 3)[1] };
            repo.Setup(r => r.GetReissueHistoryAsync(1001, "TK-NEW-888", 1, 10))
                .ReturnsAsync((sample, 1));
            var service = CreateService(repo);

            var result = await service.GetReissueHistoryAsync(1001, "TK-NEW-888", 1, 10, callerRole: "Admin");

            Assert.NotNull(result);
            var item = Assert.Single(result.Items);
            Assert.Equal("TK-NEW-888", item.NewTicketCode);
            Assert.Equal(1, result.TotalCount);
        }

        [Fact]
        public async Task ViewReissueHistory_UTCID04_BoundaryPageSizeMin1_ReturnsSingleItemPage()
        {
            var repo = new Mock<ITicketGovernanceRepository>();
            var sample = new List<TicketReissue> { CreateSampleReissues(1001, 3)[0] };
            repo.Setup(r => r.GetReissueHistoryAsync(1001, null, 1, 1))
                .ReturnsAsync((sample, 3));
            var service = CreateService(repo);

            var result = await service.GetReissueHistoryAsync(1001, null, 1, 1, callerRole: "Admin");

            Assert.Single(result.Items);
            Assert.Equal(3, result.TotalCount);
            Assert.Equal(1, result.PageSize);
        }

        [Fact]
        public async Task ViewReissueHistory_UTCID05_BoundaryPageSizeMax100_ReturnsAllItemsWithinLimit()
        {
            var repo = new Mock<ITicketGovernanceRepository>();
            var sample = CreateSampleReissues(1001, 5);
            repo.Setup(r => r.GetReissueHistoryAsync(1001, null, 1, 100))
                .ReturnsAsync((sample, 5));
            var service = CreateService(repo);

            var result = await service.GetReissueHistoryAsync(1001, null, 1, 100, callerRole: "Admin");

            Assert.Equal(5, result.Items.Count);
            Assert.Equal(5, result.TotalCount);
            Assert.Equal(100, result.PageSize);
        }

        [Fact]
        public async Task ViewReissueHistory_UTCID06_BoundaryEventWithNoReissues_ReturnsEmptyResult()
        {
            var repo = new Mock<ITicketGovernanceRepository>();
            repo.Setup(r => r.GetReissueHistoryAsync(1003, null, 1, 10))
                .ReturnsAsync((new List<TicketReissue>(), 0));
            var service = CreateService(repo);

            var result = await service.GetReissueHistoryAsync(1003, null, 1, 10, callerRole: "Admin");

            Assert.Empty(result.Items);
            Assert.Equal(0, result.TotalCount);
        }

        [Fact]
        public async Task ViewReissueHistory_UTCID07_BoundaryInvalidZeroEventIdAndPage_ThrowsArgumentException()
        {
            var repo = new Mock<ITicketGovernanceRepository>();
            var service = CreateService(repo);

            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                service.GetReissueHistoryAsync(0, null, 0, 10, callerRole: "Admin"));

            Assert.Equal("EventId must be positive", ex.Message);
        }

        [Fact]
        public async Task ViewReissueHistory_UTCID08_AbnormalNegativeEventIdAndPage_ThrowsArgumentException()
        {
            var repo = new Mock<ITicketGovernanceRepository>();
            var service = CreateService(repo);

            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                service.GetReissueHistoryAsync(-5, null, -1, 10, callerRole: "Admin"));

            Assert.Equal("EventId must be positive", ex.Message);
        }

        [Fact]
        public async Task ViewReissueHistory_UTCID09_AbnormalCustomerRoleCaller_ThrowsUnauthorizedAccessException()
        {
            var repo = new Mock<ITicketGovernanceRepository>();
            var service = CreateService(repo);

            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                service.GetReissueHistoryAsync(1001, null, 1, 10, callerRole: "Customer"));

            Assert.Equal("Admin access required", ex.Message);
        }
    }
}
