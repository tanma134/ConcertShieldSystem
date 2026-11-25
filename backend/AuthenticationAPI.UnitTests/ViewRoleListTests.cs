using AuthenticationAPI.Models;
using AuthenticationAPI.Repositories;
using AuthenticationAPI.Services;
using Moq;
using Xunit;

namespace AuthenticationAPI.Tests;

public class ViewRoleListTests
{
    [Fact]
    public async Task UTCID01_GetAllAsync_ReturnsAllRolesInRepositoryOrder()
    {
        var repository = new Mock<IRoleRepository>();
        repository.Setup(r => r.GetAllAsync()).ReturnsAsync(
        [
            new Role { RoleId = 1, RoleName = "Admin" },
            new Role { RoleId = 2, RoleName = "Customer" },
            new Role { RoleId = 3, RoleName = "Organizer" }
        ]);
        var service = new RoleService(repository.Object);

        var roles = await service.GetAllAsync();

        Assert.Equal(3, roles.Count);
        AssertRole(roles[0], 1, "Admin");
        AssertRole(roles[1], 2, "Customer");
        AssertRole(roles[2], 3, "Organizer");
        repository.Verify(r => r.GetAllAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID02_GetAllAsync_ReturnsEmptyList_WhenRepositoryHasNoRoles()
    {
        var repository = new Mock<IRoleRepository>();
        repository.Setup(r => r.GetAllAsync()).ReturnsAsync([]);
        var service = new RoleService(repository.Object);

        var roles = await service.GetAllAsync();

        Assert.Empty(roles);
        repository.Verify(r => r.GetAllAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID03_GetAllAsync_ReturnsSingleRole()
    {
        var repository = new Mock<IRoleRepository>();
        repository.Setup(r => r.GetAllAsync()).ReturnsAsync(
        [
            new Role { RoleId = 2, RoleName = "Customer" }
        ]);
        var service = new RoleService(repository.Object);

        var roles = await service.GetAllAsync();

        Assert.Single(roles);
        AssertRole(roles[0], 2, "Customer");
        repository.Verify(r => r.GetAllAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID04_GetAllAsync_PropagatesRepositoryException()
    {
        var repository = new Mock<IRoleRepository>();
        repository.Setup(r => r.GetAllAsync())
            .ThrowsAsync(new InvalidOperationException("Database unavailable."));
        var service = new RoleService(repository.Object);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetAllAsync());

        Assert.Equal("Database unavailable.", exception.Message);
        repository.Verify(r => r.GetAllAsync(), Times.Once);
    }

    private static void AssertRole(object role, int expectedId, string expectedName)
    {
        Assert.Equal(expectedId, role.GetType().GetProperty("RoleId")!.GetValue(role));
        Assert.Equal(expectedName, role.GetType().GetProperty("RoleName")!.GetValue(role));
    }
}
