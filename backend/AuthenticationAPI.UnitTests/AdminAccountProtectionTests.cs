using AuthenticationAPI.DTOs;
using AuthenticationAPI.Models;
using AuthenticationAPI.Repositories;
using AuthenticationAPI.Services;
using Moq;
using Xunit;

namespace AuthenticationAPI.UnitTests;

public sealed class AdminAccountProtectionTests
{
    [Fact]
    public async Task UpdateUserAsync_RejectsAdminAccountWithoutChangingProfile()
    {
        var admin = TestData.CreateUser(1, "Admin");
        var repository = new Mock<IUserRepository>();
        repository.Setup(repo => repo.GetByIdAsync(1)).ReturnsAsync(admin);

        var result = await new UserService(repository.Object).UpdateUserAsync(
            1,
            new UpdateUserDTO { FullName = "Changed Admin Name" });

        Assert.False(result.Success);
        Assert.Equal("Admin accounts cannot be modified.", result.Message);
        Assert.Equal("User 1", admin.FullName);
        repository.Verify(repo => repo.SaveChangesAsync(), Times.Never);
        repository.Verify(repo => repo.RemoveUserRole(It.IsAny<UserRole>()), Times.Never);
    }

    [Fact]
    public async Task AddRoleAsync_RejectsAdminAccountWithoutChangingRoles()
    {
        var repository = new Mock<IUserRepository>();
        repository.Setup(repo => repo.GetByIdAsync(1))
            .ReturnsAsync(TestData.CreateUser(1, "Admin"));

        var result = await new UserService(repository.Object).AddRoleAsync(1, "Organizer");

        Assert.False(result.Success);
        Assert.Equal("Admin accounts cannot be modified.", result.Message);
        repository.Verify(repo => repo.AddUserRoleAsync(It.IsAny<UserRole>()), Times.Never);
        repository.Verify(repo => repo.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UpdateUserStatusAsync_RejectsAdminAccount()
    {
        var repository = new Mock<IUserRepository>();
        repository.Setup(repo => repo.GetByIdAsync(1))
            .ReturnsAsync(TestData.CreateUser(1, "Admin"));

        var result = await new UserService(repository.Object).UpdateUserStatusAsync(1, 2);

        Assert.False(result.Success);
        Assert.Equal("Admin accounts cannot be blocked or unblocked.", result.Message);
        repository.Verify(repo => repo.SaveChangesAsync(), Times.Never);
    }
}
