using AuthenticationAPI.Models;
using AuthenticationAPI.Repositories;
using AuthenticationAPI.Services;
using Moq;
using Xunit;

namespace AuthenticationAPI.Tests;

public class ViewProfileTests
{
    [Fact]
    public async Task UTCID01_GetProfileAsync_UsesUserIdAndMapsProfileAndRoles()
    {
        var user = CreateUser(
            roles:
            [
                new Role { RoleName = "Staff" },
                new Role { RoleName = "Customer" }
            ]);
        var repository = new Mock<IUserRepository>();
        repository.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(user);
        var service = CreateService(repository);

        var profile = await service.GetProfileAsync("an@mail.com", "5");

        Assert.NotNull(profile);
        Assert.Equal(5, profile.UserId);
        Assert.Equal("an@mail.com", profile.Email);
        Assert.Equal("Staff", profile.RoleName);
        Assert.Equal(["Customer", "Staff"], profile.Roles);
        repository.Verify(r => r.GetByIdAsync(5), Times.Once);
        repository.Verify(r => r.GetByEmailAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UTCID02_GetProfileAsync_FallsBackToEmail_WhenUserIdIsNull()
    {
        var user = CreateUser();
        var repository = new Mock<IUserRepository>();
        repository.Setup(r => r.GetByEmailAsync("an@mail.com")).ReturnsAsync(user);
        var service = CreateService(repository);

        var profile = await service.GetProfileAsync("an@mail.com", null);

        Assert.NotNull(profile);
        Assert.Equal(user.UserId, profile.UserId);
        repository.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
        repository.Verify(r => r.GetByEmailAsync("an@mail.com"), Times.Once);
    }

    [Fact]
    public async Task UTCID03_GetProfileAsync_FallsBackToEmail_WhenUserIdDoesNotExist()
    {
        var user = CreateUser();
        var repository = new Mock<IUserRepository>();
        repository.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((User?)null);
        repository.Setup(r => r.GetByEmailAsync("an@mail.com")).ReturnsAsync(user);
        var service = CreateService(repository);

        var profile = await service.GetProfileAsync("an@mail.com", "999");

        Assert.NotNull(profile);
        Assert.Equal(user.UserId, profile.UserId);
        repository.Verify(r => r.GetByIdAsync(999), Times.Once);
        repository.Verify(r => r.GetByEmailAsync("an@mail.com"), Times.Once);
    }

    [Fact]
    public async Task UTCID04_GetProfileAsync_ReturnsNull_WhenUserIdIsUnknownAndEmailIsNull()
    {
        var repository = new Mock<IUserRepository>();
        repository.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((User?)null);
        var service = CreateService(repository);

        var profile = await service.GetProfileAsync(null, "999");

        Assert.Null(profile);
        repository.Verify(r => r.GetByIdAsync(999), Times.Once);
        repository.Verify(r => r.GetByEmailAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UTCID05_GetProfileAsync_ReturnsNull_WhenUserIdIsNotIntegerAndEmailIsNull()
    {
        var repository = new Mock<IUserRepository>();
        var service = CreateService(repository);

        var profile = await service.GetProfileAsync(null, "abc");

        Assert.Null(profile);
        repository.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
        repository.Verify(r => r.GetByEmailAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UTCID06_GetProfileAsync_ReturnsNull_WhenBothClaimsAreNull()
    {
        var repository = new Mock<IUserRepository>();
        var service = CreateService(repository);

        var profile = await service.GetProfileAsync(null, null);

        Assert.Null(profile);
        repository.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
        repository.Verify(r => r.GetByEmailAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UTCID07_GetProfileAsync_ReturnsNull_WhenEmailIsWhitespace()
    {
        var repository = new Mock<IUserRepository>();
        var service = CreateService(repository);

        var profile = await service.GetProfileAsync("   ", null);

        Assert.Null(profile);
        repository.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
        repository.Verify(r => r.GetByEmailAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UTCID08_GetProfileAsync_ReturnsNull_WhenEmailFallbackDoesNotFindUser()
    {
        var repository = new Mock<IUserRepository>();
        repository.Setup(r => r.GetByEmailAsync("unknown@mail.com")).ReturnsAsync((User?)null);
        var service = CreateService(repository);

        var profile = await service.GetProfileAsync("unknown@mail.com", null);

        Assert.Null(profile);
        repository.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
        repository.Verify(r => r.GetByEmailAsync("unknown@mail.com"), Times.Once);
    }

    [Fact]
    public async Task UTCID09_GetProfileAsync_MapsUserWithoutRoles()
    {
        var user = CreateUser(roles: []);
        var repository = new Mock<IUserRepository>();
        repository.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(user);
        var service = CreateService(repository);

        var profile = await service.GetProfileAsync("an@mail.com", "5");

        Assert.NotNull(profile);
        Assert.Null(profile.RoleName);
        Assert.Empty(profile.Roles);
        repository.Verify(r => r.GetByIdAsync(5), Times.Once);
        repository.Verify(r => r.GetByEmailAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UTCID10_GetProfileAsync_ReturnsNull_WhenNeitherClaimFindsUser()
    {
        var repository = new Mock<IUserRepository>();
        repository.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((User?)null);
        repository.Setup(r => r.GetByEmailAsync("unknown@mail.com")).ReturnsAsync((User?)null);
        var service = CreateService(repository);

        var profile = await service.GetProfileAsync("unknown@mail.com", "999");

        Assert.Null(profile);
        repository.Verify(r => r.GetByIdAsync(999), Times.Once);
        repository.Verify(r => r.GetByEmailAsync("unknown@mail.com"), Times.Once);
    }

    private static ProfileService CreateService(Mock<IUserRepository> repository) =>
        new(repository.Object, Mock.Of<IAvatarStorageService>());

    private static User CreateUser(IReadOnlyCollection<Role>? roles = null)
    {
        var user = new User
        {
            UserId = 5,
            Email = "an@mail.com",
            FullName = "An Nguyen",
            PhoneNumber = "0901000001",
            AvatarUrl = null,
            IsVerified = true,
            IsActive = true,
            EkycStatus = "Verified",
            CreatedAt = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc)
        };

        if (roles != null)
        {
            user.UserRoles = roles
                .Select(role => new UserRole { Role = role })
                .ToList();
        }

        return user;
    }
}