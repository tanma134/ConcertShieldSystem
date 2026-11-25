using AuthenticationAPI.Models;
using AuthenticationAPI.Repositories;
using AuthenticationAPI.Services;
using Moq;
using Xunit;

namespace AuthenticationAPI.UnitTests;

public sealed class UpdateAccountStatusTests
{
    [Fact]
    public async Task UTCID01_BlocksActiveCustomer()
    {
        var user = TestData.CreateUser(5, "Customer");
        var repository = CreateRepository(user);
        var result = await new UserService(repository.Object).UpdateUserStatusAsync(5, 2, 1);

        Assert.Equal((true, "User blocked.", (bool?)false), result);
        Assert.False(user.IsActive);
        repository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID02_UnlocksBlockedCustomer()
    {
        var user = TestData.CreateUser(5, "Customer");
        user.IsActive = false;
        var repository = CreateRepository(user);

        var result = await new UserService(repository.Object).UpdateUserStatusAsync(5, 1, 1);

        Assert.Equal((true, "User unlocked.", (bool?)true), result);
        Assert.True(user.IsActive);
        repository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID03_UnlocksAlreadyActiveCustomerSuccessfully()
    {
        var user = TestData.CreateUser(5, "Customer");
        var repository = CreateRepository(user);

        var result = await new UserService(repository.Object).UpdateUserStatusAsync(5, 1, 1);

        Assert.Equal((true, "User unlocked.", (bool?)true), result);
        Assert.True(user.IsActive);
        repository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID04_BlocksAlreadyBlockedCustomerSuccessfully()
    {
        var user = TestData.CreateUser(5, "Customer");
        user.IsActive = false;
        var repository = CreateRepository(user);

        var result = await new UserService(repository.Object).UpdateUserStatusAsync(5, 2, 1);

        Assert.Equal((true, "User blocked.", (bool?)false), result);
        Assert.False(user.IsActive);
        repository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID05_BlocksActiveStaff()
    {
        var user = TestData.CreateUser(5, "Staff");
        var repository = CreateRepository(user);

        var result = await new UserService(repository.Object).UpdateUserStatusAsync(5, 2, 1);

        Assert.Equal((true, "User blocked.", (bool?)false), result);
        Assert.False(user.IsActive);
        repository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID06_RejectsZeroStatusWithoutSaving()
    {
        var user = TestData.CreateUser(5, "Customer");
        var repository = CreateRepository(user);

        var result = await new UserService(repository.Object).UpdateUserStatusAsync(5, 0, 1);

        Assert.Equal((false, "Invalid status. 1 = Unlock, 2 = Block.", (bool?)null), result);
        Assert.True(user.IsActive);
        repository.Verify(repo => repo.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UTCID07_RejectsStatusAboveSupportedRangeWithoutSaving()
    {
        var user = TestData.CreateUser(5, "Customer");
        var repository = CreateRepository(user);

        var result = await new UserService(repository.Object).UpdateUserStatusAsync(5, 3, 1);

        Assert.Equal((false, "Invalid status. 1 = Unlock, 2 = Block.", (bool?)null), result);
        Assert.True(user.IsActive);
        repository.Verify(repo => repo.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UTCID08_RejectsNegativeStatusWithoutSaving()
    {
        var user = TestData.CreateUser(5, "Customer");
        var repository = CreateRepository(user);

        var result = await new UserService(repository.Object).UpdateUserStatusAsync(5, -1, 1);

        Assert.Equal((false, "Invalid status. 1 = Unlock, 2 = Block.", (bool?)null), result);
        Assert.True(user.IsActive);
        repository.Verify(repo => repo.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UTCID09_ReturnsNotFoundWhenUserDoesNotExist()
    {
        var repository = new Mock<IUserRepository>();
        repository.Setup(repo => repo.GetByIdAsync(999)).ReturnsAsync((User?)null);

        var result = await new UserService(repository.Object).UpdateUserStatusAsync(999, 2, 1);

        Assert.Equal((false, "User not found.", (bool?)null), result);
        repository.Verify(repo => repo.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UTCID10_RejectsBlockingAdminAccount()
    {
        var admin = TestData.CreateUser(1, "Admin");
        var repository = CreateRepository(admin);

        var result = await new UserService(repository.Object).UpdateUserStatusAsync(1, 2, 2);

        Assert.Equal((false, "Admin accounts cannot be blocked or unblocked.", (bool?)null), result);
        Assert.True(admin.IsActive);
        repository.Verify(repo => repo.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UTCID11_RejectsInvalidStatusForAdminBeforeStatusValidation()
    {
        var admin = TestData.CreateUser(1, "Admin");
        var repository = CreateRepository(admin);

        var result = await new UserService(repository.Object).UpdateUserStatusAsync(1, 3, 2);

        Assert.Equal((false, "Admin accounts cannot be blocked or unblocked.", (bool?)null), result);
        repository.Verify(repo => repo.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UTCID12_PropagatesSaveChangesException()
    {
        var user = TestData.CreateUser(5, "Customer");
        var repository = CreateRepository(user);
        repository.Setup(repo => repo.SaveChangesAsync()).ThrowsAsync(new InvalidOperationException("Save failed"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new UserService(repository.Object).UpdateUserStatusAsync(5, 2, 1));

        Assert.Equal("Save failed", exception.Message);
        Assert.False(user.IsActive);
        repository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    private static Mock<IUserRepository> CreateRepository(User user)
    {
        var repository = new Mock<IUserRepository>();
        repository.Setup(repo => repo.GetByIdAsync(user.UserId)).ReturnsAsync(user);
        repository.Setup(repo => repo.SaveChangesAsync()).Returns(Task.CompletedTask);
        return repository;
    }
}
