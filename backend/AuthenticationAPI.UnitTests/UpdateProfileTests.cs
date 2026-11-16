using AuthenticationAPI.DTOs;
using AuthenticationAPI.Models;
using AuthenticationAPI.Repositories;
using AuthenticationAPI.Services;
using Moq;
using Xunit;

namespace AuthenticationAPI.Tests;

public class UpdateProfileTests
{
    [Fact]
    public async Task UTCID01_UpdateProfileAsync_UpdatesAllProvidedFields()
    {
        var (repository, service, user) = CreateService();
        repository.Setup(r => r.GetByEmailAsync("new.mail@mail.com")).ReturnsAsync((User?)null);
        repository.Setup(r => r.GetByPhoneAsync("0912345678")).ReturnsAsync((User?)null);

        var result = await service.UpdateProfileAsync(
            "an@mail.com",
            "5",
            CreateDto(" New.Mail@Mail.com ", "0912345678", "Nguyen Van A", "https://cdn.example.com/a.png"));

        Assert.True(result.Success);
        Assert.Equal("Profile updated successfully.", result.Message);
        Assert.NotNull(result.Profile);
        Assert.Equal("new.mail@mail.com", user.Email);
        Assert.Equal("0912345678", user.PhoneNumber);
        Assert.Equal("Nguyen Van A", user.FullName);
        Assert.Equal("https://cdn.example.com/a.png", user.AvatarUrl);
        Assert.Equal(user.Email, result.Profile.Email);
        repository.Verify(r => r.GetByEmailAsync("new.mail@mail.com"), Times.Once);
        repository.Verify(r => r.GetByPhoneAsync("0912345678"), Times.Once);
        repository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID02_UpdateProfileAsync_UpdatesNonblankNameAndIgnoresBlankFields()
    {
        var (repository, service, user) = CreateService();
        var originalEmail = user.Email;
        var originalPhone = user.PhoneNumber;
        var originalAvatar = user.AvatarUrl;

        var result = await service.UpdateProfileAsync(
            "an@mail.com",
            "5",
            CreateDto(null, null, "Nguyen Van A", null));

        Assert.True(result.Success);
        Assert.Equal("Nguyen Van A", user.FullName);
        Assert.Equal(originalEmail, user.Email);
        Assert.Equal(originalPhone, user.PhoneNumber);
        Assert.Equal(originalAvatar, user.AvatarUrl);
        repository.Verify(r => r.GetByEmailAsync(It.IsAny<string>()), Times.Never);
        repository.Verify(r => r.GetByPhoneAsync(It.IsAny<string>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID03_UpdateProfileAsync_UpdatesAvatarWhenOtherFieldsAreBlank()
    {
        var (repository, service, user) = CreateService();
        var originalEmail = user.Email;
        var originalPhone = user.PhoneNumber;
        var originalName = user.FullName;

        var result = await service.UpdateProfileAsync(
            "an@mail.com",
            "5",
            CreateDto(null, null, null, "https://cdn.example.com/a.png"));

        Assert.True(result.Success);
        Assert.Equal("https://cdn.example.com/a.png", user.AvatarUrl);
        Assert.Equal(originalEmail, user.Email);
        Assert.Equal(originalPhone, user.PhoneNumber);
        Assert.Equal(originalName, user.FullName);
        repository.Verify(r => r.GetByEmailAsync(It.IsAny<string>()), Times.Never);
        repository.Verify(r => r.GetByPhoneAsync(It.IsAny<string>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID04_UpdateProfileAsync_LeavesUnchangedEmailAndPhoneAlone()
    {
        var (repository, service, user) = CreateService();
        var originalEmail = user.Email;
        var originalPhone = user.PhoneNumber;

        var result = await service.UpdateProfileAsync(
            "an@mail.com",
            "5",
            CreateDto(" AN@MAIL.COM ", originalPhone, "Nguyen Van A", null));

        Assert.True(result.Success);
        Assert.Equal(originalEmail, user.Email);
        Assert.Equal(originalPhone, user.PhoneNumber);
        Assert.Equal("Nguyen Van A", user.FullName);
        repository.Verify(r => r.GetByEmailAsync(It.IsAny<string>()), Times.Never);
        repository.Verify(r => r.GetByPhoneAsync(It.IsAny<string>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID05_UpdateProfileAsync_ReturnsUserNotFound_WhenClaimsDoNotFindUser()
    {
        var repository = new Mock<IUserRepository>();
        repository.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((User?)null);
        repository.Setup(r => r.GetByEmailAsync("missing@mail.com")).ReturnsAsync((User?)null);
        var service = CreateService(repository);

        var result = await service.UpdateProfileAsync(
            "missing@mail.com",
            "999",
            CreateDto("new.mail@mail.com", "0912345678", "Nguyen Van A", "https://cdn.example.com/a.png"));

        Assert.False(result.Success);
        Assert.Equal("User not found.", result.Message);
        Assert.Null(result.Profile);
        repository.Verify(r => r.GetByEmailAsync("missing@mail.com"), Times.Once);
        repository.Verify(r => r.GetByEmailAsync("new.mail@mail.com"), Times.Never);
        repository.Verify(r => r.GetByPhoneAsync(It.IsAny<string>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UTCID06_UpdateProfileAsync_RejectsDuplicateEmailWithoutChangingUser()
    {
        var (repository, service, user) = CreateService();
        var otherUser = CreateUser(9);
        repository.Setup(r => r.GetByEmailAsync("new.mail@mail.com")).ReturnsAsync(otherUser);

        var result = await service.UpdateProfileAsync(
            "an@mail.com",
            "5",
            CreateDto("new.mail@mail.com", "0912345678", "Nguyen Van A", "https://cdn.example.com/a.png"));

        Assert.False(result.Success);
        Assert.Equal("Email already exists.", result.Message);
        Assert.Null(result.Profile);
        Assert.Equal("an@mail.com", user.Email);
        Assert.Equal("0901000001", user.PhoneNumber);
        Assert.Equal("Old Name", user.FullName);
        Assert.Null(user.AvatarUrl);
        repository.Verify(r => r.GetByPhoneAsync(It.IsAny<string>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UTCID07_UpdateProfileAsync_RejectsDuplicatePhoneWithoutChangingUser()
    {
        var (repository, service, user) = CreateService();
        var otherUser = CreateUser(9);
        repository.Setup(r => r.GetByEmailAsync("new.mail@mail.com")).ReturnsAsync((User?)null);
        repository.Setup(r => r.GetByPhoneAsync("0912345678")).ReturnsAsync(otherUser);

        var result = await service.UpdateProfileAsync(
            "an@mail.com",
            "5",
            CreateDto("new.mail@mail.com", "0912345678", "Nguyen Van A", "https://cdn.example.com/a.png"));

        Assert.False(result.Success);
        Assert.Equal("Phone number already exists.", result.Message);
        Assert.Null(result.Profile);
        Assert.Equal("an@mail.com", user.Email);
        Assert.Equal("0901000001", user.PhoneNumber);
        Assert.Equal("Old Name", user.FullName);
        Assert.Null(user.AvatarUrl);
        repository.Verify(r => r.GetByEmailAsync("new.mail@mail.com"), Times.Once);
        repository.Verify(r => r.GetByPhoneAsync("0912345678"), Times.Once);
        repository.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UTCID08_UpdateProfileAsync_UpdatesPhoneWhenOtherFieldsAreBlank()
    {
        var (repository, service, user) = CreateService();
        repository.Setup(r => r.GetByPhoneAsync("0912345678")).ReturnsAsync((User?)null);

        var result = await service.UpdateProfileAsync(
            "an@mail.com",
            "5",
            CreateDto(null, "0912345678", null, null));

        Assert.True(result.Success);
        Assert.Equal("0912345678", user.PhoneNumber);
        Assert.Equal("an@mail.com", user.Email);
        Assert.Equal("Old Name", user.FullName);
        repository.Verify(r => r.GetByEmailAsync(It.IsAny<string>()), Times.Never);
        repository.Verify(r => r.GetByPhoneAsync("0912345678"), Times.Once);
        repository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID09_UpdateProfileAsync_SucceedsWithoutChanges_WhenAllFieldsAreNull()
    {
        var (repository, service, user) = CreateService();

        var result = await service.UpdateProfileAsync(
            "an@mail.com",
            "5",
            CreateDto(null, null, null, null));

        Assert.True(result.Success);
        Assert.Equal("an@mail.com", result.Profile!.Email);
        Assert.Equal("0901000001", result.Profile.PhoneNumber);
        Assert.Equal("Old Name", result.Profile.FullName);
        Assert.Null(result.Profile.AvatarUrl);
        repository.Verify(r => r.GetByEmailAsync(It.IsAny<string>()), Times.Never);
        repository.Verify(r => r.GetByPhoneAsync(It.IsAny<string>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID10_UpdateProfileAsync_IgnoresWhitespaceFields()
    {
        var (repository, service, user) = CreateService();

        var result = await service.UpdateProfileAsync(
            "an@mail.com",
            "5",
            CreateDto(" ", null, "  ", " "));

        Assert.True(result.Success);
        Assert.Equal("an@mail.com", user.Email);
        Assert.Equal("0901000001", user.PhoneNumber);
        Assert.Equal("Old Name", user.FullName);
        Assert.Null(user.AvatarUrl);
        repository.Verify(r => r.GetByEmailAsync(It.IsAny<string>()), Times.Never);
        repository.Verify(r => r.GetByPhoneAsync(It.IsAny<string>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID11_UpdateProfileAsync_IgnoresEmptyFields()
    {
        var (repository, service, user) = CreateService();

        var result = await service.UpdateProfileAsync(
            "an@mail.com",
            "5",
            CreateDto(null, "", null, null));

        Assert.True(result.Success);
        Assert.Equal("an@mail.com", user.Email);
        Assert.Equal("0901000001", user.PhoneNumber);
        repository.Verify(r => r.GetByPhoneAsync(It.IsAny<string>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UTCID12_UpdateProfileAsync_RejectsNonNumericPhone()
    {
        var (repository, service, user) = CreateService();

        var result = await service.UpdateProfileAsync(
            "an@mail.com",
            "5",
            CreateDto(null, "abc", "Nguyen Van A", null));

        Assert.False(result.Success);
        Assert.Equal("Phone number must be 10 to 15 digits and start with 03, 05, 07, 08, or 09.", result.Message);
        Assert.Null(result.Profile);
        Assert.Equal("0901000001", user.PhoneNumber);
        repository.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UTCID13_UpdateProfileAsync_RejectsPhoneShorterThanTenDigits()
    {
        var (repository, service, user) = CreateService();

        var result = await service.UpdateProfileAsync(
            "an@mail.com",
            "5",
            CreateDto(null, "035", "Nguyen Van A", null));

        Assert.False(result.Success);
        Assert.Equal("Phone number must be 10 to 15 digits and start with 03, 05, 07, 08, or 09.", result.Message);
        Assert.Null(result.Profile);
        Assert.Equal("0901000001", user.PhoneNumber);
        repository.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UTCID14_UpdateProfileAsync_RejectsPhoneWithUnsupportedPrefix()
    {
        var (repository, service, user) = CreateService();

        var result = await service.UpdateProfileAsync(
            "an@mail.com",
            "5",
            CreateDto(null, "0123456789", "Nguyen Van A", null));

        Assert.False(result.Success);
        Assert.Equal("Phone number must be 10 to 15 digits and start with 03, 05, 07, 08, or 09.", result.Message);
        Assert.Null(result.Profile);
        Assert.Equal("0901000001", user.PhoneNumber);
        repository.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UTCID15_UpdateProfileAsync_RejectsFullNameShorterThanTwoCharacters()
    {
        var (repository, service, user) = CreateService();

        var result = await service.UpdateProfileAsync(
            "an@mail.com",
            "5",
            CreateDto(null, "0912345678", "a", null));

        Assert.False(result.Success);
        Assert.Equal("Full name must be 2 to 100 letters and spaces only.", result.Message);
        Assert.Null(result.Profile);
        Assert.Equal("Old Name", user.FullName);
        repository.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
        repository.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    private static (Mock<IUserRepository> Repository, ProfileService Service, User User) CreateService()
    {
        var repository = new Mock<IUserRepository>();
        var user = CreateUser(5);
        repository.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(user);
        repository.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        var service = new ProfileService(repository.Object, Mock.Of<IAvatarStorageService>());
        return (repository, service, user);
    }

    private static ProfileService CreateService(Mock<IUserRepository> repository) =>
        new(repository.Object, Mock.Of<IAvatarStorageService>());

    private static UpdateProfileDTO CreateDto(
        string? email,
        string? phoneNumber,
        string? fullName,
        string? avatarUrl) =>
        new()
        {
            Email = email,
            PhoneNumber = phoneNumber,
            FullName = fullName,
            AvatarUrl = avatarUrl
        };

    private static User CreateUser(int userId = 5) =>
        new()
        {
            UserId = userId,
            Email = userId == 5 ? "an@mail.com" : "other@mail.com",
            FullName = "Old Name",
            PhoneNumber = userId == 5 ? "0901000001" : "0999999999",
            AvatarUrl = null,
            IsVerified = true,
            IsActive = true,
            EkycStatus = "Verified",
            CreatedAt = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc)
        };
}
