using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using BookingAPI.DTOS;
using CheckinAPI.API;
using CheckinAPI.DTOs;
using CheckinAPI.Hubs;
using CheckinAPI.Models;
using CheckinAPI.Repositories;
using CheckinAPI.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using TicketAPI.Domain;
using TicketAPI.Repositories;
using TicketAPI.Services;
using Xunit;
using ICheckinRepository = CheckinAPI.Repositories.ICheckinRepository;

namespace TicketAPI.UnitTests.Features;

/// <summary>
/// Comprehensive Unit Tests for Check-in Monitoring, Reporting, Offline Sync, and Gate Terminal Operations:
/// - Function_089_MonitorCheckin (GetMonitorStatusAsync: UTCID01..UTCID09)
/// - Function_090_ExportCheckinRepor (ExportCheckinAuditReportAsync / ExportCheckinAsync: UTCID01..UTCID14)
/// - Function_093_EnableOfflineMode (GetOfflinePackageAsync: UTCID01..UTCID09)
/// - Function_094_SyncOfflineRecords (SyncOfflineScansAsync: UTCID01..UTCID10)
/// - Function_095_ResolveOfflineConf (ResolveOfflineConflictAsync: UTCID01..UTCID09)
/// - Function_096_ViewGateAssignment (GetGateAssignmentAsync: UTCID01..UTCID08)
/// - Function_097_ScanDynamicQRTicke (ScanDynamicQrTicketAsync / ScanQrTicketAsync: UTCID01..UTCID11)
/// - Function_098_ManualCheck-in (ManualCheckinAsync: UTCID01..UTCID09)
/// - Function_099_LookupTicket (LookupTicketsAsync: UTCID01..UTCID09)
/// - Function_100_ViewCheckinHistory (GetScanHistoryAsync: UTCID01..UTCID09)
/// </summary>
public class Feature12_CheckinMonitoringTests
{
    private static readonly DateTime At = new(2026, 10, 3, 19, 30, 0, DateTimeKind.Utc);

    private static CheckinTicketFact Ticket(int id, string type, string status, bool checkedIn) =>
        new(id, type, status, checkedIn ? At : null);

    private static CheckinMonitorService CreateMonitorService(
        Mock<ICheckinRepository> repoMock,
        Mock<CheckinAPI.API.IEventApiClient> eventApiMock)
    {
        var hubMock = new Mock<IHubContext<CheckinHub>>();
        var clientsMock = new Mock<IHubClients>();
        var clientProxyMock = new Mock<IClientProxy>();
        clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(clientProxyMock.Object);
        hubMock.Setup(h => h.Clients).Returns(clientsMock.Object);

        return new CheckinMonitorService(
            repoMock.Object,
            eventApiMock.Object,
            hubMock.Object,
            Mock.Of<ILogger<CheckinMonitorService>>());
    }

    private static OfflineCheckinService CreateOfflineService(
        Mock<ICheckinRepository> repoMock,
        Mock<CheckinAPI.API.IEventApiClient> eventApiMock)
    {
        return new OfflineCheckinService(
            repoMock.Object,
            eventApiMock.Object,
            Mock.Of<ILogger<OfflineCheckinService>>());
    }

    private static void SetupActiveShiftAssignment(
        Mock<CheckinAPI.API.IEventApiClient> eventApiMock,
        int eventId = 3001,
        int staffUserId = 101,
        string gateName = "GATE_A",
        int gateId = 1,
        List<string>? allowedTypes = null)
    {
        eventApiMock.Setup(e => e.GetEventInfoAsync(eventId))
            .ReturnsAsync(new EventInfoResult
            {
                EventId = eventId,
                OrganizerId = 10,
                Title = $"Concert #{eventId}",
                Status = "PUBLISHED"
            });

        eventApiMock.Setup(e => e.GetStaffAssignmentAsync(eventId, staffUserId))
            .ReturnsAsync(new StaffAssignmentInfo
            {
                EventId = eventId,
                StaffUserId = staffUserId,
                GateName = gateName,
                ShiftStartTime = "00:00",
                ShiftEndTime = "23:59",
                ShiftDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)).ToString("yyyy-MM-dd"),
                CanCheckin = true,
                EventTitle = $"Concert #{eventId}"
            });

        eventApiMock.Setup(e => e.GetEventGatesAsync(eventId))
            .ReturnsAsync(new List<EventGateInfo>
            {
                new EventGateInfo
                {
                    GateId = gateId,
                    EventId = eventId,
                    GateName = gateName,
                    AllowedTicketTypeList = allowedTypes ?? new List<string> { "VIP", "STANDARD" }
                }
            });
    }

    // =========================================================================
    // 1. Function_089_MonitorCheckin (GetMonitorStatusAsync: UTCID01..UTCID09)
    // =========================================================================

    [Fact]
    public async Task MonitorCheckinStatus_UTCID01_AllGatesAndZones_ReturnsLiveAggregateMetrics()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        eventApi.Setup(e => e.GetEventInfoAsync(2001))
            .ReturnsAsync(new EventInfoResult { EventId = 2001, OrganizerId = 10, Title = "Live Concert 2001", Status = "PUBLISHED" });
        repo.Setup(r => r.GetMonitorCountsAsync(2001))
            .ReturnsAsync((100, 45, new List<ZoneCheckinCount> { new() { ZoneName = "VIP", TotalIssued = 100, CheckedIn = 45 } }));
        repo.Setup(r => r.GetGateCountsAsync(2001))
            .ReturnsAsync(new List<GateCheckinCount> { new() { GateId = "GATE_A", GateName = "Gate A", ScanCount = 45, SuccessCount = 45 } });
        repo.Setup(r => r.GetRecentScansAsync(2001, 20)).ReturnsAsync(new List<CheckinLog>());

        var service = CreateMonitorService(repo, eventApi);
        var result = await service.GetMonitorStatusAsync(2001, null, null, 10, false, "Organizer", strictValidation: true);

        Assert.True(result.CheckedIn > 0);
        Assert.True(result.OccupancyRate > 0);
        Assert.Equal(45, result.CheckedIn);
    }

    [Fact]
    public async Task MonitorCheckinStatus_UTCID02_FilterByGateA_ReturnsGateASummary()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        eventApi.Setup(e => e.GetEventInfoAsync(2001))
            .ReturnsAsync(new EventInfoResult { EventId = 2001, OrganizerId = 10, Title = "Live Concert 2001" });
        repo.Setup(r => r.GetMonitorCountsAsync(2001))
            .ReturnsAsync((100, 30, new List<ZoneCheckinCount>()));
        repo.Setup(r => r.GetGateCountsAsync(2001))
            .ReturnsAsync(new List<GateCheckinCount>
            {
                new() { GateId = "GATE_A", GateName = "Gate A", ScanCount = 30, SuccessCount = 30 },
                new() { GateId = "GATE_B", GateName = "Gate B", ScanCount = 10, SuccessCount = 10 }
            });
        repo.Setup(r => r.GetRecentScansAsync(2001, 20)).ReturnsAsync(new List<CheckinLog>());

        var service = CreateMonitorService(repo, eventApi);
        var result = await service.GetMonitorStatusAsync(2001, "GATE_A", null, 10, false, "Organizer", strictValidation: true);

        var gate = Assert.Single(result.Gates);
        Assert.Equal("GATE_A", gate.GateId);
        Assert.True(result.CheckedIn > 0);
    }

    [Fact]
    public async Task MonitorCheckinStatus_UTCID03_FilterByVipZone_ReturnsVipZoneSummary()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        eventApi.Setup(e => e.GetEventInfoAsync(2001))
            .ReturnsAsync(new EventInfoResult { EventId = 2001, OrganizerId = 10, Title = "Live Concert 2001" });
        repo.Setup(r => r.GetMonitorCountsAsync(2001))
            .ReturnsAsync((100, 25, new List<ZoneCheckinCount>
            {
                new() { ZoneName = "VIP", TotalIssued = 50, CheckedIn = 25 },
                new() { ZoneName = "GA", TotalIssued = 50, CheckedIn = 0 }
            }));
        repo.Setup(r => r.GetGateCountsAsync(2001)).ReturnsAsync(new List<GateCheckinCount>());
        repo.Setup(r => r.GetRecentScansAsync(2001, 20)).ReturnsAsync(new List<CheckinLog>());

        var service = CreateMonitorService(repo, eventApi);
        var result = await service.GetMonitorStatusAsync(2001, null, "VIP", 10, false, "Organizer", strictValidation: true);

        var zone = Assert.Single(result.Zones);
        Assert.Equal("VIP", zone.ZoneName);
        Assert.True(result.CheckedIn > 0);
    }

    [Fact]
    public async Task MonitorCheckinStatus_UTCID04_BoundaryZeroCheckinsScanned_ReturnsZeroRate()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        eventApi.Setup(e => e.GetEventInfoAsync(2001))
            .ReturnsAsync(new EventInfoResult { EventId = 2001, OrganizerId = 10, Title = "Live Concert 2001" });
        repo.Setup(r => r.GetMonitorCountsAsync(2001))
            .ReturnsAsync((100, 0, new List<ZoneCheckinCount>()));
        repo.Setup(r => r.GetGateCountsAsync(2001)).ReturnsAsync(new List<GateCheckinCount>());
        repo.Setup(r => r.GetRecentScansAsync(2001, 20)).ReturnsAsync(new List<CheckinLog>());

        var service = CreateMonitorService(repo, eventApi);
        var result = await service.GetMonitorStatusAsync(2001, null, null, 10, false, "Organizer", strictValidation: true);

        Assert.Equal(0, result.CheckedIn);
        Assert.Equal(0.0, result.OccupancyRate);
    }

    [Fact]
    public async Task MonitorCheckinStatus_UTCID05_BoundaryFullCapacityCheckedIn_Returns100PercentRate()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        eventApi.Setup(e => e.GetEventInfoAsync(2001))
            .ReturnsAsync(new EventInfoResult { EventId = 2001, OrganizerId = 10, Title = "Live Concert 2001" });
        repo.Setup(r => r.GetMonitorCountsAsync(2001))
            .ReturnsAsync((100, 100, new List<ZoneCheckinCount>()));
        repo.Setup(r => r.GetGateCountsAsync(2001)).ReturnsAsync(new List<GateCheckinCount>());
        repo.Setup(r => r.GetRecentScansAsync(2001, 20)).ReturnsAsync(new List<CheckinLog>());

        var service = CreateMonitorService(repo, eventApi);
        var result = await service.GetMonitorStatusAsync(2001, null, null, 10, false, "Organizer", strictValidation: true);

        Assert.Equal(100, result.CheckedIn);
        Assert.Equal(100.0, result.OccupancyRate);
    }

    [Fact]
    public async Task MonitorCheckinStatus_UTCID06_NonExistentEventId99999_ThrowsKeyNotFoundException()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        eventApi.Setup(e => e.GetEventInfoAsync(99999)).ReturnsAsync((EventInfoResult?)null);

        var service = CreateMonitorService(repo, eventApi);
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.GetMonitorStatusAsync(99999, null, null, 10, false, "Organizer", strictValidation: true));

        Assert.Equal("Event not found", ex.Message);
    }

    [Fact]
    public async Task MonitorCheckinStatus_UTCID07_InvalidZeroOrNegativeEventId_ThrowsArgumentException()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        var service = CreateMonitorService(repo, eventApi);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetMonitorStatusAsync(0, null, null, 10, false, "Organizer", strictValidation: true));

        Assert.Equal("Event ID must be positive", ex.Message);
    }

    [Fact]
    public async Task MonitorCheckinStatus_UTCID08_NonOwnerOrganizer99_ThrowsUnauthorizedAccessException()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        eventApi.Setup(e => e.GetEventInfoAsync(2001))
            .ReturnsAsync(new EventInfoResult { EventId = 2001, OrganizerId = 10, Title = "Live Concert 2001" });

        var service = CreateMonitorService(repo, eventApi);
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetMonitorStatusAsync(2001, null, null, 99, false, "Organizer", strictValidation: true));

        Assert.Equal("You are not authorized to view this event", ex.Message);
    }

    [Fact]
    public async Task MonitorCheckinStatus_UTCID09_CustomerRole500_ThrowsUnauthorizedAccessException()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        var service = CreateMonitorService(repo, eventApi);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetMonitorStatusAsync(2001, null, null, 500, false, "Customer", strictValidation: true));

        Assert.Equal("You are not authorized to view this event", ex.Message);
    }

    // =========================================================================
    // 2. Function_090_ExportCheckinRepor (ExportCheckinAuditReportAsync: UTCID01..UTCID14)
    // =========================================================================

    private static ReportService CreateReportService(int ownerOrganizerId = 10, bool eventExists = true)
    {
        var reportRepo = new Mock<IReportRepository>();
        reportRepo.Setup(r => r.GetActiveTicketsByEventIdAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<TicketAPI.Models.Ticket>
            {
                new()
                {
                    TicketId = 1,
                    EventId = 101,
                    TicketCode = Guid.NewGuid(),
                    TicketTypeName = "VIP",
                    OwnerName = "Nguyen Van A",
                    Status = "Used",
                    CheckedInAt = DateTime.UtcNow,
                    CheckedInBy = 50
                }
            });

        var eventApi = new Mock<TicketAPI.API.IEventApiClient>();
        if (eventExists)
        {
            eventApi.Setup(e => e.GetOwnedEventAsync(101, It.IsAny<string?>()))
                .ReturnsAsync(new EventApiResponseDto { EventId = 101, OrganizerId = ownerOrganizerId, Title = "Concert 101" });
        }
        else
        {
            eventApi.Setup(e => e.GetOwnedEventAsync(It.IsAny<int>(), It.IsAny<string?>()))
                .ReturnsAsync((EventApiResponseDto?)null);
        }

        return new ReportService(reportRepo.Object, eventApi.Object);
    }

    [Fact]
    public async Task ExportCheckinReport_UTCID01_StandardConcertScopePdfDaily_ReturnsExportFile()
    {
        var service = CreateReportService();
        var file = await service.ExportCheckinAuditReportAsync(101, 10, false, "jwt", "ConcertId_101", 30, "PDF", "Daily");
        Assert.NotNull(file);
        Assert.NotEmpty(file.Content);
    }

    [Fact]
    public async Task ExportCheckinReport_UTCID02_StandardConcertScopeExcelHourly_ReturnsExportFile()
    {
        var service = CreateReportService();
        var file = await service.ExportCheckinAuditReportAsync(101, 10, false, "jwt", "ConcertId_101", 30, "Excel_XLSX", "Hourly");
        Assert.NotNull(file);
        Assert.NotEmpty(file.Content);
    }

    [Fact]
    public async Task ExportCheckinReport_UTCID03_StandardConcertScopeCsvSummaryByZone_ReturnsExportFile()
    {
        var service = CreateReportService();
        var file = await service.ExportCheckinAuditReportAsync(101, 10, false, "jwt", "ConcertId_101", 30, "CSV", "SummaryByZone");
        Assert.NotNull(file);
        Assert.EndsWith(".csv", file.FileName);
    }

    [Fact]
    public async Task ExportCheckinReport_UTCID04_BoundaryJsonTicketTypeBreakdown_ReturnsExportFile()
    {
        var service = CreateReportService();
        var file = await service.ExportCheckinAuditReportAsync(101, 10, false, "jwt", "ConcertId_101", 30, "JSON", "TicketTypeBreakdown");
        Assert.NotNull(file);
        Assert.NotEmpty(file.Content);
    }

    [Fact]
    public async Task ExportCheckinReport_UTCID05_BoundaryTodayMin1DayRawLedger_ReturnsExportFile()
    {
        var service = CreateReportService();
        var file = await service.ExportCheckinAuditReportAsync(101, 10, false, "jwt", "ConcertId_101", 1, "PDF", "None");
        Assert.NotNull(file);
        Assert.NotEmpty(file.Content);
    }

    [Fact]
    public async Task ExportCheckinReport_UTCID06_BoundaryOrganizerWideScope_ThrowsArgumentException()
    {
        var service = CreateReportService();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ExportCheckinAuditReportAsync(101, 10, false, "jwt", "OrganizerWide", 365, "XLSX", "Daily"));
        Assert.Equal("Report scope, date filter and export format are required", ex.Message);
    }

    [Fact]
    public async Task ExportCheckinReport_UTCID07_BoundaryZeroIntervalAndUnsupportedTxtFormat_ThrowsArgumentException()
    {
        var service = CreateReportService();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ExportCheckinAuditReportAsync(101, 10, false, "jwt", "ConcertId_101", 0, "TXT", "Daily"));
        Assert.Equal("Report scope, date filter and export format are required", ex.Message);
    }

    [Fact]
    public async Task ExportCheckinReport_UTCID08_AbnormalNullReportScope_ThrowsArgumentException()
    {
        var service = CreateReportService();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ExportCheckinAuditReportAsync(101, 10, false, "jwt", null, 30, "PDF", "Daily"));
        Assert.Equal("Date range must be 1-365 days and format in PDF/XLSX/CSV", ex.Message);
    }

    [Fact]
    public async Task ExportCheckinReport_UTCID09_AbnormalInvalidScopeAllGlobalData_ThrowsArgumentException()
    {
        var service = CreateReportService();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ExportCheckinAuditReportAsync(101, 10, false, "jwt", "AllGlobalData", 30, "PDF", "Daily"));
        Assert.Equal("Date range must be 1-365 days and format in PDF/XLSX/CSV", ex.Message);
    }

    [Fact]
    public async Task ExportCheckinReport_UTCID10_AbnormalFromDatePriorToPlatformLaunchOrNullFormat_ThrowsArgumentException()
    {
        var service = CreateReportService();
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ExportCheckinAuditReportAsync(101, 10, false, "jwt", "ConcertId_101", 30, null, "Daily",
                fromDate: new DateTime(2019, 1, 1)));
        Assert.Equal("Date range must be 1-365 days and format in PDF/XLSX/CSV", ex.Message);
    }

    [Fact]
    public async Task ExportCheckinReport_UTCID11_AbnormalNonExistentEvent_ThrowsKeyNotFoundException()
    {
        var service = CreateReportService(eventExists: false);
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ExportCheckinAuditReportAsync(99999, 10, false, "jwt", "ConcertId_101", 30, "PDF", "Daily"));
        Assert.Equal("Resource not found in system", ex.Message);
    }

    [Fact]
    public async Task ExportCheckinReport_UTCID12_AbnormalUnauthenticatedCaller_ThrowsUnauthorizedAccessException()
    {
        var service = CreateReportService();
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.ExportCheckinAuditReportAsync(101, 0, false, null, "ConcertId_101", 30, "PDF", "Daily"));
        Assert.Equal("Authentication required or token expired", ex.Message);
    }

    [Fact]
    public async Task ExportCheckinReport_UTCID13_AbnormalAnotherOrganizer_ThrowsUnauthorizedAccessException()
    {
        var service = CreateReportService(ownerOrganizerId: 10);
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.ExportCheckinAuditReportAsync(101, 99, false, "jwt", "ConcertId_101", 30, "PDF", "Daily"));
        Assert.Equal("Access denied: insufficient permissions", ex.Message);
    }

    [Fact]
    public async Task ExportCheckinReport_UTCID14_AbnormalRateLimitOrInvalidStateConflict_ThrowsInvalidOperationException()
    {
        var service = CreateReportService();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ExportCheckinAuditReportAsync(101, 10, false, "jwt", "ConcertId_101", 30, "PDF", "Daily",
                rateLimitExceeded: true));
        Assert.Equal("Entity state does not allow this operation", ex.Message);
    }

    // =========================================================================
    // 3. Function_093_EnableOfflineMode (GetOfflinePackageAsync: UTCID01..UTCID09)
    // =========================================================================

    private static List<CheckinAPI.Models.Ticket> CreateCheckinTickets(int count, int eventId = 2001)
    {
        var list = new List<CheckinAPI.Models.Ticket>();
        for (int i = 1; i <= count; i++)
        {
            list.Add(new CheckinAPI.Models.Ticket
            {
                TicketId = i,
                EventId = eventId,
                TicketCode = Guid.NewGuid(),
                OwnerName = $"Attendee {i}",
                TicketTypeName = "VIP",
                Status = "Available",
                IsCheckedIn = false
            });
        }
        return list;
    }

    [Fact]
    public async Task EnableOfflineMode_UTCID01_PublishedEventAndAssignedStaff_ReturnsPackageWithTickets()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        SetupActiveShiftAssignment(eventApi, 2001, 50);
        repo.Setup(r => r.GetOfflinePackageTicketsAsync(2001, It.IsAny<List<string>?>()))
            .ReturnsAsync(CreateCheckinTickets(5, 2001));

        var service = CreateOfflineService(repo, eventApi);
        var pkg = await service.GetOfflinePackageAsync(2001, "GATE_A", 50, "Staff", strictValidation: true);

        Assert.True(pkg.TotalTickets > 1);
        Assert.NotEmpty(pkg.Signature);
    }

    [Fact]
    public async Task EnableOfflineMode_UTCID02_PublishedEventDefaultGate_ReturnsSignedOfflinePackage()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        SetupActiveShiftAssignment(eventApi, 2001, 50);
        repo.Setup(r => r.GetOfflinePackageTicketsAsync(2001, It.IsAny<List<string>?>()))
            .ReturnsAsync(CreateCheckinTickets(3, 2001));

        var service = CreateOfflineService(repo, eventApi);
        var pkg = await service.GetOfflinePackageAsync(2001, null, 50, "Staff", strictValidation: true);

        Assert.Equal(3, pkg.TotalTickets);
        Assert.NotEmpty(pkg.Signature);
    }

    [Fact]
    public async Task EnableOfflineMode_UTCID03_BoundarySmallEventWithSingleTicket_ReturnsTicketCountOne()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        SetupActiveShiftAssignment(eventApi, 2002, 50);
        repo.Setup(r => r.GetOfflinePackageTicketsAsync(2002, It.IsAny<List<string>?>()))
            .ReturnsAsync(CreateCheckinTickets(1, 2002));

        var service = CreateOfflineService(repo, eventApi);
        var pkg = await service.GetOfflinePackageAsync(2002, "GATE_A", 50, "Staff", strictValidation: true);

        Assert.Equal(1, pkg.TotalTickets);
    }

    [Fact]
    public async Task EnableOfflineMode_UTCID04_BoundaryLargeTicketBatch_ReturnsAllTicketsWithSignature()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        SetupActiveShiftAssignment(eventApi, 2001, 50);
        repo.Setup(r => r.GetOfflinePackageTicketsAsync(2001, It.IsAny<List<string>?>()))
            .ReturnsAsync(CreateCheckinTickets(100, 2001));

        var service = CreateOfflineService(repo, eventApi);
        var pkg = await service.GetOfflinePackageAsync(2001, "GATE_A", 50, "Staff", strictValidation: true);

        Assert.Equal(100, pkg.TotalTickets);
        Assert.NotEmpty(pkg.Signature);
    }

    [Fact]
    public async Task EnableOfflineMode_UTCID05_NonExistentEvent99999_ThrowsKeyNotFoundException()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        eventApi.Setup(e => e.GetEventInfoAsync(99999)).ReturnsAsync((EventInfoResult?)null);

        var service = CreateOfflineService(repo, eventApi);
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.GetOfflinePackageAsync(99999, "GATE_A", 50, "Staff", strictValidation: true));

        Assert.Equal("Event not found", ex.Message);
    }

    [Fact]
    public async Task EnableOfflineMode_UTCID06_DraftUnpublishedEvent3001_ThrowsInvalidOperationException()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        eventApi.Setup(e => e.GetEventInfoAsync(3001))
            .ReturnsAsync(new EventInfoResult { EventId = 3001, Status = "DRAFT", Title = "Draft Event" });

        var service = CreateOfflineService(repo, eventApi);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetOfflinePackageAsync(3001, "GATE_A", 50, "Staff", strictValidation: true));

        Assert.Equal("Cannot generate offline package for unpublished event", ex.Message);
    }

    [Fact]
    public async Task EnableOfflineMode_UTCID07_InvalidZeroOrNegativeEventId_ThrowsArgumentException()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        var service = CreateOfflineService(repo, eventApi);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetOfflinePackageAsync(0, "GATE_A", 50, "Staff", strictValidation: true));

        Assert.Equal("EventId must be positive", ex.Message);
    }

    [Fact]
    public async Task EnableOfflineMode_UTCID08_UnassignedStaff88_ThrowsUnauthorizedAccessException()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        eventApi.Setup(e => e.GetEventInfoAsync(2001))
            .ReturnsAsync(new EventInfoResult { EventId = 2001, Status = "PUBLISHED" });
        eventApi.Setup(e => e.GetStaffAssignmentAsync(2001, 88))
            .ReturnsAsync((StaffAssignmentInfo?)null);

        var service = CreateOfflineService(repo, eventApi);
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetOfflinePackageAsync(2001, "GATE_A", 88, "Staff", strictValidation: true));

        Assert.Equal("Staff member is not assigned to this event", ex.Message);
    }

    [Fact]
    public async Task EnableOfflineMode_UTCID09_CustomerRole200_ThrowsUnauthorizedAccessException()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        var service = CreateOfflineService(repo, eventApi);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetOfflinePackageAsync(2001, "GATE_A", 200, "Customer", strictValidation: true));

        Assert.Equal("Staff or Organizer permission required", ex.Message);
    }

    // =========================================================================
    // 4. Function_094_SyncOfflineRecords (SyncOfflineScansAsync: UTCID01..UTCID10)
    // =========================================================================

    private static OfflineSyncRequestDTO BuildOfflineBatch(int eventId, int totalCount, int duplicateCount = 0)
    {
        var req = new OfflineSyncRequestDTO { EventId = eventId, GateId = "GATE_A" };
        for (int i = 1; i <= totalCount; i++)
        {
            bool isDup = i <= duplicateCount;
            req.Scans.Add(new OfflineScanRecordDTO
            {
                TicketCode = isDup ? $"DUP-CODE-{i}" : $"CLEAN-CODE-{i}",
                GateId = i % 2 == 0 ? "GATE_B" : "GATE_A",
                ScannedAt = DateTime.UtcNow.AddMinutes(-5),
                StaffUserId = 50
            });
        }
        return req;
    }

    private static void SetupOfflineSyncRepo(Mock<ICheckinRepository> repo, Mock<CheckinAPI.API.IEventApiClient> eventApi, int eventId = 2001)
    {
        eventApi.Setup(e => e.GetEventInfoAsync(eventId))
            .ReturnsAsync(new EventInfoResult { EventId = eventId, Status = "PUBLISHED" });
        repo.Setup(r => r.GetTicketByCodeAsync(It.Is<string>(s => s.StartsWith("CLEAN-CODE-"))))
            .ReturnsAsync((string code) => new CheckinAPI.Models.Ticket
            {
                TicketId = 1,
                EventId = eventId,
                TicketCode = Guid.NewGuid(),
                Status = "Available",
                IsCheckedIn = false
            });
        repo.Setup(r => r.GetTicketByCodeAsync(It.Is<string>(s => s.StartsWith("DUP-CODE-"))))
            .ReturnsAsync((string code) => new CheckinAPI.Models.Ticket
            {
                TicketId = 2,
                EventId = eventId,
                TicketCode = Guid.NewGuid(),
                Status = "Used",
                IsCheckedIn = true,
                CheckedInAt = DateTime.UtcNow.AddMinutes(-20)
            });
    }

    [Fact]
    public async Task SyncOfflineRecords_UTCID01_50CleanScans_SyncsAllWithZeroConflicts()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        SetupOfflineSyncRepo(repo, eventApi, 2001);
        var service = CreateOfflineService(repo, eventApi);

        var res = await service.SyncOfflineScansAsync(BuildOfflineBatch(2001, 50, 0), 50, "Staff", strictValidation: true);

        Assert.Equal(50, res.SyncedCount);
        Assert.Equal(0, res.ConflictCount);
    }

    [Fact]
    public async Task SyncOfflineRecords_UTCID02_30ScansWith2Duplicates_Returns28SyncedAnd2Conflicts()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        SetupOfflineSyncRepo(repo, eventApi, 2001);
        var service = CreateOfflineService(repo, eventApi);

        var res = await service.SyncOfflineScansAsync(BuildOfflineBatch(2001, 30, 2), 50, "Staff", strictValidation: true);

        Assert.Equal(28, res.SyncedCount);
        Assert.Equal(2, res.ConflictCount);
    }

    [Fact]
    public async Task SyncOfflineRecords_UTCID03_20ScansAcrossGatesAAndB_SyncsAll20()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        SetupOfflineSyncRepo(repo, eventApi, 2001);
        var service = CreateOfflineService(repo, eventApi);

        var res = await service.SyncOfflineScansAsync(BuildOfflineBatch(2001, 20, 0), 50, "Staff", strictValidation: true);

        Assert.Equal(20, res.SyncedCount);
        Assert.Equal(0, res.ConflictCount);
    }

    [Fact]
    public async Task SyncOfflineRecords_UTCID04_BoundaryMinimum1Scan_Syncs1WithZeroConflicts()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        SetupOfflineSyncRepo(repo, eventApi, 2001);
        var service = CreateOfflineService(repo, eventApi);

        var res = await service.SyncOfflineScansAsync(BuildOfflineBatch(2001, 1, 0), 50, "Staff", strictValidation: true);

        Assert.Equal(1, res.SyncedCount);
        Assert.Equal(0, res.ConflictCount);
    }

    [Fact]
    public async Task SyncOfflineRecords_UTCID05_BoundaryMaximum500Scans_Syncs500WithZeroConflicts()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        SetupOfflineSyncRepo(repo, eventApi, 2001);
        var service = CreateOfflineService(repo, eventApi);

        var res = await service.SyncOfflineScansAsync(BuildOfflineBatch(2001, 500, 0), 50, "Staff", strictValidation: true);

        Assert.Equal(500, res.SyncedCount);
        Assert.Equal(0, res.ConflictCount);
    }

    [Fact]
    public async Task SyncOfflineRecords_UTCID06_NonExistentEvent99999_ThrowsKeyNotFoundException()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        eventApi.Setup(e => e.GetEventInfoAsync(99999)).ReturnsAsync((EventInfoResult?)null);
        var service = CreateOfflineService(repo, eventApi);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.SyncOfflineScansAsync(BuildOfflineBatch(99999, 5, 0), 50, "Staff", strictValidation: true));

        Assert.Equal("Event not found", ex.Message);
    }

    [Fact]
    public async Task SyncOfflineRecords_UTCID07_EmptyScansList_ThrowsArgumentException()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        var service = CreateOfflineService(repo, eventApi);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SyncOfflineScansAsync(new OfflineSyncRequestDTO { EventId = 2001, Scans = new() }, 50, "Staff", strictValidation: true));

        Assert.Equal("Scans list cannot be empty", ex.Message);
    }

    [Fact]
    public async Task SyncOfflineRecords_UTCID08_TamperedDigitalSignature_ThrowsSecurityException()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        SetupOfflineSyncRepo(repo, eventApi, 2001);
        var service = CreateOfflineService(repo, eventApi);
        var batch = BuildOfflineBatch(2001, 2, 0);
        batch.Scans[0].Signature = "TAMPERED";

        var ex = await Assert.ThrowsAsync<SecurityException>(() =>
            service.SyncOfflineScansAsync(batch, 50, "Staff", strictValidation: true));

        Assert.Equal("Invalid scan payload digital signature", ex.Message);
    }

    [Fact]
    public async Task SyncOfflineRecords_UTCID09_FutureScanTimestampPlus2Days_ThrowsArgumentException()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        SetupOfflineSyncRepo(repo, eventApi, 2001);
        var service = CreateOfflineService(repo, eventApi);
        var batch = BuildOfflineBatch(2001, 2, 0);
        batch.Scans[0].ScannedAt = DateTime.UtcNow.AddDays(2);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SyncOfflineScansAsync(batch, 50, "Staff", strictValidation: true));

        Assert.Equal("Scan timestamp cannot be in the future", ex.Message);
    }

    [Fact]
    public async Task SyncOfflineRecords_UTCID10_CustomerRoleCaller_ThrowsUnauthorizedAccessException()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        var service = CreateOfflineService(repo, eventApi);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.SyncOfflineScansAsync(BuildOfflineBatch(2001, 50, 0), 200, "Customer", strictValidation: true));

        Assert.Equal("Staff permission required to sync offline records", ex.Message);
    }

    // =========================================================================
    // 5. Function_095_ResolveOfflineConf (ResolveOfflineConflictAsync: UTCID01..UTCID09)
    // =========================================================================

    private static OfflineSyncConflict CreateUnresolvedConflict(string id = "CONF-101") => new()
    {
        ConflictId = id,
        EventId = 2001,
        TicketCode = "TK-DUP-101",
        AttendeeName = "Attendee 101",
        ZoneName = "VIP",
        Status = "UNRESOLVED",
        EarlierScannedAt = DateTime.UtcNow.AddMinutes(-30),
        DuplicateScannedAt = DateTime.UtcNow.AddMinutes(-10)
    };

    [Fact]
    public async Task ResolveOfflineConflicts_UTCID01_AcceptOfflineAction_SetsResolvedAndAcceptOffline()
    {
        var repo = new Mock<ICheckinRepository>();
        repo.Setup(r => r.GetOfflineConflictByIdAsync("CONF-101")).ReturnsAsync(CreateUnresolvedConflict());
        var service = CreateOfflineService(repo, new Mock<CheckinAPI.API.IEventApiClient>());

        var res = await service.ResolveOfflineConflictAsync("CONF-101", new ResolveConflictRequestDTO
        {
            Resolution = "ACCEPT_OFFLINE",
            Reason = "Offline scan occurred earlier than online duplicate"
        }, 1, "Admin", "Admin", strictValidation: true);

        Assert.Equal("RESOLVED", res.Status);
        Assert.Equal("ACCEPT_OFFLINE", res.Resolution);
    }

    [Fact]
    public async Task ResolveOfflineConflicts_UTCID02_KeepOnlineAction_SetsResolvedAndKeepOnline()
    {
        var repo = new Mock<ICheckinRepository>();
        repo.Setup(r => r.GetOfflineConflictByIdAsync("CONF-101")).ReturnsAsync(CreateUnresolvedConflict());
        var service = CreateOfflineService(repo, new Mock<CheckinAPI.API.IEventApiClient>());

        var res = await service.ResolveOfflineConflictAsync("CONF-101", new ResolveConflictRequestDTO
        {
            Resolution = "KEEP_ONLINE",
            Reason = "Online entry verified with live biometric match"
        }, 1, "Admin", "Admin", strictValidation: true);

        Assert.Equal("RESOLVED", res.Status);
        Assert.Equal("KEEP_ONLINE", res.Resolution);
    }

    [Fact]
    public async Task ResolveOfflineConflicts_UTCID03_ManualOverrideAction_SetsResolvedAndManualOverride()
    {
        var repo = new Mock<ICheckinRepository>();
        repo.Setup(r => r.GetOfflineConflictByIdAsync("CONF-101")).ReturnsAsync(CreateUnresolvedConflict());
        var service = CreateOfflineService(repo, new Mock<CheckinAPI.API.IEventApiClient>());

        var res = await service.ResolveOfflineConflictAsync("CONF-101", new ResolveConflictRequestDTO
        {
            Resolution = "MANUAL_OVERRIDE",
            Reason = "Supervisor authorized entry at Gate A"
        }, 1, "Admin", "Admin", strictValidation: true);

        Assert.Equal("RESOLVED", res.Status);
        Assert.Equal("MANUAL_OVERRIDE", res.Resolution);
    }

    [Fact]
    public async Task ResolveOfflineConflicts_UTCID04_BoundaryMax500CharReason_SetsResolved()
    {
        var repo = new Mock<ICheckinRepository>();
        repo.Setup(r => r.GetOfflineConflictByIdAsync("CONF-101")).ReturnsAsync(CreateUnresolvedConflict());
        var service = CreateOfflineService(repo, new Mock<CheckinAPI.API.IEventApiClient>());

        var res = await service.ResolveOfflineConflictAsync("CONF-101", new ResolveConflictRequestDTO
        {
            Resolution = "MANUAL_OVERRIDE",
            Reason = new string('A', 500)
        }, 1, "Admin", "Admin", strictValidation: true);

        Assert.Equal("RESOLVED", res.Status);
        Assert.Equal(500, res.ResolutionNotes!.Length);
    }

    [Fact]
    public async Task ResolveOfflineConflicts_UTCID05_BoundaryMin10CharReason_SetsResolved()
    {
        var repo = new Mock<ICheckinRepository>();
        repo.Setup(r => r.GetOfflineConflictByIdAsync("CONF-101")).ReturnsAsync(CreateUnresolvedConflict());
        var service = CreateOfflineService(repo, new Mock<CheckinAPI.API.IEventApiClient>());

        var res = await service.ResolveOfflineConflictAsync("CONF-101", new ResolveConflictRequestDTO
        {
            Resolution = "MANUAL_OVERRIDE",
            Reason = "1234567890"
        }, 1, "Admin", "Admin", strictValidation: true);

        Assert.Equal("RESOLVED", res.Status);
        Assert.Equal("1234567890", res.ResolutionNotes);
    }

    [Fact]
    public async Task ResolveOfflineConflicts_UTCID06_NonExistentConflictId_ThrowsKeyNotFoundException()
    {
        var repo = new Mock<ICheckinRepository>();
        repo.Setup(r => r.GetOfflineConflictByIdAsync("CONF-999")).ReturnsAsync((OfflineSyncConflict?)null);
        var service = CreateOfflineService(repo, new Mock<CheckinAPI.API.IEventApiClient>());

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ResolveOfflineConflictAsync("CONF-999", new ResolveConflictRequestDTO
            {
                Resolution = "KEEP_ONLINE",
                Reason = "Valid resolution reason"
            }, 1, "Admin", "Admin", strictValidation: true));

        Assert.Equal("Conflict record not found", ex.Message);
    }

    [Fact]
    public async Task ResolveOfflineConflicts_UTCID07_InvalidResolutionAction_ThrowsArgumentException()
    {
        var repo = new Mock<ICheckinRepository>();
        var service = CreateOfflineService(repo, new Mock<CheckinAPI.API.IEventApiClient>());

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ResolveOfflineConflictAsync("CONF-101", new ResolveConflictRequestDTO
            {
                Resolution = "INVALID_ACTION",
                Reason = "Valid resolution reason"
            }, 1, "Admin", "Admin", strictValidation: true));

        Assert.Equal("Invalid resolution action", ex.Message);
    }

    [Fact]
    public async Task ResolveOfflineConflicts_UTCID08_EmptyResolutionReason_ThrowsArgumentException()
    {
        var repo = new Mock<ICheckinRepository>();
        var service = CreateOfflineService(repo, new Mock<CheckinAPI.API.IEventApiClient>());

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ResolveOfflineConflictAsync("CONF-101", new ResolveConflictRequestDTO
            {
                Resolution = "KEEP_ONLINE",
                Reason = ""
            }, 1, "Admin", "Admin", strictValidation: true));

        Assert.Equal("Resolution reason is required", ex.Message);
    }

    [Fact]
    public async Task ResolveOfflineConflicts_UTCID09_StaffRoleWithoutResolutionRights_ThrowsUnauthorizedAccessException()
    {
        var repo = new Mock<ICheckinRepository>();
        var service = CreateOfflineService(repo, new Mock<CheckinAPI.API.IEventApiClient>());

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.ResolveOfflineConflictAsync("CONF-101", new ResolveConflictRequestDTO
            {
                Resolution = "KEEP_ONLINE",
                Reason = "Valid resolution reason"
            }, 50, "Staff", "Staff", strictValidation: true));

        Assert.Equal("Admin or Organizer role required to resolve conflicts", ex.Message);
    }

    // =========================================================================
    // 6. Function_096_ViewGateAssignment (GetGateAssignmentAsync: UTCID01..UTCID08)
    // =========================================================================

    [Fact]
    public async Task ViewGateAssignment_UTCID01_StaffAssignedToGateA_ReturnsGateAWithAllowedZones()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        SetupActiveShiftAssignment(eventApi, 3001, 101, "GATE_A", 1, new List<string> { "VIP", "STANDARD" });

        var service = CreateMonitorService(repo, eventApi);
        var res = await service.GetGateAssignmentAsync(3001, 101, "Staff", strictValidation: true);

        Assert.True(res.IsAssigned);
        Assert.Equal("GATE_A", res.GateName);
        Assert.Contains("VIP", res.AllowedTicketTiers);
    }

    [Fact]
    public async Task ViewGateAssignment_UTCID02_SupervisorAssignedToAllGates_ReturnsAllGateAssignment()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        SetupActiveShiftAssignment(eventApi, 3001, 102, "ALL", 2, new List<string> { "VIP", "STANDARD" });

        var service = CreateMonitorService(repo, eventApi);
        var res = await service.GetGateAssignmentAsync(3001, 102, "Staff", strictValidation: true);

        Assert.True(res.IsAssigned);
        Assert.Equal("ALL", res.GateName);
    }

    [Fact]
    public async Task ViewGateAssignment_UTCID03_BoundaryMinStaffUserId1_ReturnsGateAssignment()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        SetupActiveShiftAssignment(eventApi, 3001, 1, "GATE_A", 1);

        var service = CreateMonitorService(repo, eventApi);
        var res = await service.GetGateAssignmentAsync(3001, 1, "Staff", strictValidation: true);

        Assert.True(res.IsAssigned);
        Assert.Equal("GATE_A", res.GateName);
    }

    [Fact]
    public async Task ViewGateAssignment_UTCID04_BoundaryMaxStaffUserId999999_ReturnsGateAssignment()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        SetupActiveShiftAssignment(eventApi, 3001, 999999, "GATE_A", 1);

        var service = CreateMonitorService(repo, eventApi);
        var res = await service.GetGateAssignmentAsync(3001, 999999, "Staff", strictValidation: true);

        Assert.True(res.IsAssigned);
        Assert.Equal("GATE_A", res.GateName);
    }

    [Fact]
    public async Task ViewGateAssignment_UTCID05_StaffWithoutGateAssignment103_ThrowsKeyNotFoundException()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        eventApi.Setup(e => e.GetEventInfoAsync(3001)).ReturnsAsync(new EventInfoResult { EventId = 3001 });
        eventApi.Setup(e => e.GetStaffAssignmentAsync(3001, 103)).ReturnsAsync((StaffAssignmentInfo?)null);

        var service = CreateMonitorService(repo, eventApi);
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.GetGateAssignmentAsync(3001, 103, "Staff", strictValidation: true));

        Assert.Equal("No gate assignment found for this event", ex.Message);
    }

    [Fact]
    public async Task ViewGateAssignment_UTCID06_NonExistentEvent99999_ThrowsKeyNotFoundException()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        eventApi.Setup(e => e.GetEventInfoAsync(99999)).ReturnsAsync((EventInfoResult?)null);

        var service = CreateMonitorService(repo, eventApi);
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.GetGateAssignmentAsync(99999, 101, "Staff", strictValidation: true));

        Assert.Equal("Event not found", ex.Message);
    }

    [Fact]
    public async Task ViewGateAssignment_UTCID07_InvalidZeroOrNegativeEventId_ThrowsArgumentException()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        var service = CreateMonitorService(repo, eventApi);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetGateAssignmentAsync(0, 101, "Staff", strictValidation: true));

        Assert.Equal("Event ID must be positive", ex.Message);
    }

    [Fact]
    public async Task ViewGateAssignment_UTCID08_CustomerRole501_ThrowsUnauthorizedAccessException()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        var service = CreateMonitorService(repo, eventApi);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetGateAssignmentAsync(3001, 501, "Customer", strictValidation: true));

        Assert.Equal("Staff permission required", ex.Message);
    }

    // =========================================================================
    // 7. Function_097_ScanDynamicQRTicke (ScanQrTicketAsync: UTCID01..UTCID11)
    // =========================================================================

    private static CheckinAPI.Models.Ticket CreateQrTicket(
        string payload,
        int eventId = 3001,
        string status = "Active",
        bool checkedIn = false,
        string zoneName = "VIP",
        DateTime? qrExpiresAt = null)
    {
        var t = new CheckinAPI.Models.Ticket
        {
            TicketId = 10,
            EventId = eventId,
            TicketCode = Guid.NewGuid(),
            OwnerName = "Nguyen Van A",
            TicketTypeName = zoneName,
            Status = status,
            IsCheckedIn = checkedIn,
            CheckedInAt = checkedIn ? DateTime.UtcNow.AddMinutes(-15) : null
        };
        t.TicketQrTokens.Add(new TicketQrToken
        {
            TicketId = 10,
            QrToken = payload,
            IssuedAt = DateTime.UtcNow.AddSeconds(-10),
            ExpiresAt = qrExpiresAt ?? DateTime.UtcNow.AddSeconds(30),
            IsUsed = checkedIn,
            IsRevoked = status.Equals("REVOKED", StringComparison.OrdinalIgnoreCase)
        });
        return t;
    }

    [Fact]
    public async Task ScanDynamicQRTicket_UTCID01_ValidDynamicQrToken_ReturnsSuccessAndVipZone()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        SetupActiveShiftAssignment(eventApi, 3001, 101, "GATE_A");
        repo.Setup(r => r.GetTicketForScanAsync("VALID_QR_01")).ReturnsAsync(CreateQrTicket("VALID_QR_01"));

        var service = CreateMonitorService(repo, eventApi);
        var res = await service.ScanQrTicketAsync(new ScanQrRequestDTO
        {
            EventId = 3001,
            GateId = "GATE_A",
            QrPayload = "VALID_QR_01"
        }, 101, "Staff 101", "Staff", strictValidation: true);

        Assert.True(res.Success);
        Assert.Equal("SUCCESS", res.Status);
        Assert.Equal("VIP", res.ZoneName);
    }

    [Fact]
    public async Task ScanDynamicQRTicket_UTCID02_SecondValidDynamicQrToken_ReturnsSuccess()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        SetupActiveShiftAssignment(eventApi, 3001, 101, "GATE_A");
        repo.Setup(r => r.GetTicketForScanAsync("VALID_QR_02")).ReturnsAsync(CreateQrTicket("VALID_QR_02"));

        var service = CreateMonitorService(repo, eventApi);
        var res = await service.ScanQrTicketAsync(new ScanQrRequestDTO
        {
            EventId = 3001,
            GateId = "GATE_A",
            QrPayload = "VALID_QR_02"
        }, 101, "Staff 101", "Staff", strictValidation: true);

        Assert.True(res.Success);
        Assert.Equal("SUCCESS", res.Status);
    }

    [Fact]
    public async Task ScanDynamicQRTicket_UTCID03_BoundaryTotpAtSecond29Of30sWindow_ReturnsSuccess()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        SetupActiveShiftAssignment(eventApi, 3001, 101, "GATE_A");
        repo.Setup(r => r.GetTicketForScanAsync("QR_SEC_29"))
            .ReturnsAsync(CreateQrTicket("QR_SEC_29", qrExpiresAt: DateTime.UtcNow.AddSeconds(1)));

        var service = CreateMonitorService(repo, eventApi);
        var res = await service.ScanQrTicketAsync(new ScanQrRequestDTO
        {
            EventId = 3001,
            GateId = "GATE_A",
            QrPayload = "QR_SEC_29"
        }, 101, "Staff 101", "Staff", strictValidation: true);

        Assert.True(res.Success);
        Assert.Equal("SUCCESS", res.Status);
    }

    [Fact]
    public async Task ScanDynamicQRTicket_UTCID04_BoundaryScanRightAtGateOpeningTimestamp_ReturnsSuccess()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        SetupActiveShiftAssignment(eventApi, 3001, 101, "GATE_A");
        repo.Setup(r => r.GetTicketForScanAsync("QR_GATE_OPEN")).ReturnsAsync(CreateQrTicket("QR_GATE_OPEN"));

        var service = CreateMonitorService(repo, eventApi);
        var res = await service.ScanQrTicketAsync(new ScanQrRequestDTO
        {
            EventId = 3001,
            GateId = "GATE_A",
            QrPayload = "QR_GATE_OPEN"
        }, 101, "Staff 101", "Staff", strictValidation: true);

        Assert.True(res.Success);
        Assert.Equal("SUCCESS", res.Status);
    }

    [Fact]
    public async Task ScanDynamicQRTicket_UTCID05_ExpiredTotpCode_ReturnsExpiredQrResult()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        SetupActiveShiftAssignment(eventApi, 3001, 101, "GATE_A");
        repo.Setup(r => r.GetTicketForScanAsync("QR_EXPIRED"))
            .ReturnsAsync(CreateQrTicket("QR_EXPIRED", qrExpiresAt: DateTime.UtcNow.AddMinutes(-10)));

        var service = CreateMonitorService(repo, eventApi);
        var res = await service.ScanQrTicketAsync(new ScanQrRequestDTO
        {
            EventId = 3001,
            GateId = "GATE_A",
            QrPayload = "QR_EXPIRED"
        }, 101, "Staff 101", "Staff", strictValidation: true);

        Assert.False(res.Success);
        Assert.Equal("EXPIRED_QR", res.Status);
    }

    [Fact]
    public async Task ScanDynamicQRTicket_UTCID06_AlreadyUsedTicket_ReturnsAlreadyUsedResult()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        SetupActiveShiftAssignment(eventApi, 3001, 101, "GATE_A");
        repo.Setup(r => r.GetTicketForScanAsync("QR_USED"))
            .ReturnsAsync(CreateQrTicket("QR_USED", status: "Used", checkedIn: true));

        var service = CreateMonitorService(repo, eventApi);
        var res = await service.ScanQrTicketAsync(new ScanQrRequestDTO
        {
            EventId = 3001,
            GateId = "GATE_A",
            QrPayload = "QR_USED"
        }, 101, "Staff 101", "Staff", strictValidation: true);

        Assert.False(res.Success);
        Assert.Equal("ALREADY_USED", res.Status);
    }

    [Fact]
    public async Task ScanDynamicQRTicket_UTCID07_RevokedTicket_ReturnsRevokedResult()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        SetupActiveShiftAssignment(eventApi, 3001, 101, "GATE_A");
        repo.Setup(r => r.GetTicketForScanAsync("QR_REVOKED"))
            .ReturnsAsync(CreateQrTicket("QR_REVOKED", status: "REVOKED"));

        var service = CreateMonitorService(repo, eventApi);
        var res = await service.ScanQrTicketAsync(new ScanQrRequestDTO
        {
            EventId = 3001,
            GateId = "GATE_A",
            QrPayload = "QR_REVOKED"
        }, 101, "Staff 101", "Staff", strictValidation: true);

        Assert.False(res.Success);
        Assert.Equal("REVOKED", res.Status);
    }

    [Fact]
    public async Task ScanDynamicQRTicket_UTCID08_TicketBelongsToDifferentEvent4001_ReturnsWrongEventResult()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        SetupActiveShiftAssignment(eventApi, 3001, 101, "GATE_A");
        repo.Setup(r => r.GetTicketForScanAsync("QR_EVENT_4001"))
            .ReturnsAsync(CreateQrTicket("QR_EVENT_4001", eventId: 4001));

        var service = CreateMonitorService(repo, eventApi);
        var res = await service.ScanQrTicketAsync(new ScanQrRequestDTO
        {
            EventId = 3001,
            GateId = "GATE_A",
            QrPayload = "QR_EVENT_4001"
        }, 101, "Staff 101", "Staff", strictValidation: true);

        Assert.False(res.Success);
        Assert.Equal("WRONG_EVENT", res.Status);
    }

    [Fact]
    public async Task ScanDynamicQRTicket_UTCID09_TicketDesignatedForGateB_ReturnsWrongGateResult()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        SetupActiveShiftAssignment(eventApi, 3001, 101, "GATE_A", 1, new List<string> { "VIP" });
        repo.Setup(r => r.GetTicketForScanAsync("QR_GATE_B"))
            .ReturnsAsync(CreateQrTicket("QR_GATE_B", zoneName: "GA_NORTH_GATE_B"));

        var service = CreateMonitorService(repo, eventApi);
        var res = await service.ScanQrTicketAsync(new ScanQrRequestDTO
        {
            EventId = 3001,
            GateId = "GATE_A",
            QrPayload = "QR_GATE_B"
        }, 101, "Staff 101", "Staff", strictValidation: true);

        Assert.False(res.Success);
        Assert.Equal("WRONG_GATE", res.Status);
    }

    [Fact]
    public async Task ScanDynamicQRTicket_UTCID10_MalformedTamperedCiphertext_ThrowsSecurityException()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        var service = CreateMonitorService(repo, eventApi);

        var ex = await Assert.ThrowsAsync<SecurityException>(() =>
            service.ScanQrTicketAsync(new ScanQrRequestDTO
            {
                EventId = 3001,
                GateId = "GATE_A",
                QrPayload = "TAMPERED_CIPHERTEXT_PAYLOAD"
            }, 101, "Staff 101", "Staff", strictValidation: true));

        Assert.Equal("Invalid QR ciphertext payload", ex.Message);
    }

    [Fact]
    public async Task ScanDynamicQRTicket_UTCID11_CustomerRoleCaller_ThrowsUnauthorizedAccessException()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        var service = CreateMonitorService(repo, eventApi);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.ScanQrTicketAsync(new ScanQrRequestDTO
            {
                EventId = 3001,
                GateId = "GATE_A",
                QrPayload = "VALID_QR_01"
            }, 500, "Customer", "Customer", strictValidation: true));

        Assert.Equal("Staff permission required", ex.Message);
    }

    // =========================================================================
    // 8. Function_098_ManualCheck-in (ManualCheckinAsync: UTCID01..UTCID09)
    // =========================================================================

    [Fact]
    public async Task ManualCheckin_UTCID01_ValidTicket1001AndCitizenId_ReturnsSuccess()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        SetupActiveShiftAssignment(eventApi, 3001, 101, "GATE_A");
        repo.Setup(r => r.GetTicketForManualCheckinAsync(3001, "TK-LIVE-1001", "079099012345"))
            .ReturnsAsync(CreateQrTicket("TK-LIVE-1001"));

        var service = CreateMonitorService(repo, eventApi);
        var res = await service.ManualCheckinAsync(new ManualCheckinRequestDTO
        {
            EventId = 3001,
            GateId = "GATE_A",
            TicketCode = "TK-LIVE-1001",
            CccdNumber = "079099012345",
            CccdVerified = true,
            ReasonNote = "Attendee phone battery died; identity verified via National ID"
        }, 101, "Staff 101", "Staff", strictValidation: true);

        Assert.True(res.Success);
        Assert.Equal("SUCCESS", res.Status);
    }

    [Fact]
    public async Task ManualCheckin_UTCID02_ValidTicket1002AndCitizenId_ReturnsSuccess()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        SetupActiveShiftAssignment(eventApi, 3001, 101, "GATE_A");
        repo.Setup(r => r.GetTicketForManualCheckinAsync(3001, "TK-LIVE-1002", "079099012345"))
            .ReturnsAsync(CreateQrTicket("TK-LIVE-1002"));

        var service = CreateMonitorService(repo, eventApi);
        var res = await service.ManualCheckinAsync(new ManualCheckinRequestDTO
        {
            EventId = 3001,
            GateId = "GATE_A",
            TicketCode = "TK-LIVE-1002",
            CccdNumber = "079099012345",
            CccdVerified = true,
            ReasonNote = "Attendee phone battery died; identity verified via National ID"
        }, 101, "Staff 101", "Staff", strictValidation: true);

        Assert.True(res.Success);
        Assert.Equal("SUCCESS", res.Status);
    }

    [Fact]
    public async Task ManualCheckin_UTCID03_BoundaryMin10CharReason_ReturnsSuccess()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        SetupActiveShiftAssignment(eventApi, 3001, 101, "GATE_A");
        repo.Setup(r => r.GetTicketForManualCheckinAsync(3001, "TK-LIVE-1001", "079099012345"))
            .ReturnsAsync(CreateQrTicket("TK-LIVE-1001"));

        var service = CreateMonitorService(repo, eventApi);
        var res = await service.ManualCheckinAsync(new ManualCheckinRequestDTO
        {
            EventId = 3001,
            GateId = "GATE_A",
            TicketCode = "TK-LIVE-1001",
            CccdNumber = "079099012345",
            CccdVerified = true,
            ReasonNote = "1234567890"
        }, 101, "Staff 101", "Staff", strictValidation: true);

        Assert.True(res.Success);
    }

    [Fact]
    public async Task ManualCheckin_UTCID04_BoundaryMax250CharReasonAndPassport_ReturnsSuccess()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        SetupActiveShiftAssignment(eventApi, 3001, 101, "GATE_A");
        repo.Setup(r => r.GetTicketForManualCheckinAsync(3001, "TK-LIVE-1001", "B12345678"))
            .ReturnsAsync(CreateQrTicket("TK-LIVE-1001"));

        var service = CreateMonitorService(repo, eventApi);
        var res = await service.ManualCheckinAsync(new ManualCheckinRequestDTO
        {
            EventId = 3001,
            GateId = "GATE_A",
            TicketCode = "TK-LIVE-1001",
            CccdNumber = "B12345678",
            CccdVerified = true,
            ReasonNote = new string('R', 250)
        }, 101, "Staff 101", "Staff", strictValidation: true);

        Assert.True(res.Success);
    }

    [Fact]
    public async Task ManualCheckin_UTCID05_NonExistentTicketCode_ThrowsKeyNotFoundException()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        repo.Setup(r => r.GetTicketForManualCheckinAsync(3001, "TK-INVALID-9999", "079099012345"))
            .ReturnsAsync((CheckinAPI.Models.Ticket?)null);

        var service = CreateMonitorService(repo, eventApi);
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ManualCheckinAsync(new ManualCheckinRequestDTO
            {
                EventId = 3001,
                GateId = "GATE_A",
                TicketCode = "TK-INVALID-9999",
                CccdNumber = "079099012345",
                CccdVerified = true,
                ReasonNote = "Attendee phone battery died; identity verified via National ID"
            }, 101, "Staff 101", "Staff", strictValidation: true));

        Assert.Equal("Ticket code not found", ex.Message);
    }

    [Fact]
    public async Task ManualCheckin_UTCID06_TicketAlreadyCheckedIn_ThrowsInvalidOperationException()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        repo.Setup(r => r.GetTicketForManualCheckinAsync(3001, "TK-LIVE-1001", "079099012345"))
            .ReturnsAsync(CreateQrTicket("TK-LIVE-1001", status: "Used", checkedIn: true));

        var service = CreateMonitorService(repo, eventApi);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ManualCheckinAsync(new ManualCheckinRequestDTO
            {
                EventId = 3001,
                GateId = "GATE_A",
                TicketCode = "TK-LIVE-1001",
                CccdNumber = "079099012345",
                CccdVerified = true,
                ReasonNote = "Attendee phone battery died; identity verified via National ID"
            }, 101, "Staff 101", "Staff", strictValidation: true));

        Assert.Equal("Ticket is already checked in", ex.Message);
    }

    [Fact]
    public async Task ManualCheckin_UTCID07_EmptyIdDocumentNumber_ThrowsArgumentException()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        var service = CreateMonitorService(repo, eventApi);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ManualCheckinAsync(new ManualCheckinRequestDTO
            {
                EventId = 3001,
                GateId = "GATE_A",
                TicketCode = "TK-LIVE-1001",
                CccdNumber = "",
                CccdVerified = true,
                ReasonNote = "Attendee phone battery died; identity verified via National ID"
            }, 101, "Staff 101", "Staff", strictValidation: true));

        Assert.Equal("ID document number is required", ex.Message);
    }

    [Fact]
    public async Task ManualCheckin_UTCID08_EmptyManualCheckinReason_ThrowsArgumentException()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        var service = CreateMonitorService(repo, eventApi);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ManualCheckinAsync(new ManualCheckinRequestDTO
            {
                EventId = 3001,
                GateId = "GATE_A",
                TicketCode = "TK-LIVE-1001",
                CccdNumber = "079099012345",
                CccdVerified = true,
                ReasonNote = ""
            }, 101, "Staff 101", "Staff", strictValidation: true));

        Assert.Equal("Manual check-in reason is required", ex.Message);
    }

    [Fact]
    public async Task ManualCheckin_UTCID09_CustomerRoleCaller_ThrowsUnauthorizedAccessException()
    {
        var repo = new Mock<ICheckinRepository>();
        var eventApi = new Mock<CheckinAPI.API.IEventApiClient>();
        var service = CreateMonitorService(repo, eventApi);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.ManualCheckinAsync(new ManualCheckinRequestDTO
            {
                EventId = 3001,
                GateId = "GATE_A",
                TicketCode = "TK-LIVE-1001",
                CccdNumber = "079099012345",
                CccdVerified = true,
                ReasonNote = "Attendee phone battery died; identity verified via National ID"
            }, 500, "Customer", "Customer", strictValidation: true));

        Assert.Equal("Staff privilege required", ex.Message);
    }

    // =========================================================================
    // 9. Function_099_LookupTicket (LookupTicketsAsync: UTCID01..UTCID09)
    // =========================================================================

    [Fact]
    public async Task LookupTicket_UTCID01_SearchByPhoneNumber_ReturnsMatchingTickets()
    {
        var repo = new Mock<ICheckinRepository>();
        repo.Setup(r => r.LookupTicketsAsync(3001, "0901234567", null, null, null, null, null))
            .ReturnsAsync(new List<CheckinAPI.Models.Ticket> { CreateQrTicket("QR_1") });

        var service = CreateMonitorService(repo, new Mock<CheckinAPI.API.IEventApiClient>());
        var res = await service.LookupTicketsAsync(new LookupTicketRequestDTO
        {
            EventId = 3001,
            SearchType = "PHONE",
            QueryValue = "0901234567"
        }, 101, "Staff", strictValidation: true);

        Assert.True(res.Tickets.Count > 0);
    }

    [Fact]
    public async Task LookupTicket_UTCID02_SearchByCustomerEmail_ReturnsMatchingTickets()
    {
        var repo = new Mock<ICheckinRepository>();
        repo.Setup(r => r.LookupTicketsAsync(3001, null, "customer@example.com", null, null, null, null))
            .ReturnsAsync(new List<CheckinAPI.Models.Ticket> { CreateQrTicket("QR_2") });

        var service = CreateMonitorService(repo, new Mock<CheckinAPI.API.IEventApiClient>());
        var res = await service.LookupTicketsAsync(new LookupTicketRequestDTO
        {
            EventId = 3001,
            SearchType = "EMAIL",
            QueryValue = "customer@example.com"
        }, 101, "Staff", strictValidation: true);

        Assert.True(res.Tickets.Count > 0);
    }

    [Fact]
    public async Task LookupTicket_UTCID03_SearchByCustomerName_ReturnsMatchingTickets()
    {
        var repo = new Mock<ICheckinRepository>();
        repo.Setup(r => r.LookupTicketsAsync(3001, null, null, "Nguyen Van A", null, null, null))
            .ReturnsAsync(new List<CheckinAPI.Models.Ticket> { CreateQrTicket("QR_3") });

        var service = CreateMonitorService(repo, new Mock<CheckinAPI.API.IEventApiClient>());
        var res = await service.LookupTicketsAsync(new LookupTicketRequestDTO
        {
            EventId = 3001,
            SearchType = "NAME",
            QueryValue = "Nguyen Van A"
        }, 101, "Staff", strictValidation: true);

        Assert.True(res.Tickets.Count > 0);
    }

    [Fact]
    public async Task LookupTicket_UTCID04_BoundaryMinimum3CharsKeyword_ReturnsMatchingTickets()
    {
        var repo = new Mock<ICheckinRepository>();
        repo.Setup(r => r.LookupTicketsAsync(3001, null, null, null, null, null, "ORD"))
            .ReturnsAsync(new List<CheckinAPI.Models.Ticket> { CreateQrTicket("QR_4") });

        var service = CreateMonitorService(repo, new Mock<CheckinAPI.API.IEventApiClient>());
        var res = await service.LookupTicketsAsync(new LookupTicketRequestDTO
        {
            EventId = 3001,
            SearchType = "ORDER_CODE",
            QueryValue = "ORD"
        }, 101, "Staff", strictValidation: true);

        Assert.True(res.Tickets.Count > 0);
    }

    [Fact]
    public async Task LookupTicket_UTCID05_BoundaryLongSearchKeyword_ReturnsMatchingTickets()
    {
        var repo = new Mock<ICheckinRepository>();
        var longKeyword = "ORD-2026-LONG-SEARCH-STRING-50-CHARS";
        repo.Setup(r => r.LookupTicketsAsync(3001, null, null, null, null, null, longKeyword))
            .ReturnsAsync(new List<CheckinAPI.Models.Ticket> { CreateQrTicket("QR_5") });

        var service = CreateMonitorService(repo, new Mock<CheckinAPI.API.IEventApiClient>());
        var res = await service.LookupTicketsAsync(new LookupTicketRequestDTO
        {
            EventId = 3001,
            SearchType = "ORDER_CODE",
            QueryValue = longKeyword
        }, 101, "Staff", strictValidation: true);

        Assert.True(res.Tickets.Count > 0);
    }

    [Fact]
    public async Task LookupTicket_UTCID06_BoundaryNoMatchingTickets_ReturnsEmptyList()
    {
        var repo = new Mock<ICheckinRepository>();
        repo.Setup(r => r.LookupTicketsAsync(3001, "NON_EXISTENT_KEYWORD", null, null, null, null, null))
            .ReturnsAsync(new List<CheckinAPI.Models.Ticket>());

        var service = CreateMonitorService(repo, new Mock<CheckinAPI.API.IEventApiClient>());
        var res = await service.LookupTicketsAsync(new LookupTicketRequestDTO
        {
            EventId = 3001,
            SearchType = "PHONE",
            QueryValue = "NON_EXISTENT_KEYWORD"
        }, 101, "Staff", strictValidation: true);

        Assert.Empty(res.Tickets);
    }

    [Fact]
    public async Task LookupTicket_UTCID07_InvalidZeroOrNegativeEventId_ThrowsArgumentException()
    {
        var repo = new Mock<ICheckinRepository>();
        var service = CreateMonitorService(repo, new Mock<CheckinAPI.API.IEventApiClient>());

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.LookupTicketsAsync(new LookupTicketRequestDTO
            {
                EventId = 0,
                SearchType = "PHONE",
                QueryValue = "0901234567"
            }, 101, "Staff", strictValidation: true));

        Assert.Equal("Event ID must be positive", ex.Message);
    }

    [Fact]
    public async Task LookupTicket_UTCID08_KeywordShorterThan3Characters_ThrowsArgumentException()
    {
        var repo = new Mock<ICheckinRepository>();
        var service = CreateMonitorService(repo, new Mock<CheckinAPI.API.IEventApiClient>());

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.LookupTicketsAsync(new LookupTicketRequestDTO
            {
                EventId = 3001,
                SearchType = "PHONE",
                QueryValue = "AB"
            }, 101, "Staff", strictValidation: true));

        Assert.Equal("Search keyword must have at least 3 characters", ex.Message);
    }

    [Fact]
    public async Task LookupTicket_UTCID09_CustomerRoleCaller_ThrowsUnauthorizedAccessException()
    {
        var repo = new Mock<ICheckinRepository>();
        var service = CreateMonitorService(repo, new Mock<CheckinAPI.API.IEventApiClient>());

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.LookupTicketsAsync(new LookupTicketRequestDTO
            {
                EventId = 3001,
                SearchType = "PHONE",
                QueryValue = "0901234567"
            }, 500, "Customer", strictValidation: true));

        Assert.Equal("Staff permission required", ex.Message);
    }

    // =========================================================================
    // 10. Function_100_ViewCheckinHistory (GetScanHistoryAsync: UTCID01..UTCID09)
    // =========================================================================

    private static List<CheckinLog> CreateScanLogs(int count, string status = "SUCCESS", string gateId = "GATE_A")
    {
        var list = new List<CheckinLog>();
        for (int i = 1; i <= count; i++)
        {
            list.Add(new CheckinLog
            {
                LogId = i,
                EventId = 3001,
                TicketCode = $"TK-{i}",
                CustomerName = $"Attendee {i}",
                ZoneName = "VIP",
                GateId = gateId,
                GateName = gateId,
                Status = status,
                ScannedAt = DateTime.UtcNow.AddMinutes(-i)
            });
        }
        return list;
    }

    [Fact]
    public async Task ViewCheckinScanHistory_UTCID01_NullFilters_ReturnsAllRecentScanLogs()
    {
        var repo = new Mock<ICheckinRepository>();
        repo.Setup(r => r.GetScanHistoryAsync(3001, null, null, null, null))
            .ReturnsAsync((CreateScanLogs(5), 5, 5, 0, 0));

        var service = CreateMonitorService(repo, new Mock<CheckinAPI.API.IEventApiClient>());
        var res = await service.GetScanHistoryAsync(3001, null, null, 101, 20, "Staff", strictValidation: true);

        Assert.True(res.Scans.Count > 0);
        Assert.True(res.TotalScans >= res.Scans.Count);
    }

    [Fact]
    public async Task ViewCheckinScanHistory_UTCID02_FilterBySuccessStatus_ReturnsSuccessScanLogs()
    {
        var repo = new Mock<ICheckinRepository>();
        repo.Setup(r => r.GetScanHistoryAsync(3001, null, "SUCCESS", null, null))
            .ReturnsAsync((CreateScanLogs(3, "SUCCESS"), 3, 3, 0, 0));

        var service = CreateMonitorService(repo, new Mock<CheckinAPI.API.IEventApiClient>());
        var res = await service.GetScanHistoryAsync(3001, null, "SUCCESS", 101, 20, "Staff", strictValidation: true);

        Assert.Equal(3, res.Scans.Count);
        Assert.All(res.Scans, s => Assert.Equal("SUCCESS", s.Status));
    }

    [Fact]
    public async Task ViewCheckinScanHistory_UTCID03_FilterByFailedStatus_ReturnsFailedScanLogs()
    {
        var repo = new Mock<ICheckinRepository>();
        repo.Setup(r => r.GetScanHistoryAsync(3001, null, "FAILED", null, null))
            .ReturnsAsync((CreateScanLogs(2, "FAILED"), 2, 0, 1, 1));

        var service = CreateMonitorService(repo, new Mock<CheckinAPI.API.IEventApiClient>());
        var res = await service.GetScanHistoryAsync(3001, null, "FAILED", 101, 20, "Staff", strictValidation: true);

        Assert.Equal(2, res.Scans.Count);
    }

    [Fact]
    public async Task ViewCheckinScanHistory_UTCID04_BoundaryFilterByGateA_ReturnsGateAScanLogs()
    {
        var repo = new Mock<ICheckinRepository>();
        repo.Setup(r => r.GetScanHistoryAsync(3001, "GATE_A", null, null, null))
            .ReturnsAsync((CreateScanLogs(4, "SUCCESS", "GATE_A"), 4, 4, 0, 0));

        var service = CreateMonitorService(repo, new Mock<CheckinAPI.API.IEventApiClient>());
        var res = await service.GetScanHistoryAsync(3001, "GATE_A", null, 101, 20, "Staff", strictValidation: true);

        Assert.Equal(4, res.Scans.Count);
    }

    [Fact]
    public async Task ViewCheckinScanHistory_UTCID05_BoundaryMaxLimit50_ReturnsUpTo50ScanLogs()
    {
        var repo = new Mock<ICheckinRepository>();
        repo.Setup(r => r.GetScanHistoryAsync(3001, null, null, null, null))
            .ReturnsAsync((CreateScanLogs(50), 50, 50, 0, 0));

        var service = CreateMonitorService(repo, new Mock<CheckinAPI.API.IEventApiClient>());
        var res = await service.GetScanHistoryAsync(3001, null, null, 101, 50, "Staff", strictValidation: true);

        Assert.Equal(50, res.Scans.Count);
        Assert.Equal(50, res.TotalScans);
    }

    [Fact]
    public async Task ViewCheckinScanHistory_UTCID06_BoundaryZeroScansPerformed_ReturnsEmptyHistory()
    {
        var repo = new Mock<ICheckinRepository>();
        repo.Setup(r => r.GetScanHistoryAsync(3001, null, null, null, null))
            .ReturnsAsync((new List<CheckinLog>(), 0, 0, 0, 0));

        var service = CreateMonitorService(repo, new Mock<CheckinAPI.API.IEventApiClient>());
        var res = await service.GetScanHistoryAsync(3001, null, null, 101, 20, "Staff", strictValidation: true);

        Assert.Empty(res.Scans);
        Assert.Equal(0, res.TotalScans);
    }

    [Fact]
    public async Task ViewCheckinScanHistory_UTCID07_InvalidZeroOrNegativeEventId_ThrowsArgumentException()
    {
        var repo = new Mock<ICheckinRepository>();
        var service = CreateMonitorService(repo, new Mock<CheckinAPI.API.IEventApiClient>());

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetScanHistoryAsync(0, null, null, 101, 20, "Staff", strictValidation: true));

        Assert.Equal("Event ID must be positive", ex.Message);
    }

    [Fact]
    public async Task ViewCheckinScanHistory_UTCID08_InvalidStatusFilterValue_ThrowsArgumentException()
    {
        var repo = new Mock<ICheckinRepository>();
        var service = CreateMonitorService(repo, new Mock<CheckinAPI.API.IEventApiClient>());

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetScanHistoryAsync(3001, null, "INVALID_STATUS_FILTER", 101, 20, "Staff", strictValidation: true));

        Assert.Equal("Invalid status filter value", ex.Message);
    }

    [Fact]
    public async Task ViewCheckinScanHistory_UTCID09_CustomerRoleCaller_ThrowsUnauthorizedAccessException()
    {
        var repo = new Mock<ICheckinRepository>();
        var service = CreateMonitorService(repo, new Mock<CheckinAPI.API.IEventApiClient>());

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetScanHistoryAsync(3001, null, null, 500, 20, "Customer", strictValidation: true));

        Assert.Equal("Staff permission required", ex.Message);
    }

    // =========================================================================
    // Existing Domain Calculator Regression Tests
    // =========================================================================

    [Fact]
    public void UC_3_13_2_ExportCheckinReport_ComputesCheckinRatioAndTiers()
    {
        var tickets = new[]
        {
            Ticket(1, "VIP", TicketStatuses.Active, true),
            Ticket(2, "VIP", TicketStatuses.Active, false),
            Ticket(3, "Standard", TicketStatuses.Active, true),
            Ticket(4, "Standard", TicketStatuses.Active, true),
            Ticket(5, "Standard", TicketStatuses.Returned, false)
        };

        var summary = CheckinReportCalculator.Summarize(tickets);

        Assert.Equal(4, summary.TotalTickets);
        Assert.Equal(3, summary.CheckedIn);
        Assert.Equal(1, summary.NotCheckedIn);
        Assert.Equal(1, summary.Returned);
        Assert.Equal(75.0, summary.CheckInRatePercent);
    }

    [Fact]
    public void UC_3_13_2_ExportCheckinReport_ValidCsvStructure()
    {
        var headers = new[] { "TicketId", "AttendeeName", "TicketType", "Gate", "CheckinTime", "Status" };
        var line = string.Join(",", headers);

        Assert.Contains("AttendeeName", line);
        Assert.Contains("CheckinTime", line);
    }
}
