using AuthenticationAPI.Controllers;
using AuthenticationAPI.DTOs;
using AuthenticationAPI.Models;
using AuthenticationAPI.Repositories;
using AuthenticationAPI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AuthenticationAPI.Tests;

public class ViewProfileUnitTest
{
    [Fact]
    public async Task UTCID01_GetProfile_ReturnsOkWithProfile_WhenServiceFindsUser()
    {
        var profile = new ProfileDTO
        {
            UserId = 42,
            Email = "vinh@example.com",
            FullName = "the vinh"
        };
        var service = new Mock<IProfileService>();
        service
            .Setup(s => s.GetProfileAsync(It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(profile);

        var controller = new ProfileController(service.Object, NullLogger<ProfileController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.GetProfile();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(profile, ok.Value);
    }

    [Fact]
    public async Task UTCID02_GetProfile_ReturnsNotFound_WhenServiceReturnsNull()
    {
        var service = new Mock<IProfileService>();
        service
            .Setup(s => s.GetProfileAsync(It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync((ProfileDTO?)null);

        var controller = new ProfileController(service.Object, NullLogger<ProfileController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.GetProfile();

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFound.StatusCode);
        Assert.Equal(
            "User not found.",
            notFound.Value?.GetType().GetProperty("message")?.GetValue(notFound.Value));
    }

    [Fact]
    public async Task UTCID03_GetProfileAsync_MapsUserAndRoles_WhenUserExists()
    {
        var createdAt = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var user = new User
        {
            UserId = 42,
            Email = "vinh@example.com",
            FullName = "the vinh",
            PhoneNumber = null,
            AvatarUrl = null,
            IsVerified = true,
            IsActive = true,
            EkycStatus = "Verified",
            CreatedAt = createdAt,
            UserRoles =
            [
                new UserRole { Role = new Role { RoleName = "Customer" } },
                new UserRole { Role = new Role { RoleName = "Organizer" } }
            ]
        };
        var repository = new Mock<IUserRepository>();
        repository.Setup(r => r.GetByIdAsync(42)).ReturnsAsync(user);
        var service = new ProfileService(repository.Object, Mock.Of<IAvatarStorageService>());

        var profile = await service.GetProfileAsync(null, "42");

        Assert.NotNull(profile);
        Assert.Equal(42, profile.UserId);
        Assert.Equal("vinh@example.com", profile.Email);
        Assert.Equal("the vinh", profile.FullName);
        Assert.Null(profile.PhoneNumber);
        Assert.Null(profile.AvatarUrl);
        Assert.True(profile.IsVerified);
        Assert.True(profile.IsActive);
        Assert.Equal("Verified", profile.EkycStatus);
        Assert.Equal(createdAt, profile.CreatedAt);
        Assert.Equal("Customer", profile.RoleName);
        Assert.Equal(["Customer", "Organizer"], profile.Roles);
        repository.Verify(r => r.GetByIdAsync(42), Times.Once);
    }

    [Fact]
    public async Task UTCID04_GetProfileAsync_ReturnsNull_WhenUserIdDoesNotExist()
    {
        var repository = new Mock<IUserRepository>();
        repository.Setup(r => r.GetByIdAsync(404)).ReturnsAsync((User?)null);
        var service = new ProfileService(repository.Object, Mock.Of<IAvatarStorageService>());

        var profile = await service.GetProfileAsync(null, "404");

        Assert.Null(profile);
        repository.Verify(r => r.GetByIdAsync(404), Times.Once);
    }

    [Fact]
    public async Task UTCID05_GetProfileAsync_PropagatesRepositoryException()
    {
        var repository = new Mock<IUserRepository>();
        repository
            .Setup(r => r.GetByIdAsync(500))
            .ThrowsAsync(new InvalidOperationException("Repository unavailable."));
        var service = new ProfileService(repository.Object, Mock.Of<IAvatarStorageService>());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetProfileAsync(null, "500"));

        Assert.Equal("Repository unavailable.", exception.Message);
        repository.Verify(r => r.GetByIdAsync(500), Times.Once);
    }
}
