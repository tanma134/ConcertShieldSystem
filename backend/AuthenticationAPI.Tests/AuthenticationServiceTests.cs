using AuthenticationAPI.DTOs;
using AuthenticationAPI.Models;
using AuthenticationAPI.Repositories;
using AuthenticationAPI.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AuthenticationAPI.Tests;

/// <summary>
/// Rules that protect the account: login lockout, OTP brute-force limits, and
/// ending every session when a password changes.
/// </summary>
public class AuthenticationServiceTests
{
    private const string Password = "CorrectHorse1";
    private readonly Mock<IUserRepository> _repo = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly Mock<IAuditLogClient> _audit = new();
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
    private readonly AuthenticationService _svc;

    public AuthenticationServiceTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:SecretKey"] = "SuperSecretKey_MustBe32CharsOrMore!@#",
                ["JwtSettings:Issuer"] = "AuthenticationAPI",
                ["JwtSettings:Audience"] = "AuthenticationAPIUsers",
                ["JwtSettings:AccessTokenExpiryMinutes"] = "60",
                ["JwtSettings:RefreshTokenExpiryDays"] = "7"
            })
            .Build();

        _svc = new AuthenticationService(_repo.Object, _email.Object, config, _cache,
            NullLogger<AuthenticationService>.Instance, _audit.Object);
    }

    private User ActiveUser(int id = 5, string email = "user@example.com") => new()
    {
        UserId = id,
        Email = email,
        FullName = "Test User",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
        IsVerified = true,
        IsActive = true,
        HasPassword = true,
        AuthProvider = "local",
        EkycStatus = "Passed",
        CreatedAt = DateTime.UtcNow,
        UserRoles = new List<UserRole>()
    };

    private LoginDTO Login(string email = "user@example.com", string password = Password)
        => new() { Email = email, Password = password };

    // ------------------------------------------------------------ login lockout

    [Fact]
    public async Task Login_WrongPasswordFiveTimes_ThenCorrectPassword_IsStillRefused()
    {
        _repo.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync(() => ActiveUser());

        for (var i = 0; i < 5; i++)
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _svc.LoginAsync(Login(password: "wrong")));

        // The sixth attempt has the right password, but the account is locked.
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _svc.LoginAsync(Login()));
        Assert.Contains("Too many failed attempts", ex.Message);
    }

    [Fact]
    public async Task Login_WrongPasswordFourTimes_ThenCorrectPassword_Succeeds()
    {
        _repo.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync(() => ActiveUser());
        _repo.Setup(r => r.AddRefreshTokenAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);

        for (var i = 0; i < 4; i++)
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _svc.LoginAsync(Login(password: "wrong")));

        var result = await _svc.LoginAsync(Login());

        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));
    }

    // ------------------------------------------------------------ OTP brute force

    [Fact]
    public async Task VerifyEmail_WrongOtpFiveTimes_DestroysThePendingRegistration()
    {
        var user = ActiveUser();
        _repo.Setup(r => r.GetRoleByNameAsync("Customer"))
            .ReturnsAsync(new Role { RoleId = 1, RoleName = "Customer" });
        _repo.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);

        await _svc.RegisterAsync(new RegisterDTO
        {
            Email = "new@example.com",
            Password = "Password123",
            FullName = "New User"
        });

        for (var i = 0; i < 5; i++)
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _svc.VerifyEmailAsync(new VerifyEmailDTO { Email = "new@example.com", OTP = "000000" }));
        }

        // After the limit the pending registration is gone: even a correct code fails.
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _svc.VerifyEmailAsync(new VerifyEmailDTO { Email = "new@example.com", OTP = "123456" }));
        Assert.Contains("No pending registration", ex.Message);
    }

    // ------------------------------------------------------------ session revocation

    [Fact]
    public async Task ChangePassword_RevokesEveryRefreshToken()
    {
        var user = ActiveUser();
        _repo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(user);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _repo.Setup(r => r.RevokeAllRefreshTokensAsync(5)).ReturnsAsync(3);

        await _svc.ChangePasswordAsync(5, new ChangePasswordDTO
        {
            CurrentPassword = Password,
            NewPassword = "BrandNewPass9",
            ConfirmPassword = "BrandNewPass9"
        });

        _repo.Verify(r => r.RevokeAllRefreshTokensAsync(5), Times.Once);
    }

    [Fact]
    public async Task ResetPassword_RevokesEveryRefreshToken()
    {
        var user = ActiveUser();
        _repo.Setup(r => r.GetByEmailAsync("user@example.com")).ReturnsAsync(user);
        _repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        _repo.Setup(r => r.RevokeAllRefreshTokensAsync(5)).ReturnsAsync(2);

        // Seed the reset token exactly as VerifyResetOtpAsync would.
        const string token = "reset-token-value";
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(token)));
        _cache.Set("reset_token:user@example.com", hash, TimeSpan.FromMinutes(10));

        await _svc.ResetPasswordAsync(new ResetPasswordDTO
        {
            Email = "user@example.com",
            ResetToken = token,
            NewPassword = "BrandNewPass9",
            ConfirmPassword = "BrandNewPass9"
        });

        _repo.Verify(r => r.RevokeAllRefreshTokensAsync(5), Times.Once);
    }

    // ------------------------------------------------------------ password policy

    [Fact]
    public async Task ChangePassword_SevenCharacters_IsRefused()
    {
        _repo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(ActiveUser());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _svc.ChangePasswordAsync(5, new ChangePasswordDTO
            {
                CurrentPassword = Password,
                NewPassword = "Seven77",
                ConfirmPassword = "Seven77"
            }));

        Assert.Contains("at least 8 characters", ex.Message);
    }

    // ------------------------------------------------------------ email normalisation

    [Fact]
    public async Task Login_MixedCaseEmail_IsLowerCaseForLookup()
    {
        _repo.Setup(r => r.GetByEmailAsync("user@example.com")).ReturnsAsync(ActiveUser());
        _repo.Setup(r => r.AddRefreshTokenAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);

        await _svc.LoginAsync(Login(email: "  USER@Example.COM  "));

        _repo.Verify(r => r.GetByEmailAsync("user@example.com"), Times.Once);
    }

    // ------------------------------------------------------------ no demo backdoor

    [Fact]
    public async Task Login_DemoStaffAddressNoLongerCreatesAnAccount()
    {
        _repo.Setup(r => r.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _svc.LoginAsync(Login(email: "staff@ticketbox.com", password: "Staff@123")));

        _repo.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
    }
}
