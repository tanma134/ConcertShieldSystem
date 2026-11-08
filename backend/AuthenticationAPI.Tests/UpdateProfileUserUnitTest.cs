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

public class UpdateProfileUserUnitTest
{
    [Fact]
    public async Task UTCID01_UpdateProfileUser_ReturnsBadRequest_WhenRequestIsNull()
    {
        var service = new Mock<IProfileService>();
        var controller = CreateController(service.Object);

        var result = await controller.UpdateProfile(null!);

        AssertError(result, 400, "Profile update request is required.");
        service.Verify(
            s => s.UpdateProfileAsync(
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<UpdateProfileDTO>()),
            Times.Never);
    }

    [Fact]
    public async Task UTCID02_UpdateProfileUser_ReturnsOk_WhenServiceUpdatesProfile()
    {
        var profile = new ProfileDTO
        {
            UserId = 7,
            Email = "alice@example.com",
            FullName = "Alice Nguyen",
            PhoneNumber = "0912345678"
        };
        var service = new Mock<IProfileService>();
        service
            .Setup(s => s.UpdateProfileAsync(
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<UpdateProfileDTO>()))
            .ReturnsAsync((true, "Profile updated successfully.", profile));
        var controller = CreateController(service.Object);

        var result = await controller.UpdateProfile(CreateValidDto());

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(profile, ok.Value);
        service.Verify(
            s => s.UpdateProfileAsync(
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<UpdateProfileDTO>()),
            Times.Once);
    }

    [Fact]
    public async Task UTCID03_UpdateProfileUser_ReturnsNotFound_WhenServiceCannotFindUser()
    {
        var service = new Mock<IProfileService>();
        service
            .Setup(s => s.UpdateProfileAsync(
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<UpdateProfileDTO>()))
            .ReturnsAsync((false, "User not found.", (ProfileDTO?)null));
        var controller = CreateController(service.Object);

        var result = await controller.UpdateProfile(CreateValidDto());

        AssertError(result, 404, "User not found.");
    }

    [Fact]
    public async Task UTCID04_UpdateProfileUser_ReturnsBadRequest_WhenServiceRejectsInput()
    {
        var service = new Mock<IProfileService>();
        service
            .Setup(s => s.UpdateProfileAsync(
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<UpdateProfileDTO>()))
            .ReturnsAsync((false, "Email is required.", (ProfileDTO?)null));
        var controller = CreateController(service.Object);

        var result = await controller.UpdateProfile(CreateValidDto());

        AssertError(result, 400, "Email is required.");
    }

    [Fact]
    public async Task UTCID05_UpdateProfileUser_ReturnsInternalServerError_WhenServiceThrows()
    {
        var service = new Mock<IProfileService>();
        service
            .Setup(s => s.UpdateProfileAsync(
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<UpdateProfileDTO>()))
            .ThrowsAsync(new InvalidOperationException("Repository unavailable."));
        var controller = CreateController(service.Object);

        var result = await controller.UpdateProfile(CreateValidDto());

        AssertError(
            result,
            500,
            "An unexpected error occurred while updating the profile. Please try again later.");
    }

    [Fact]
    public async Task UTCID06_UpdateProfileUser_UpdatesNameAndPhone_WhenRequestIsValid()
    {
        var user = CreateUser();
        var repository = CreateRepositoryWithUser(user);
        repository
            .Setup(r => r.GetByPhoneAsync("0912345678"))
            .ReturnsAsync((User?)null);
        var service = CreateService(repository);
        var dto = CreateValidDto();
        dto.FullName = "  José Alvarez  ";
        dto.PhoneNumber = "0912345678";
        dto.Email = " ALICE@example.com ";

        var result = await service.UpdateProfileAsync(null, "7", dto);

        Assert.True(result.Success);
        Assert.Equal("Profile updated successfully.", result.Message);
        Assert.Equal("José Alvarez", user.FullName);
        Assert.Equal("0912345678", user.PhoneNumber);
        Assert.Equal("José Alvarez", result.Profile?.FullName);
        Assert.Equal("0912345678", result.Profile?.PhoneNumber);
        repository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID07_UpdateProfileUser_RejectsNullEmail()
    {
        var dto = CreateValidDto();
        dto.Email = null;

        await AssertRejectedBeforeUserLookup(dto, "Email is required.");
    }

    [Fact]
    public async Task UTCID08_UpdateProfileUser_RejectsWhitespaceEmail()
    {
        var dto = CreateValidDto();
        dto.Email = "   ";

        await AssertRejectedBeforeUserLookup(dto, "Email is required.");
    }

    [Fact]
    public async Task UTCID09_UpdateProfileUser_RejectsNullFullName()
    {
        var dto = CreateValidDto();
        dto.FullName = null;

        await AssertRejectedBeforeUserLookup(
            dto,
            "Full name is required.");
    }

    [Fact]
    public async Task UTCID10_UpdateProfileUser_RejectsEmptyFullName()
    {
        var dto = CreateValidDto();
        dto.FullName = "";

        await AssertRejectedBeforeUserLookup(
            dto,
            "Full name is required.");
    }

    [Fact]
    public async Task UTCID11_UpdateProfileUser_RejectsFullNameShorterThanTwoCharacters()
    {
        var dto = CreateValidDto();
        dto.FullName = "A";

        await AssertRejectedBeforeUserLookup(
            dto,
            "Full name must be 2 to 100 letters and spaces only.");
    }

    [Fact]
    public async Task UTCID12_UpdateProfileUser_RejectsFullNameLongerThanOneHundredCharacters()
    {
        var dto = CreateValidDto();
        dto.FullName = new string('A', 101);

        await AssertRejectedBeforeUserLookup(
            dto,
            "Full name must be 2 to 100 letters and spaces only.");
    }

    [Fact]
    public async Task UTCID13_UpdateProfileUser_RejectsFullNameContainingNonLetters()
    {
        var dto = CreateValidDto();
        dto.FullName = "Alice2";

        await AssertRejectedBeforeUserLookup(
            dto,
            "Full name must be 2 to 100 letters and spaces only.");
    }

    [Fact]
    public async Task UTCID14_UpdateProfileUser_RejectsNullPhoneNumber()
    {
        var dto = CreateValidDto();
        dto.PhoneNumber = null;

        await AssertRejectedBeforeUserLookup(
            dto,
            "Phone number is required.");
    }

    [Fact]
    public async Task UTCID15_UpdateProfileUser_RejectsWhitespacePhoneNumber()
    {
        var dto = CreateValidDto();
        dto.PhoneNumber = "   ";

        await AssertRejectedBeforeUserLookup(
            dto,
            "Phone number is required.");
    }

    [Fact]
    public async Task UTCID16_UpdateProfileUser_RejectsPhoneNumberShorterThanTenDigits()
    {
        var dto = CreateValidDto();
        dto.PhoneNumber = "091234567";

        await AssertRejectedBeforeUserLookup(
            dto,
            "Phone number must be 10 to 15 digits and start with 03, 05, 07, 08, or 09.");
    }

    [Fact]
    public async Task UTCID17_UpdateProfileUser_RejectsPhoneNumberLongerThanFifteenDigits()
    {
        var dto = CreateValidDto();
        dto.PhoneNumber = "0912345678901234";

        await AssertRejectedBeforeUserLookup(
            dto,
            "Phone number must be 10 to 15 digits and start with 03, 05, 07, 08, or 09.");
    }

    [Fact]
    public async Task UTCID18_UpdateProfileUser_RejectsPhoneNumberWithUnsupportedPrefix()
    {
        var dto = CreateValidDto();
        dto.PhoneNumber = "0612345678";

        await AssertRejectedBeforeUserLookup(
            dto,
            "Phone number must be 10 to 15 digits and start with 03, 05, 07, 08, or 09.");
    }

    [Fact]
    public async Task UTCID19_UpdateProfileUser_ReturnsUserNotFound_WhenNoCurrentUserMatchesClaims()
    {
        var repository = new Mock<IUserRepository>();
        repository.Setup(r => r.GetByIdAsync(404)).ReturnsAsync((User?)null);
        var service = CreateService(repository);

        var result = await service.UpdateProfileAsync(null, "404", CreateValidDto());

        Assert.False(result.Success);
        Assert.Equal("User not found.", result.Message);
        Assert.Null(result.Profile);
        repository.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UTCID20_UpdateProfileUser_RejectsEmailDifferentFromCurrentUserEmail()
    {
        var user = CreateUser();
        var repository = CreateRepositoryWithUser(user);
        var service = CreateService(repository);
        var dto = CreateValidDto();
        dto.Email = "other@example.com";

        var result = await service.UpdateProfileAsync(null, "7", dto);

        Assert.False(result.Success);
        Assert.Equal("Email cannot be changed.", result.Message);
        Assert.Null(result.Profile);
        repository.Verify(r => r.GetByPhoneAsync(It.IsAny<string>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UTCID21_UpdateProfileUser_RejectsPhoneNumberAlreadyUsedByAnotherUser()
    {
        var user = CreateUser();
        var otherUser = new User { UserId = 8 };
        var repository = CreateRepositoryWithUser(user);
        repository
            .Setup(r => r.GetByPhoneAsync("0912345678"))
            .ReturnsAsync(otherUser);
        var service = CreateService(repository);
        var dto = CreateValidDto();
        dto.PhoneNumber = "0912345678";

        var result = await service.UpdateProfileAsync(null, "7", dto);

        Assert.False(result.Success);
        Assert.Equal("Phone number already exists.", result.Message);
        Assert.Null(result.Profile);
        Assert.Equal("0987654321", user.PhoneNumber);
        repository.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UTCID22_UpdateProfileUser_DoesNotCheckPhoneUniqueness_WhenPhoneIsUnchanged()
    {
        var user = CreateUser();
        var repository = CreateRepositoryWithUser(user);
        var service = CreateService(repository);
        var dto = CreateValidDto();
        dto.PhoneNumber = user.PhoneNumber;

        var result = await service.UpdateProfileAsync(null, "7", dto);

        Assert.True(result.Success);
        Assert.Equal(user.PhoneNumber, result.Profile?.PhoneNumber);
        repository.Verify(r => r.GetByPhoneAsync(It.IsAny<string>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID23_UpdateProfileUser_AcceptsFullNameAtTwoCharacterMinimum()
    {
        var dto = CreateValidDto();
        dto.FullName = "Al";

        var result = await UpdateSuccessfully(dto);

        Assert.True(result.Success);
        Assert.Equal("Al", result.Profile?.FullName);
    }

    [Fact]
    public async Task UTCID24_UpdateProfileUser_AcceptsFullNameAtOneHundredCharacterMaximum()
    {
        var dto = CreateValidDto();
        dto.FullName = new string('A', 100);

        var result = await UpdateSuccessfully(dto);

        Assert.True(result.Success);
        Assert.Equal(dto.FullName, result.Profile?.FullName);
    }

    [Fact]
    public async Task UTCID25_UpdateProfileUser_AcceptsPhoneNumberAtFifteenDigitMaximum()
    {
        var dto = CreateValidDto();
        dto.PhoneNumber = "091234567890123";

        var result = await UpdateSuccessfully(dto);

        Assert.True(result.Success);
        Assert.Equal(dto.PhoneNumber, result.Profile?.PhoneNumber);
    }

    private static ProfileController CreateController(IProfileService service) =>
        new(service, NullLogger<ProfileController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

    private static ProfileService CreateService(Mock<IUserRepository> repository) =>
        new(repository.Object, Mock.Of<IAvatarStorageService>());

    private static Mock<IUserRepository> CreateRepositoryWithUser(User user)
    {
        var repository = new Mock<IUserRepository>();
        repository.Setup(r => r.GetByIdAsync(user.UserId)).ReturnsAsync(user);
        return repository;
    }

    private static User CreateUser() =>
        new()
        {
            UserId = 7,
            Email = "alice@example.com",
            FullName = "Alice Oldname",
            PhoneNumber = "0987654321",
            EkycStatus = "NotStarted",
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UserRoles = []
        };

    private static UpdateProfileDTO CreateValidDto() =>
        new()
        {
            Email = "alice@example.com",
            FullName = "Alice Nguyen",
            PhoneNumber = "0987654321"
        };

    private static async Task AssertRejectedBeforeUserLookup(
        UpdateProfileDTO dto,
        string expectedMessage)
    {
        var repository = new Mock<IUserRepository>(MockBehavior.Strict);
        var service = CreateService(repository);

        var result = await service.UpdateProfileAsync(null, "7", dto);

        Assert.False(result.Success);
        Assert.Equal(expectedMessage, result.Message);
        Assert.Null(result.Profile);
        repository.VerifyNoOtherCalls();
    }

    private static async Task<(bool Success, string Message, ProfileDTO? Profile)>
        UpdateSuccessfully(UpdateProfileDTO dto)
    {
        var user = CreateUser();
        var repository = CreateRepositoryWithUser(user);
        repository
            .Setup(r => r.GetByPhoneAsync(dto.PhoneNumber!))
            .ReturnsAsync((User?)null);
        var service = CreateService(repository);

        return await service.UpdateProfileAsync(null, "7", dto);
    }

    private static void AssertError(
        IActionResult result,
        int expectedStatusCode,
        string expectedMessage)
    {
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(expectedStatusCode, objectResult.StatusCode);
        Assert.Equal(
            expectedMessage,
            objectResult.Value?.GetType().GetProperty("message")?.GetValue(objectResult.Value));
    }
}
