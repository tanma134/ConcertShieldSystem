using AuthenticationAPI.Models;
using AuthenticationAPI.Repositories;
using AuthenticationAPI.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AuthenticationAPI.UnitTests;

public sealed class ViewOrganizerRequestListTests
{
    [Fact]
    public async Task UTCID01_MapsAllRequestsAndPreservesRepositoryOrder()
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

        var result = await service.GetAllRequestsAsync(null);

        Assert.Equal(new[] { 2, 1 }, result.Select(request => request.RequestId));
        Assert.Equal("user11@example.com", result[0].UserEmail);
        Assert.Equal("User 11", result[0].UserFullName);
        Assert.Null(result[0].ReviewedByName);
        Assert.Equal("Admin One", result[1].ReviewedByName);
        Assert.Equal("user12@example.com", result[1].UserEmail);
        repository.Verify(repo => repo.GetAllAsync(null), Times.Once);
    }

    [Fact]
    public async Task UTCID02_ForwardsPendingStatusAndMapsOneRequest()
    {
        var request = TestData.CreateOrganizerRequest(
            2, "Pending", TestData.CreateUser(11));
        var repository = new Mock<IOrganizerRequestRepository>();
        repository.Setup(repo => repo.GetAllAsync("Pending"))
            .ReturnsAsync(new List<OrganizerRequest> { request });
        var service = CreateService(repository);

        var result = await service.GetAllRequestsAsync("Pending");

        var mapped = Assert.Single(result);
        Assert.Equal(2, mapped.RequestId);
        Assert.Equal("Pending", mapped.Status);
        repository.Verify(repo => repo.GetAllAsync("Pending"), Times.Once);
    }

    [Fact]
    public async Task UTCID03_ReturnsEmptyListForUnknownStatus()
    {
        var repository = new Mock<IOrganizerRequestRepository>();
        repository.Setup(repo => repo.GetAllAsync("Unknown")).ReturnsAsync(new List<OrganizerRequest>());
        var service = CreateService(repository);

        var result = await service.GetAllRequestsAsync("Unknown");

        Assert.Empty(result);
        repository.Verify(repo => repo.GetAllAsync("Unknown"), Times.Once);
    }

    [Fact]
    public async Task UTCID04_ForwardsWhitespaceStatusWithoutFilteringInService()
    {
        var requests = new List<OrganizerRequest>
        {
            TestData.CreateOrganizerRequest(2, "Pending", TestData.CreateUser(11)),
            TestData.CreateOrganizerRequest(1, "Approved", TestData.CreateUser(12))
        };
        var repository = new Mock<IOrganizerRequestRepository>();
        repository.Setup(repo => repo.GetAllAsync("   ")).ReturnsAsync(requests);
        var service = CreateService(repository);

        var result = await service.GetAllRequestsAsync("   ");

        Assert.Equal(new[] { 2, 1 }, result.Select(request => request.RequestId));
        repository.Verify(repo => repo.GetAllAsync("   "), Times.Once);
    }

    [Fact]
    public async Task UTCID05_PropagatesRepositoryException()
    {
        var repository = new Mock<IOrganizerRequestRepository>();
        repository.Setup(repo => repo.GetAllAsync(null))
            .ThrowsAsync(new InvalidOperationException("Repository failure"));
        var service = CreateService(repository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetAllRequestsAsync(null));

        Assert.Equal("Repository failure", exception.Message);
        repository.Verify(repo => repo.GetAllAsync(null), Times.Once);
    }

    private static OrganizerRequestService CreateService(Mock<IOrganizerRequestRepository> repository) =>
        new(repository.Object, Mock.Of<IUserRepository>(), Mock.Of<ILogger<OrganizerRequestService>>());
}
