using AuthenticationAPI.Models;
using AuthenticationAPI.Repositories;
using AuthenticationAPI.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AuthenticationAPI.UnitTests;

/// <summary>
/// Unit Tests for Function_163_ReviewOrganizerVer (GetVerificationRequests / GetVerificationRequestsAsync / GetAllRequestsAsync)
/// Total Specification Test Cases: 9 (UTCID01..UTCID09: N=3, B=3, A=3) + regression tests
/// </summary>
public sealed class ViewOrganizerRequestListTests
{
    [Fact]
    public async Task ReviewOrganizerVerification_UTCID01_AllRequests_ReturnsListAndPreservesOrder()
    {
        var pendingUser = TestData.CreateUser(11);
        var pending = TestData.CreateOrganizerRequest(2, "Pending", pendingUser);
        var approvedUser = TestData.CreateUser(12);
        var admin = TestData.CreateUser(1, "Admin");
        admin.FullName = "Admin One";
        var approved = TestData.CreateOrganizerRequest(1, "Approved", approvedUser, 1, admin);
        var repository = new Mock<IOrganizerRequestRepository>();
        repository.Setup(repo => repo.GetAllAsync(null)).ReturnsAsync(new List<OrganizerRequest> { pending, approved });
        var service = CreateService(repository);

        var result = await service.GetVerificationRequestsAsync(status: null, page: 1, limit: 10, callerRole: "Admin");

        Assert.Equal(new[] { 2, 1 }, result.Select(request => request.RequestId));
        Assert.Equal("user11@example.com", result[0].UserEmail);
        Assert.Equal("Admin One", result[1].ReviewedByName);
    }

    [Fact]
    public async Task ReviewOrganizerVerification_UTCID02_FilterByPendingStatus_ReturnsPendingRequests()
    {
        var request = TestData.CreateOrganizerRequest(2, "PENDING", TestData.CreateUser(11));
        var repository = new Mock<IOrganizerRequestRepository>();
        repository.Setup(repo => repo.GetAllAsync("PENDING"))
            .ReturnsAsync(new List<OrganizerRequest> { request });
        var service = CreateService(repository);

        var result = await service.GetVerificationRequestsAsync(status: "PENDING", page: 1, limit: 10, callerRole: "Admin");

        var mapped = Assert.Single(result);
        Assert.Equal(2, mapped.RequestId);
        Assert.Equal("PENDING", mapped.Status);
    }

    [Fact]
    public async Task ReviewOrganizerVerification_UTCID03_FilterByApprovedStatus_ReturnsApprovedRequests()
    {
        var request = TestData.CreateOrganizerRequest(3, "APPROVED", TestData.CreateUser(12));
        var repository = new Mock<IOrganizerRequestRepository>();
        repository.Setup(repo => repo.GetAllAsync("APPROVED"))
            .ReturnsAsync(new List<OrganizerRequest> { request });
        var service = CreateService(repository);

        var result = await service.GetVerificationRequestsAsync(status: "APPROVED", page: 1, limit: 10, callerRole: "Admin");

        var mapped = Assert.Single(result);
        Assert.Equal(3, mapped.RequestId);
        Assert.Equal("APPROVED", mapped.Status);
    }

    [Fact]
    public async Task ReviewOrganizerVerification_UTCID04_BoundaryLimitMin1_ReturnsSingleItem()
    {
        var requests = new List<OrganizerRequest>
        {
            TestData.CreateOrganizerRequest(2, "Pending", TestData.CreateUser(11)),
            TestData.CreateOrganizerRequest(1, "Approved", TestData.CreateUser(12))
        };
        var repository = new Mock<IOrganizerRequestRepository>();
        repository.Setup(repo => repo.GetAllAsync(null)).ReturnsAsync(requests);
        var service = CreateService(repository);

        var result = await service.GetVerificationRequestsAsync(status: null, page: 1, limit: 1, callerRole: "Admin");

        Assert.Single(result);
    }

    [Fact]
    public async Task ReviewOrganizerVerification_UTCID05_BoundaryLimitMax100_ReturnsAllMatchingItems()
    {
        var requests = new List<OrganizerRequest>
        {
            TestData.CreateOrganizerRequest(2, "Pending", TestData.CreateUser(11)),
            TestData.CreateOrganizerRequest(1, "Approved", TestData.CreateUser(12))
        };
        var repository = new Mock<IOrganizerRequestRepository>();
        repository.Setup(repo => repo.GetAllAsync(null)).ReturnsAsync(requests);
        var service = CreateService(repository);

        var result = await service.GetVerificationRequestsAsync(status: null, page: 1, limit: 100, callerRole: "Admin");

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task ReviewOrganizerVerification_UTCID06_BoundaryZeroPendingRequests_ReturnsEmptyList()
    {
        var repository = new Mock<IOrganizerRequestRepository>();
        repository.Setup(repo => repo.GetAllAsync("PENDING")).ReturnsAsync(new List<OrganizerRequest>());
        var service = CreateService(repository);

        var result = await service.GetVerificationRequestsAsync(status: "PENDING", page: 1, limit: 10, callerRole: "Admin");

        Assert.Empty(result);
    }

    [Fact]
    public async Task ReviewOrganizerVerification_UTCID07_BoundaryPageZero_ThrowsArgumentException()
    {
        var repository = new Mock<IOrganizerRequestRepository>();
        var service = CreateService(repository);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetVerificationRequestsAsync(status: null, page: 0, limit: 10, callerRole: "Admin"));

        Assert.Equal("Page must be >= 1", ex.Message);
    }

    [Fact]
    public async Task ReviewOrganizerVerification_UTCID08_AbnormalNegativePage_ThrowsArgumentException()
    {
        var repository = new Mock<IOrganizerRequestRepository>();
        var service = CreateService(repository);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetVerificationRequestsAsync(status: null, page: -1, limit: 10, callerRole: "Admin"));

        Assert.Equal("Page must be >= 1", ex.Message);
    }

    [Fact]
    public async Task ReviewOrganizerVerification_UTCID09_CallerIsOrganizerRole_ThrowsUnauthorizedAccessException()
    {
        var repository = new Mock<IOrganizerRequestRepository>();
        var service = CreateService(repository);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetVerificationRequestsAsync(status: null, page: 1, limit: 10, callerRole: "Organizer"));

        Assert.Equal("Admin access required", ex.Message);
    }

    private static OrganizerRequestService CreateService(Mock<IOrganizerRequestRepository> repository) =>
        new(repository.Object, Mock.Of<IUserRepository>(), Mock.Of<ILogger<OrganizerRequestService>>());
}
