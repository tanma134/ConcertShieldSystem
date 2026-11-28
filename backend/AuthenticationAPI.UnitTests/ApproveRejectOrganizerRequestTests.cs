using AuthenticationAPI.DTOs;
using AuthenticationAPI.Models;
using AuthenticationAPI.Repositories;
using AuthenticationAPI.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AuthenticationAPI.UnitTests;

/// <summary>
/// Unit Tests for Function_164_ApproveRejectVerif (ReviewVerification / ReviewVerificationAsync / ReviewRequestAsync)
/// Covers all 9 UTCIDs from Excel specification (UTCID01..UTCID09: N=2, B=2, A=5) plus service regression cases.
/// </summary>
public sealed class ApproveRejectOrganizerRequestTests
{
    [Fact]
    public async Task ReviewVerification_UTCID01_ApprovePendingRequest_UpdatesStatusToApproved()
    {
        var user = TestData.CreateUser(11, "Customer");
        var request = TestData.CreateOrganizerRequest(501, "Pending", user);
        var repositories = CreateRepositories(request);
        repositories.Users.Setup(repo => repo.GetRoleByNameAsync("Organizer"))
            .ReturnsAsync(new Role { RoleId = 3, RoleName = "Organizer" });
        repositories.Users.Setup(repo => repo.AddUserRoleAsync(It.IsAny<UserRole>()))
            .Returns(Task.CompletedTask);
        repositories.Requests.Setup(repo => repo.SaveChangesAsync()).Returns(Task.CompletedTask);

        var result = await CreateService(repositories).ReviewVerificationAsync(
            501, 1, new ReviewOrganizerRequestDTO { Decision = "APPROVE", RejectionReason = null }, callerRole: "Admin");

        Assert.Equal("APPROVED", result.Status);
        Assert.Equal(1, result.ReviewedBy);
        Assert.NotNull(result.ReviewedAt);
        Assert.Null(result.RejectionReason);
    }

    [Fact]
    public async Task ReviewVerification_UTCID02_RejectPendingRequestWithReason_UpdatesStatusToRejected()
    {
        var user = TestData.CreateUser(11, "Customer");
        var request = TestData.CreateOrganizerRequest(501, "Pending", user);
        var repositories = CreateRepositories(request);
        repositories.Requests.Setup(repo => repo.SaveChangesAsync()).Returns(Task.CompletedTask);

        var result = await CreateService(repositories).ReviewVerificationAsync(
            501, 1, new ReviewOrganizerRequestDTO
            {
                Decision = "REJECT",
                RejectionReason = "Business registration certificate expired"
            }, callerRole: "Admin");

        Assert.Equal("REJECTED", result.Status);
        Assert.Equal("Business registration certificate expired", result.RejectionReason);
    }

    [Fact]
    public async Task ReviewVerification_UTCID03_BoundaryApproveWithEmptyRejectionReason_IsAllowed()
    {
        var user = TestData.CreateUser(11, "Customer");
        var request = TestData.CreateOrganizerRequest(501, "Pending", user);
        var repositories = CreateRepositories(request);
        repositories.Users.Setup(repo => repo.GetRoleByNameAsync("Organizer"))
            .ReturnsAsync(new Role { RoleId = 3, RoleName = "Organizer" });
        repositories.Requests.Setup(repo => repo.SaveChangesAsync()).Returns(Task.CompletedTask);

        var result = await CreateService(repositories).ReviewVerificationAsync(
            501, 1, new ReviewOrganizerRequestDTO { Decision = "APPROVE", RejectionReason = "" }, callerRole: "Admin");

        Assert.Equal("APPROVED", result.Status);
        Assert.Null(result.RejectionReason);
    }

    [Fact]
    public async Task ReviewVerification_UTCID04_BoundaryRejectWith500CharReason_UpdatesStatusToRejected()
    {
        var user = TestData.CreateUser(11, "Customer");
        var request = TestData.CreateOrganizerRequest(501, "Pending", user);
        var repositories = CreateRepositories(request);
        repositories.Requests.Setup(repo => repo.SaveChangesAsync()).Returns(Task.CompletedTask);
        var reason500 = new string('R', 500);

        var result = await CreateService(repositories).ReviewVerificationAsync(
            501, 1, new ReviewOrganizerRequestDTO
            {
                Decision = "REJECT",
                RejectionReason = reason500
            }, callerRole: "Admin");

        Assert.Equal("REJECTED", result.Status);
        Assert.Equal(500, result.RejectionReason!.Length);
    }

    [Fact]
    public async Task ReviewVerification_UTCID05_NonExistentRequestId99999_ThrowsKeyNotFoundException()
    {
        var repositories = CreateRepositories();
        repositories.Requests.Setup(repo => repo.GetByIdAsync(99999)).ReturnsAsync((OrganizerRequest?)null);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            CreateService(repositories).ReviewVerificationAsync(
                99999, 1, new ReviewOrganizerRequestDTO { Decision = "APPROVE" }, callerRole: "Admin"));

        Assert.Equal("Verification request not found", ex.Message);
    }

    [Fact]
    public async Task ReviewVerification_UTCID06_AlreadyResolvedRequestId502_ThrowsInvalidOperationException()
    {
        var request = TestData.CreateOrganizerRequest(502, "Approved", TestData.CreateUser(11, "Customer"));
        var repositories = CreateRepositories(request);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(repositories).ReviewVerificationAsync(
                502, 1, new ReviewOrganizerRequestDTO { Decision = "APPROVE" }, callerRole: "Admin"));

        Assert.Equal("Request has already been processed", ex.Message);
    }

    [Fact]
    public async Task ReviewVerification_UTCID07_RejectWithEmptyRejectionReason_ThrowsArgumentException()
    {
        var request = TestData.CreateOrganizerRequest(501, "Pending", TestData.CreateUser(11, "Customer"));
        var repositories = CreateRepositories(request);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService(repositories).ReviewVerificationAsync(
                501, 1, new ReviewOrganizerRequestDTO { Decision = "REJECT", RejectionReason = "" }, callerRole: "Admin"));

        Assert.Equal("RejectionReason is required when rejecting", ex.Message);
    }

    [Fact]
    public async Task ReviewVerification_UTCID08_InvalidDecisionAction_ThrowsArgumentException()
    {
        var request = TestData.CreateOrganizerRequest(501, "Pending", TestData.CreateUser(11, "Customer"));
        var repositories = CreateRepositories(request);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService(repositories).ReviewVerificationAsync(
                501, 1, new ReviewOrganizerRequestDTO { Decision = "INVALID_ACTION" }, callerRole: "Admin"));

        Assert.Equal("Invalid decision value", ex.Message);
    }

    [Fact]
    public async Task ReviewVerification_UTCID09_CallerIsOrganizerRole_ThrowsUnauthorizedAccessException()
    {
        var request = TestData.CreateOrganizerRequest(501, "Pending", TestData.CreateUser(11, "Customer"));
        var repositories = CreateRepositories(request);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreateService(repositories).ReviewVerificationAsync(
                501, 10, new ReviewOrganizerRequestDTO { Decision = "APPROVE" }, callerRole: "Organizer"));

        Assert.Equal("Admin privilege required", ex.Message);
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
