using AuthenticationAPI.Models;
using AuthenticationAPI.Repositories;
using AuthenticationAPI.Services;
using Moq;
using Xunit;

namespace AuthenticationAPI.UnitTests;

public sealed class ViewUserAccountListTests
{
    [Fact]
    public async Task UTCID01_ReturnsUsersInRepositoryOrderWithMappedSortedRolesAndStatuses()
    {
        var admin = TestData.CreateUser(1, "Admin");
        var staffAndCustomer = TestData.CreateUser(2, "Staff", "Customer");
        staffAndCustomer.IsActive = false;
        staffAndCustomer.IsVerified = false;
        staffAndCustomer.EkycStatus = "Pending";
        var createdAt = new DateTime(2025, 6, 1, 12, 30, 0, DateTimeKind.Utc);
        staffAndCustomer.CreatedAt = createdAt;
        var noRoles = TestData.CreateUser(3);
        var repository = new Mock<IUserRepository>();
        repository.Setup(repo => repo.GetAllAsync()).ReturnsAsync(new List<User> { admin, staffAndCustomer, noRoles });
        var service = new UserService(repository.Object);

        var users = await service.GetUsersAsync();

        Assert.Equal(new[] { 1, 2, 3 }, users.Select(user => user.UserId));
        Assert.Equal(new[] { "Admin" }, users[0].Roles.Select(role => role.RoleName));
        Assert.Equal(new[] { "Customer", "Staff" }, users[1].Roles.Select(role => role.RoleName));
        Assert.Empty(users[2].Roles);
        Assert.False(users[1].IsActive);
        Assert.False(users[1].IsVerified);
        Assert.Equal("Pending", users[1].EkycStatus);
        Assert.Equal(createdAt, users[1].CreatedAt);
        Assert.Equal(staffAndCustomer.Email, users[1].Email);
        Assert.Equal(staffAndCustomer.FullName, users[1].FullName);
        repository.Verify(repo => repo.GetAllAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID02_ReturnsEmptyListWhenRepositoryHasNoUsers()
    {
        var repository = new Mock<IUserRepository>();
        repository.Setup(repo => repo.GetAllAsync()).ReturnsAsync(new List<User>());
        var service = new UserService(repository.Object);

        var users = await service.GetUsersAsync();

        Assert.Empty(users);
        repository.Verify(repo => repo.GetAllAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID03_SkipsRoleEntriesWithNullRoleNavigation()
    {
        var user = TestData.CreateUser(4);
        user.UserRoles.Add(new UserRole { UserId = 4, RoleId = 5, Role = null! });
        var repository = new Mock<IUserRepository>();
        repository.Setup(repo => repo.GetAllAsync()).ReturnsAsync(new List<User> { user });
        var service = new UserService(repository.Object);

        var result = await service.GetUsersAsync();

        Assert.Empty(Assert.Single(result).Roles);
        repository.Verify(repo => repo.GetAllAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID04_PropagatesRepositoryException()
    {
        var repository = new Mock<IUserRepository>();
        repository.Setup(repo => repo.GetAllAsync()).ThrowsAsync(new InvalidOperationException("Repository failure"));
        var service = new UserService(repository.Object);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetUsersAsync());

        Assert.Equal("Repository failure", exception.Message);
        repository.Verify(repo => repo.GetAllAsync(), Times.Once);
    }
}
