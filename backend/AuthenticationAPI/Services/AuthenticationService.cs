using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AuthenticationAPI.DTOs;
using AuthenticationAPI.Models;
using AuthenticationAPI.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;

namespace AuthenticationAPI.Services
{
    public class AuthenticationService : IAuthenticationService
    {
        private readonly IUserRepository _userRepository;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AuthenticationService> _logger;

        private const string DefaultRoleName = "User";

        // Shared message for the reset-password flow, so we don't leak
        // whether a given email exists in the system.
        private const string GenericOtpInvalidMessage = "Invalid or expired OTP.";

        public AuthenticationService(
            IUserRepository userRepository,
            IEmailService emailService,
            IConfiguration configuration,
            IMemoryCache cache,
            ILogger<AuthenticationService> logger)
        {
            _userRepository = userRepository;
            _emailService = emailService;
            _configuration = configuration;
            _cache = cache;
            _logger = logger;
        }

        public async Task RegisterAsync(RegisterDTO dto)
        {
            var existingUser = await _userRepository.GetByEmailAsync(dto.Email);
            if (existingUser != null)
            {
                _logger.LogWarning("Registration failed: email {Email} already exists.", dto.Email);
                throw new InvalidOperationException("Email already exists.");
            }

            string cacheKey = $"pending_register:{dto.Email}";
            string otp = GenerateOTP();
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            var pending = new PendingRegistration
            {
                FullName = dto.FullName,
                Email = dto.Email,
                PhoneNumber = dto.PhoneNumber,
                PasswordHash = passwordHash,
                OtpCode = otp,
                ExpiredAt = DateTime.UtcNow.AddMinutes(5)
            };
            _cache.Set(cacheKey, pending, TimeSpan.FromMinutes(5));

            await _emailService.SendOTPAsync(dto.Email, otp, EmailPurpose.Register);
            _logger.LogInformation("Registration OTP sent to {Email}.", dto.Email);
        }

        public async Task VerifyEmailAsync(VerifyEmailDTO dto)
        {
            string cacheKey = $"pending_register:{dto.Email}";
            if (!_cache.TryGetValue(cacheKey, out PendingRegistration? pending) || pending == null)
            {
                _logger.LogWarning("Email verification failed: no pending registration for {Email}.", dto.Email);
                throw new KeyNotFoundException("No pending registration found for this email. Please register again.");
            }

            if (pending.OtpCode != dto.OTP)
            {
                _logger.LogWarning("Email verification failed: invalid OTP for {Email}.", dto.Email);
                throw new UnauthorizedAccessException("Invalid OTP.");
            }

            if (pending.ExpiredAt < DateTime.UtcNow)
            {
                _cache.Remove(cacheKey);
                _logger.LogWarning("Email verification failed: OTP expired for {Email}.", dto.Email);
                throw new UnauthorizedAccessException("OTP has expired. Please register again.");
            }

            var role = await _userRepository.GetRoleByNameAsync(DefaultRoleName)
                ?? throw new InvalidOperationException($"Default role '{DefaultRoleName}' is not seeded in the database.");

            var user = new User
            {
                Email = pending.Email,
                FullName = pending.FullName,
                PhoneNumber = pending.PhoneNumber,
                PasswordHash = pending.PasswordHash,
                IsVerified = true,
                IsActive = true,
                OtpHash = null,
                OtpExpiredAt = null,
                EkycStatus = "NotSubmitted",
                CreatedAt = DateTime.UtcNow
            };

            await _userRepository.AddAsync(user);
            await _userRepository.SaveChangesAsync();

            await _userRepository.AddUserRoleAsync(new UserRole
            {
                UserId = user.UserId,
                RoleId = role.RoleId,
                AssignedAt = DateTime.UtcNow
            });
            await _userRepository.SaveChangesAsync();

            _cache.Remove(cacheKey);
            _logger.LogInformation("Account {Email} verified and created successfully (UserId={UserId}).", user.Email, user.UserId);
        }

        public async Task<LoginResponseDTO> LoginAsync(LoginDTO dto)
        {
            var user = await _userRepository.GetByEmailAsync(dto.Email)
                ?? throw new KeyNotFoundException("Invalid email or password.");

            if (!user.IsVerified)
                throw new InvalidOperationException("Account is not verified. Please verify your email first.");

            if (!user.IsActive)
                throw new UnauthorizedAccessException("Your account has been locked. Please contact the administrator.");

            if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            {
                _logger.LogWarning("Login failed: incorrect password for {Email}.", dto.Email);
                throw new UnauthorizedAccessException("Invalid email or password.");
            }

            var (accessToken, accessTokenExpiry) = GenerateAccessToken(user);
            string refreshToken = GenerateRefreshToken();

            var refreshTokenEntity = new RefreshToken
            {
                UserId = user.UserId,
                TokenHash = HashValue(refreshToken),
                ExpiresAt = DateTime.UtcNow.AddDays(
                    _configuration.GetValue<int>("JwtSettings:RefreshTokenExpiryDays")),
                CreatedAt = DateTime.UtcNow,
                IsRevoked = false
            };

            await _userRepository.AddRefreshTokenAsync(refreshTokenEntity);

            _logger.LogInformation("Login successful: {Email} (UserId={UserId}).", user.Email, user.UserId);

            return new LoginResponseDTO
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                AccessTokenExpiresAt = accessTokenExpiry,
                Roles = user.UserRoles.Select(ur => ur.Role.RoleName).ToList()
            };
        }

        public async Task<RefreshResponseDTO> RefreshTokenAsync(RefreshTokenDTO dto)
        {
            string tokenHash = HashValue(dto.RefreshToken);

            var tokenEntity = await _userRepository.GetRefreshTokenByHashAsync(tokenHash)
                ?? throw new KeyNotFoundException("Invalid refresh token.");

            // The token was already revoked but is being reused -> possible sign of
            // token theft/replay. Revoke all sessions for this user as a precaution.
            if (tokenEntity.IsRevoked)
            {
                _logger.LogWarning(
                    "Detected reuse of a revoked refresh token (UserId={UserId}). Revoking all sessions.",
                    tokenEntity.UserId);

                // TODO: requires a RevokeAllRefreshTokensAsync(int userId) method on IUserRepository
                // await _userRepository.RevokeAllRefreshTokensAsync(tokenEntity.UserId);

                throw new UnauthorizedAccessException("Refresh token has been revoked.");
            }

            if (tokenEntity.ExpiresAt < DateTime.UtcNow)
                throw new UnauthorizedAccessException("Refresh token has expired. Please login again.");

            if (!tokenEntity.User.IsActive)
            {
                _logger.LogWarning("Refresh failed: account UserId={UserId} is locked.", tokenEntity.UserId);
                throw new UnauthorizedAccessException("Your account has been locked. Please contact the administrator.");
            }

            var (accessToken, accessTokenExpiry) = GenerateAccessToken(tokenEntity.User);

            // Rotate the refresh token: revoke the old one, issue a new one.
            await _userRepository.RevokeRefreshTokenAsync(tokenHash);

            string newRefreshToken = GenerateRefreshToken();
            await _userRepository.AddRefreshTokenAsync(new RefreshToken
            {
                UserId = tokenEntity.UserId,
                TokenHash = HashValue(newRefreshToken),
                ExpiresAt = DateTime.UtcNow.AddDays(
                    _configuration.GetValue<int>("JwtSettings:RefreshTokenExpiryDays")),
                CreatedAt = DateTime.UtcNow,
                IsRevoked = false
            });

            return new RefreshResponseDTO
            {
                AccessToken = accessToken,
                AccessTokenExpiresAt = accessTokenExpiry,
                // Requires RefreshResponseDTO to have a RefreshToken field so the client can update it.
                RefreshToken = newRefreshToken
            };
        }

        public async Task LogoutAsync(LogoutDTO dto)
        {
            string tokenHash = HashValue(dto.RefreshToken);

            var tokenEntity = await _userRepository.GetRefreshTokenByHashAsync(tokenHash)
                ?? throw new KeyNotFoundException("Invalid refresh token.");

            if (tokenEntity.IsRevoked)
                return;

            await _userRepository.RevokeRefreshTokenAsync(tokenHash);
            _logger.LogInformation("Logout successful (UserId={UserId}).", tokenEntity.UserId);
        }

        public async Task ForgotPasswordAsync(ForgotPasswordDTO dto)
        {
            var user = await _userRepository.GetByEmailAsync(dto.Email);

            // Deliberately stay silent if the email doesn't exist or isn't verified,
            // to avoid revealing which emails are registered in the system.
            if (user == null || !user.IsVerified)
            {
                _logger.LogInformation("Forgot-password request for a non-existent/unverified email: {Email}.", dto.Email);
                return;
            }

            string otp = GenerateOTP();
            user.OtpHash = HashValue(otp);
            user.OtpExpiredAt = DateTime.UtcNow.AddMinutes(5);

            await _userRepository.SaveChangesAsync();
            await _emailService.SendOTPAsync(dto.Email, otp, EmailPurpose.ForgotPassword);
            _logger.LogInformation("Password reset OTP sent to {Email}.", dto.Email);
        }

        public async Task<VerifyResetOtpResponseDTO> VerifyResetOtpAsync(VerifyResetOtpDTO dto)
        {
            var user = await _userRepository.GetByEmailAsync(dto.Email);

            // Use one shared message for "account not found" and "wrong OTP"
            // to avoid revealing which emails exist in the system.
            if (user == null || string.IsNullOrEmpty(user.OtpHash) || user.OtpHash != HashValue(dto.OTP))
            {
                _logger.LogWarning("Password reset OTP verification failed for {Email}.", dto.Email);
                throw new UnauthorizedAccessException(GenericOtpInvalidMessage);
            }

            if (user.OtpExpiredAt == null || user.OtpExpiredAt < DateTime.UtcNow)
            {
                _logger.LogWarning("Password reset OTP expired for {Email}.", dto.Email);
                throw new UnauthorizedAccessException(GenericOtpInvalidMessage);
            }

            user.OtpHash = null;
            user.OtpExpiredAt = null;
            await _userRepository.SaveChangesAsync();

            string resetToken = GenerateRefreshToken();
            var expiresAt = DateTime.UtcNow.AddMinutes(10);

            _cache.Set(
                $"reset_token:{dto.Email}",
                HashValue(resetToken),
                expiresAt - DateTime.UtcNow);

            return new VerifyResetOtpResponseDTO
            {
                ResetToken = resetToken,
                ExpiresAt = expiresAt
            };
        }

        public async Task ResetPasswordAsync(ResetPasswordDTO dto)
        {
            var user = await _userRepository.GetByEmailAsync(dto.Email);

            string cacheKey = $"reset_token:{dto.Email}";
            bool hasValidCache = _cache.TryGetValue(cacheKey, out string? storedHash) && storedHash != null;

            // Use one shared message for every failure reason (user not found,
            // token expired, token mismatch) to avoid leaking account information.
            if (user == null || !hasValidCache || storedHash != HashValue(dto.ResetToken))
            {
                _logger.LogWarning("Password reset failed for {Email}: reset token invalid or expired.", dto.Email);
                throw new UnauthorizedAccessException("Reset token is invalid or expired. Please verify OTP again.");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            await _userRepository.SaveChangesAsync();

            _cache.Remove(cacheKey);

            // Important: revoke all old refresh tokens after a password change,
            // so that if the account was ever compromised, the attacker can't
            // keep using an old token.
            // TODO: requires a RevokeAllRefreshTokensAsync(int userId) method on IUserRepository
            // await _userRepository.RevokeAllRefreshTokensAsync(user.UserId);

            _logger.LogInformation("Password reset successful for {Email} (UserId={UserId}).", user.Email, user.UserId);
        }

        private (string token, DateTime expiry) GenerateAccessToken(User user)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"]!;
            var issuer = jwtSettings["Issuer"]!;
            var audience = jwtSettings["Audience"]!;
            int expiryMinutes = jwtSettings.GetValue<int>("AccessTokenExpiryMinutes");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub,   user.UserId.ToString()),
                new(ClaimTypes.NameIdentifier,     user.UserId.ToString()),
                new(JwtRegisteredClaimNames.Email, user.Email),
                new(ClaimTypes.Email,              user.Email),
                new("fullName",                    user.FullName),
                new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString())
            };
            claims.AddRange(user.UserRoles.Select(ur => new Claim(ClaimTypes.Role, ur.Role.RoleName)));

            var expiry = DateTime.UtcNow.AddMinutes(expiryMinutes);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: expiry,
                signingCredentials: creds);

            return (new JwtSecurityTokenHandler().WriteToken(token), expiry);
        }

        private static string GenerateRefreshToken()
        {
            var bytes = new byte[64];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes);
        }

        private static string GenerateOTP()
        {
            int value = RandomNumberGenerator.GetInt32(100000, 1000000);
            return value.ToString();
        }

        private static string HashValue(string value)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(bytes); // hex, uppercase
        }
    }
}