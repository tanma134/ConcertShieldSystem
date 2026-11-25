using AuthenticationAPI.Repositories;
using AuthenticationAPI.Services;
using Moq;
using Xunit;

namespace AuthenticationAPI.UnitTests;

public sealed class ViewUserAccountDetailsTests
{
    [Fact]
    public async Task UTCID01_ReturnsUserAndSortedRolesForExistingId()
    {
        var user = TestData.CreateUser(5, "Staff", "Customer");
        var repository = new Mock<IUserRepository>();
        repository.Setup(repo => repo.GetByIdAsync(5)).ReturnsAsync(user);
        var service = new UserService(repository.Object);

        var result = await service.GetUserAsync(5);

        Assert.NotNull(result);
        Assert.Equal(5, result.UserId);
        Assert.Equal(user.Email, result.Email);
        Assert.Equal(user.FullName, result.FullName);
        Assert.Equal(new[] { "Customer", "Staff" }, result.Roles.Select(role => role.RoleName));
        Assert.True(result.IsActive);
        repository.Verify(repo => repo.GetByIdAsync(5), Times.Once);
    }

    [Fact]
    public async Task UTCID02_ReturnsNullWhenUserDoesNotExist()
    {
        var repository = new Mock<IUserRepository>();
        repository.Setup(repo => repo.GetByIdAsync(999)).ReturnsAsync((AuthenticationAPI.Models.User?)null);
        var service = new UserService(repository.Object);

        var result = await service.GetUserAsync(999);

        Assert.Null(result);
        repository.Verify(repo => repo.GetByIdAsync(999), Times.Once);
    }

    [Fact]
    public async Task UTCID03_ReturnsEmptyRolesForExistingUserWithoutRoles()
    {
        var user = TestData.CreateUser(6);
        var repository = new Mock<IUserRepository>();
        repository.Setup(repo => repo.GetByIdAsync(6)).ReturnsAsync(user);
        var service = new UserService(repository.Object);

        var result = await service.GetUserAsync(6);

        Assert.NotNull(result);
        Assert.Equal(6, result.UserId);
        Assert.Empty(result.Roles);
        repository.Verify(repo => repo.GetByIdAsync(6), Times.Once);
    }

    [Fact]
    public async Task UTCID04_PropagatesRepositoryException()
    {
        var repository = new Mock<IUserRepository>();
        repository.Setup(repo => repo.GetByIdAsync(5)).ThrowsAsync(new InvalidOperationException("Repository failure"));
        var service = new UserService(repository.Object);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetUserAsync(5));

        Assert.Equal("Repository failure", exception.Message);
        repository.Verify(repo => repo.GetByIdAsync(5), Times.Once);
    }
}
