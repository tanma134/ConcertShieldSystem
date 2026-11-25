using AuthenticationAPI.DTOs;
using AuthenticationAPI.Models;
using AuthenticationAPI.Repositories;
using AuthenticationAPI.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AuthenticationAPI.UnitTests;

public sealed class ApproveRejectOrganizerRequestTests
{
    [Fact]
    public async Task UTCID01_ApprovesPendingRequestAndAssignsOrganizerRole()
    {
        var user = TestData.CreateUser(11, "Customer");
        var request = TestData.CreateOrganizerRequest(30, "Pending", user);
        var repositories = CreateRepositories(request);
        repositories.Users.Setup(repo => repo.GetRoleByNameAsync("Organizer"))
            .ReturnsAsync(new Role { RoleId = 3, RoleName = "Organizer" });
        repositories.Users.Setup(repo => repo.AddUserRoleAsync(It.IsAny<UserRole>()))
            .Returns(Task.CompletedTask);
        repositories.Requests.Setup(repo => repo.SaveChangesAsync()).Returns(Task.CompletedTask);

        var result = await CreateService(repositories).ReviewRequestAsync(
            30, 1, new ReviewOrganizerRequestDTO { Status = "Approved", ReviewNote = "Verified company" });

        Assert.Equal("Approved", result.Status);
        Assert.Equal(1, result.ReviewedBy);
        Assert.NotNull(result.ReviewedAt);
        Assert.Equal("Verified company", result.ReviewNote);
        Assert.Null(result.RejectionReason);
        repositories.Users.Verify(repo => repo.AddUserRoleAsync(It.Is<UserRole>(role =>
            role.UserId == 11 && role.RoleId == 3)), Times.Once);
        repositories.Requests.Verify(repo => repo.SaveChangesAsync(), Times.Once);
        repositories.Notifier.Verify(notifier => notifier.NotifyApprovedAsync(30, 11), Times.Once);
    }

    [Fact]
    public async Task UTCID02_ApprovesRequestWithoutDuplicatingExistingOrganizerRole()
    {
        var user = TestData.CreateUser(11, "Organizer");
        var request = TestData.CreateOrganizerRequest(30, "Pending", user);
        var repositories = CreateRepositories(request);
        repositories.Users.Setup(repo => repo.GetRoleByNameAsync("Organizer"))
            .ReturnsAsync(new Role { RoleId = 3, RoleName = "Organizer" });
        repositories.Requests.Setup(repo => repo.SaveChangesAsync()).Returns(Task.CompletedTask);

        var result = await CreateService(repositories).ReviewRequestAsync(
            30, 1, new ReviewOrganizerRequestDTO { Status = "Approved", ReviewNote = "Verified company" });

        Assert.Equal("Approved", result.Status);
        Assert.Equal(1, result.ReviewedBy);
        Assert.Null(result.RejectionReason);
        repositories.Users.Verify(repo => repo.AddUserRoleAsync(It.IsAny<UserRole>()), Times.Never);
        repositories.Requests.Verify(repo => repo.SaveChangesAsync(), Times.Once);
        repositories.Notifier.Verify(notifier => notifier.NotifyApprovedAsync(30, 11), Times.Once);
    }

    [Fact]
    public async Task UTCID03_RejectsRequestAndStoresReasonWithoutOrganizerRoleLookup()
    {
        var request = TestData.CreateOrganizerRequest(
            30, "Pending", TestData.CreateUser(11, "Customer"));
        var repositories = CreateRepositories(request);
        repositories.Requests.Setup(repo => repo.SaveChangesAsync()).Returns(Task.CompletedTask);

        var result = await CreateService(repositories).ReviewRequestAsync(
            30, 1, new ReviewOrganizerRequestDTO
            {
                Status = "Rejected",
                RejectionReason = "Incomplete documents"
            });

        Assert.Equal("Rejected", result.Status);
        Assert.Equal(1, result.ReviewedBy);
        Assert.Equal("Incomplete documents", result.RejectionReason);
        Assert.Null(result.ReviewNote);
        repositories.Users.Verify(repo => repo.GetRoleByNameAsync(It.IsAny<string>()), Times.Never);
        repositories.Users.Verify(repo => repo.AddUserRoleAsync(It.IsAny<UserRole>()), Times.Never);
        repositories.Requests.Verify(repo => repo.SaveChangesAsync(), Times.Once);
        repositories.Notifier.Verify(notifier => notifier.NotifyApprovedAsync(
            It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task UTCID04_ApprovesRequestAndIgnoresRejectionReason()
    {
        var request = TestData.CreateOrganizerRequest(
            30, "Pending", TestData.CreateUser(11, "Customer"));
        var repositories = CreateRepositories(request);
        repositories.Users.Setup(repo => repo.GetRoleByNameAsync("Organizer"))
            .ReturnsAsync(new Role { RoleId = 3, RoleName = "Organizer" });
        repositories.Users.Setup(repo => repo.AddUserRoleAsync(It.IsAny<UserRole>()))
            .Returns(Task.CompletedTask);
        repositories.Requests.Setup(repo => repo.SaveChangesAsync()).Returns(Task.CompletedTask);

        var result = await CreateService(repositories).ReviewRequestAsync(
            30, 1, new ReviewOrganizerRequestDTO
            {
                Status = "Approved",
                RejectionReason = "Incomplete documents",
                ReviewNote = "Verified company"
            });

        Assert.Equal("Approved", result.Status);
        Assert.Equal("Verified company", result.ReviewNote);
        Assert.Null(result.RejectionReason);
        repositories.Users.Verify(repo => repo.AddUserRoleAsync(It.Is<UserRole>(role =>
            role.UserId == 11 && role.RoleId == 3)), Times.Once);
        repositories.Requests.Verify(repo => repo.SaveChangesAsync(), Times.Once);
        repositories.Notifier.Verify(notifier => notifier.NotifyApprovedAsync(30, 11), Times.Once);
    }

    [Fact]
    public async Task UTCID05_RejectsWhitespaceRejectionReasonBeforeRepositoryLookup()
    {
        var repositories = CreateRepositories();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService(repositories).ReviewRequestAsync(
                30, 1, new ReviewOrganizerRequestDTO { Status = "Rejected", RejectionReason = "   " }));

        Assert.Equal("RejectionReason is required when rejecting a request.", exception.Message);
        repositories.Requests.Verify(repo => repo.GetByIdAsync(It.IsAny<int>()), Times.Never);
        repositories.Requests.Verify(repo => repo.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UTCID06_RejectsNullRejectionReasonBeforeRepositoryLookup()
    {
        var repositories = CreateRepositories();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService(repositories).ReviewRequestAsync(
                30, 1, new ReviewOrganizerRequestDTO { Status = "Rejected", RejectionReason = null }));

        Assert.Equal("RejectionReason is required when rejecting a request.", exception.Message);
        repositories.Requests.Verify(repo => repo.GetByIdAsync(It.IsAny<int>()), Times.Never);
        repositories.Requests.Verify(repo => repo.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UTCID07_RejectsPendingAsReviewStatusBeforeRepositoryLookup()
    {
        var repositories = CreateRepositories();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService(repositories).ReviewRequestAsync(
                999, 1, new ReviewOrganizerRequestDTO { Status = "Pending" }));

        Assert.Equal("Status must be 'Approved' or 'Rejected'.", exception.Message);
        repositories.Requests.Verify(repo => repo.GetByIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task UTCID08_RejectsLowercaseApprovedStatus()
    {
        var repositories = CreateRepositories();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService(repositories).ReviewRequestAsync(
                30, 1, new ReviewOrganizerRequestDTO { Status = "approved" }));

        Assert.Equal("Status must be 'Approved' or 'Rejected'.", exception.Message);
        repositories.Requests.Verify(repo => repo.GetByIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task UTCID09_RejectsNullStatus()
    {
        var repositories = CreateRepositories();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService(repositories).ReviewRequestAsync(
                30, 1, new ReviewOrganizerRequestDTO { Status = null! }));

        Assert.Equal("Status must be 'Approved' or 'Rejected'.", exception.Message);
        repositories.Requests.Verify(repo => repo.GetByIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task UTCID10_ThrowsWhenOrganizerRequestDoesNotExist()
    {
        var repositories = CreateRepositories();
        repositories.Requests.Setup(repo => repo.GetByIdAsync(999)).ReturnsAsync((OrganizerRequest?)null);

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            CreateService(repositories).ReviewRequestAsync(
                999, 1, new ReviewOrganizerRequestDTO { Status = "Approved", ReviewNote = "Verified company" }));

        Assert.Equal("Organizer request not found.", exception.Message);
        repositories.Requests.Verify(repo => repo.SaveChangesAsync(), Times.Never);
        repositories.Notifier.Verify(notifier => notifier.NotifyApprovedAsync(
            It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task UTCID11_RejectsAlreadyApprovedRequest()
    {
        var request = TestData.CreateOrganizerRequest(
            30, "Approved", TestData.CreateUser(11, "Customer"));
        var repositories = CreateRepositories(request);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(repositories).ReviewRequestAsync(
                30, 1, new ReviewOrganizerRequestDTO { Status = "Approved", ReviewNote = "Verified company" }));

        Assert.Equal("This request has already been approved.", exception.Message);
        repositories.Requests.Verify(repo => repo.SaveChangesAsync(), Times.Never);
        repositories.Notifier.Verify(notifier => notifier.NotifyApprovedAsync(
            It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task UTCID12_RejectsAlreadyRejectedRequest()
    {
        var request = TestData.CreateOrganizerRequest(
            30, "Rejected", TestData.CreateUser(11, "Customer"));
        var repositories = CreateRepositories(request);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(repositories).ReviewRequestAsync(
                30, 1, new ReviewOrganizerRequestDTO
                {
                    Status = "Rejected",
                    RejectionReason = "Incomplete documents"
                }));

        Assert.Equal("This request has already been rejected.", exception.Message);
        repositories.Requests.Verify(repo => repo.SaveChangesAsync(), Times.Never);
        repositories.Notifier.Verify(notifier => notifier.NotifyApprovedAsync(
            It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task UTCID13_ThrowsWhenOrganizerRoleIsNotSeeded()
    {
        var request = TestData.CreateOrganizerRequest(
            30, "Pending", TestData.CreateUser(11, "Customer"));
        var repositories = CreateRepositories(request);
        repositories.Users.Setup(repo => repo.GetRoleByNameAsync("Organizer"))
            .ReturnsAsync((Role?)null);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(repositories).ReviewRequestAsync(
                30, 1, new ReviewOrganizerRequestDTO { Status = "Approved", ReviewNote = "Verified company" }));

        Assert.Equal("Role 'Organizer' is not seeded in the database.", exception.Message);
        repositories.Requests.Verify(repo => repo.SaveChangesAsync(), Times.Never);
        repositories.Users.Verify(repo => repo.AddUserRoleAsync(It.IsAny<UserRole>()), Times.Never);
        repositories.Notifier.Verify(notifier => notifier.NotifyApprovedAsync(
            It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    private static (
        Mock<IOrganizerRequestRepository> Requests,
        Mock<IUserRepository> Users,
        Mock<IOrganizerApprovalNotifier> Notifier) CreateRepositories(OrganizerRequest? request = null)
    {
        var requests = new Mock<IOrganizerRequestRepository>();
        if (request != null)
            requests.Setup(repo => repo.GetByIdAsync(request.RequestId)).ReturnsAsync(request);
        var users = new Mock<IUserRepository>();
        var notifier = new Mock<IOrganizerApprovalNotifier>();
        notifier.Setup(mock => mock.NotifyApprovedAsync(It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);
        return (requests, users, notifier);
    }

    private static OrganizerRequestService CreateService(
        (Mock<IOrganizerRequestRepository> Requests,
            Mock<IUserRepository> Users,
            Mock<IOrganizerApprovalNotifier> Notifier) repositories) =>
        new(
            repositories.Requests.Object,
            repositories.Users.Object,
            Mock.Of<ILogger<OrganizerRequestService>>(),
            repositories.Notifier.Object);
}
