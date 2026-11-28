using AuthenticationAPI.Models;
using AuthenticationAPI.Repositories;
using AuthenticationAPI.Services;
using Moq;
using Xunit;

namespace AuthenticationAPI.Tests;

public class RoleServiceTests
{
    [Fact]
    public async Task DeleteAsync_DoesNotDeleteAdminRole()
    {
        var repository = new Mock<IRoleRepository>();
        repository.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(new Role { RoleId = 1, RoleName = "Admin" });
        var service = new RoleService(repository.Object);

        var result = await service.DeleteAsync(1);

        Assert.False(result.Success);
        Assert.Equal("The Admin role cannot be deleted.", result.Message);
        repository.Verify(r => r.HasAssignedUsersAsync(It.IsAny<int>()), Times.Never);
        repository.Verify(r => r.Delete(It.IsAny<Role>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_RequiresReassignmentWhenRoleHasUsers()
    {
        var repository = new Mock<IRoleRepository>();
        repository.Setup(r => r.GetByIdAsync(2))
            .ReturnsAsync(new Role { RoleId = 2, RoleName = "Organizer" });
        repository.Setup(r => r.HasAssignedUsersAsync(2)).ReturnsAsync(true);
        var service = new RoleService(repository.Object);

        var result = await service.DeleteAsync(2);

        Assert.False(result.Success);
        Assert.Equal(
            "There are currently users assigned to the Organizer role. Please reassign them to another role before deleting it.",
            result.Message);
        repository.Verify(r => r.Delete(It.IsAny<Role>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_DeletesRoleWhenNoUsersAreAssigned()
    {
        var role = new Role { RoleId = 3, RoleName = "Staff" };
        var repository = new Mock<IRoleRepository>();
        repository.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(role);
        repository.Setup(r => r.HasAssignedUsersAsync(3)).ReturnsAsync(false);
        var service = new RoleService(repository.Object);

        var result = await service.DeleteAsync(3);

        Assert.True(result.Success);
        repository.Verify(r => r.Delete(role), Times.Once);
        repository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_DoesNotModifyAdminRole()
    {
        var repository = new Mock<IRoleRepository>();
        repository.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(new Role { RoleId = 1, RoleName = "Admin" });
        var service = new RoleService(repository.Object);

        var result = await service.UpdateAsync(1, new AuthenticationAPI.DTOs.RoleDTO
        {
            RoleName = "SuperAdmin"
        });

        Assert.False(result.Success);
        Assert.Equal("The Admin role cannot be modified.", result.Message);
        repository.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_CannotRenameAnotherRoleToAdmin()
    {
        var repository = new Mock<IRoleRepository>();
        repository.Setup(r => r.GetByIdAsync(2))
            .ReturnsAsync(new Role { RoleId = 2, RoleName = "Staff" });
        var service = new RoleService(repository.Object);

        var result = await service.UpdateAsync(2, new AuthenticationAPI.DTOs.RoleDTO
        {
            RoleName = " admin "
        });

        Assert.False(result.Success);
        Assert.Equal("The Admin role cannot be modified.", result.Message);
        repository.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_CannotCreateAdminRole()
    {
        var repository = new Mock<IRoleRepository>();
        var service = new RoleService(repository.Object);

        var result = await service.CreateAsync(new AuthenticationAPI.DTOs.RoleDTO
        {
            RoleName = "Admin"
        });

        Assert.False(result.Success);
        Assert.Equal("The Admin role cannot be created or modified.", result.Message);
        repository.Verify(r => r.AddAsync(It.IsAny<Role>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

}
