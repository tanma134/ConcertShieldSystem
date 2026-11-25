using AuthenticationAPI.Models;
using AuthenticationAPI.Repositories;
using AuthenticationAPI.Services;
using Moq;
using Xunit;

namespace AuthenticationAPI.UnitTests;

public sealed class AssignUserRoleTests
{
    [Fact]
    public async Task UTCID01_AssignsStaffRoleAndReturnsSortedRoles()
    {
        var existing = TestData.CreateUser(5, "Customer");
        var updated = TestData.CreateUser(5, "Customer", "Staff");
        var repository = CreateRepository();
        repository.SetupSequence(repo => repo.GetByIdAsync(5))
            .ReturnsAsync(existing)
            .ReturnsAsync(updated);
        repository.Setup(repo => repo.GetRoleByIdAsync(4))
            .ReturnsAsync(new Role { RoleId = 4, RoleName = "Staff" });
        repository.Setup(repo => repo.AddUserRoleAsync(It.IsAny<UserRole>())).Returns(Task.CompletedTask);
        repository.Setup(repo => repo.SaveChangesAsync()).Returns(Task.CompletedTask);

        var result = await new UserService(repository.Object).AssignRoleAsync(5, 4, 1);

        Assert.True(result.Success);
        Assert.Equal("Role assigned successfully.", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(new[] { "Customer", "Staff" }, result.Data.Roles.Select(role => role.RoleName));
        repository.Verify(repo => repo.AddUserRoleAsync(It.Is<UserRole>(role =>
            role.UserId == 5 && role.RoleId == 4 && role.AssignedAt.Kind == DateTimeKind.Utc)), Times.Once);
        repository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID02_AssignsCustomerRoleToUserWithoutRoles()
    {
        var existing = TestData.CreateUser(5);
        var updated = TestData.CreateUser(5, "Customer");
        var repository = CreateRepository();
        repository.SetupSequence(repo => repo.GetByIdAsync(5))
            .ReturnsAsync(existing)
            .ReturnsAsync(updated);
        repository.Setup(repo => repo.GetRoleByIdAsync(2))
            .ReturnsAsync(new Role { RoleId = 2, RoleName = "Customer" });
        repository.Setup(repo => repo.AddUserRoleAsync(It.IsAny<UserRole>())).Returns(Task.CompletedTask);
        repository.Setup(repo => repo.SaveChangesAsync()).Returns(Task.CompletedTask);

        var result = await new UserService(repository.Object).AssignRoleAsync(5, 2, 1);

        Assert.True(result.Success);
        Assert.Equal(new[] { "Customer" }, result.Data!.Roles.Select(role => role.RoleName));
        repository.Verify(repo => repo.AddUserRoleAsync(It.Is<UserRole>(role => role.UserId == 5 && role.RoleId == 2)), Times.Once);
        repository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID03_ReturnsNotFoundWithoutWritingWhenUserDoesNotExist()
    {
        var repository = CreateRepository();
        repository.Setup(repo => repo.GetByIdAsync(999)).ReturnsAsync((User?)null);

        var result = await new UserService(repository.Object).AssignRoleAsync(999, 4, 1);

        Assert.False(result.Success);
        Assert.Equal("User not found.", result.Message);
        Assert.Null(result.Data);
        repository.Verify(repo => repo.GetRoleByIdAsync(It.IsAny<int>()), Times.Never);
        repository.Verify(repo => repo.AddUserRoleAsync(It.IsAny<UserRole>()), Times.Never);
        repository.Verify(repo => repo.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UTCID04_RejectsAdminAccountWithoutWriting()
    {
        var repository = CreateRepository();
        repository.Setup(repo => repo.GetByIdAsync(1)).ReturnsAsync(TestData.CreateUser(1, "Admin"));

        var result = await new UserService(repository.Object).AssignRoleAsync(1, 4, 2);

        Assert.False(result.Success);
        Assert.Equal("Role assignment is not available for Admin or Organizer accounts.", result.Message);
        repository.Verify(repo => repo.GetRoleByIdAsync(It.IsAny<int>()), Times.Never);
        VerifyNoRoleWrites(repository);
    }

    [Fact]
    public async Task UTCID05_RejectsOrganizerAccountWithoutWriting()
    {
        var repository = CreateRepository();
        repository.Setup(repo => repo.GetByIdAsync(3)).ReturnsAsync(TestData.CreateUser(3, "Organizer"));

        var result = await new UserService(repository.Object).AssignRoleAsync(3, 4, 1);

        Assert.False(result.Success);
        Assert.Equal("Role assignment is not available for Admin or Organizer accounts.", result.Message);
        repository.Verify(repo => repo.GetRoleByIdAsync(It.IsAny<int>()), Times.Never);
        VerifyNoRoleWrites(repository);
    }

    [Fact]
    public async Task UTCID06_ReturnsNotFoundForUnknownRole()
    {
        var repository = CreateRepository();
        repository.Setup(repo => repo.GetByIdAsync(5)).ReturnsAsync(TestData.CreateUser(5, "Customer"));
        repository.Setup(repo => repo.GetRoleByIdAsync(77)).ReturnsAsync((Role?)null);

        var result = await new UserService(repository.Object).AssignRoleAsync(5, 77, 1);

        Assert.False(result.Success);
        Assert.Equal("Role not found.", result.Message);
        Assert.Null(result.Data);
        VerifyNoRoleWrites(repository);
    }

    [Fact]
    public async Task UTCID07_RejectsAdminRoleAssignment()
    {
        var repository = CreateRepository();
        repository.Setup(repo => repo.GetByIdAsync(5)).ReturnsAsync(TestData.CreateUser(5, "Customer"));
        repository.Setup(repo => repo.GetRoleByIdAsync(1))
            .ReturnsAsync(new Role { RoleId = 1, RoleName = "Admin" });

        var result = await new UserService(repository.Object).AssignRoleAsync(5, 1, 1);

        Assert.False(result.Success);
        Assert.Equal("Only Customer and Staff roles can be assigned.", result.Message);
        VerifyNoRoleWrites(repository);
    }

    [Fact]
    public async Task UTCID08_RejectsOrganizerRoleAssignment()
    {
        var repository = CreateRepository();
        repository.Setup(repo => repo.GetByIdAsync(5)).ReturnsAsync(TestData.CreateUser(5, "Customer"));
        repository.Setup(repo => repo.GetRoleByIdAsync(3))
            .ReturnsAsync(new Role { RoleId = 3, RoleName = "Organizer" });

        var result = await new UserService(repository.Object).AssignRoleAsync(5, 3, 1);

        Assert.False(result.Success);
        Assert.Equal("Only Customer and Staff roles can be assigned.", result.Message);
        VerifyNoRoleWrites(repository);
    }

    [Fact]
    public async Task UTCID09_RejectsDuplicateRoleAssignment()
    {
        var repository = CreateRepository();
        repository.Setup(repo => repo.GetByIdAsync(5)).ReturnsAsync(TestData.CreateUser(5, "Customer"));
        repository.Setup(repo => repo.GetRoleByIdAsync(2))
            .ReturnsAsync(new Role { RoleId = 2, RoleName = "Customer" });

        var result = await new UserService(repository.Object).AssignRoleAsync(5, 2, 1);

        Assert.False(result.Success);
        Assert.Equal("This user already has this role.", result.Message);
        VerifyNoRoleWrites(repository);
    }

    [Fact]
    public async Task UTCID10_ReturnsSuccessWithNullDataWhenRefreshedUserDisappears()
    {
        var repository = CreateRepository();
        repository.SetupSequence(repo => repo.GetByIdAsync(5))
            .ReturnsAsync(TestData.CreateUser(5, "Customer"))
            .ReturnsAsync((User?)null);
        repository.Setup(repo => repo.GetRoleByIdAsync(4))
            .ReturnsAsync(new Role { RoleId = 4, RoleName = "Staff" });
        repository.Setup(repo => repo.AddUserRoleAsync(It.IsAny<UserRole>())).Returns(Task.CompletedTask);
        repository.Setup(repo => repo.SaveChangesAsync()).Returns(Task.CompletedTask);

        var result = await new UserService(repository.Object).AssignRoleAsync(5, 4, 1);

        Assert.True(result.Success);
        Assert.Equal("Role assigned successfully.", result.Message);
        Assert.Null(result.Data);
        repository.Verify(repo => repo.AddUserRoleAsync(It.Is<UserRole>(role => role.UserId == 5 && role.RoleId == 4)), Times.Once);
        repository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    private static Mock<IUserRepository> CreateRepository() => new();

    private static void VerifyNoRoleWrites(Mock<IUserRepository> repository)
    {
        repository.Verify(repo => repo.AddUserRoleAsync(It.IsAny<UserRole>()), Times.Never);
        repository.Verify(repo => repo.SaveChangesAsync(), Times.Never);
    }
}
