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

        private const string DefaultRoleName = "User";

        public AuthenticationService(
            IUserRepository userRepository,
            IEmailService emailService,
            IConfiguration configuration,
            IMemoryCache cache)
        {
            _userRepository = userRepository;
            _emailService = emailService;
            _configuration = configuration;
            _cache = cache;
        }

    
        public async Task RegisterAsync(RegisterDTO dto)
        {
            var existingUser = await _userRepository.GetByEmailAsync(dto.Email);
            if (existingUser != null)
                throw new InvalidOperationException("Email already exists.");

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
        }
        public async Task VerifyEmailAsync(VerifyEmailDTO dto)
        {
            string cacheKey = $"pending_register:{dto.Email}";
            if (!_cache.TryGetValue(cacheKey, out PendingRegistration? pending) || pending == null)
                throw new KeyNotFoundException("No pending registration found for this email. Please register again.");

            if (pending.OtpCode != dto.OTP)
                throw new UnauthorizedAccessException("Invalid OTP.");

            if (pending.ExpiredAt < DateTime.UtcNow)
            {
                _cache.Remove(cacheKey);
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
                throw new UnauthorizedAccessException("Invalid email or password.");

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

            if (tokenEntity.IsRevoked)
                throw new UnauthorizedAccessException("Refresh token has been revoked.");

            if (tokenEntity.ExpiresAt < DateTime.UtcNow)
                throw new UnauthorizedAccessException("Refresh token has expired. Please login again.");

            var (accessToken, accessTokenExpiry) = GenerateAccessToken(tokenEntity.User);

            return new RefreshResponseDTO
            {
                AccessToken = accessToken,
                AccessTokenExpiresAt = accessTokenExpiry
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
        }

        public async Task ForgotPasswordAsync(ForgotPasswordDTO dto)
        {
            var user = await _userRepository.GetByEmailAsync(dto.Email);

            if (user == null || !user.IsVerified)
                return;

            string otp = GenerateOTP();
            user.OtpHash = HashValue(otp);
            user.OtpExpiredAt = DateTime.UtcNow.AddMinutes(5);

            await _userRepository.SaveChangesAsync();
            await _emailService.SendOTPAsync(dto.Email, otp, EmailPurpose.ForgotPassword);
        }
        public async Task<VerifyResetOtpResponseDTO> VerifyResetOtpAsync(VerifyResetOtpDTO dto)
        {
            var user = await _userRepository.GetByEmailAsync(dto.Email)
                ?? throw new KeyNotFoundException("Account not found.");

            if (string.IsNullOrEmpty(user.OtpHash) || user.OtpHash != HashValue(dto.OTP))
                throw new UnauthorizedAccessException("Invalid OTP.");

            if (user.OtpExpiredAt == null || user.OtpExpiredAt < DateTime.UtcNow)
                throw new UnauthorizedAccessException("OTP has expired. Please request a new one.");
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
            var user = await _userRepository.GetByEmailAsync(dto.Email)
                ?? throw new KeyNotFoundException("Account not found.");

            string cacheKey = $"reset_token:{dto.Email}";
            if (!_cache.TryGetValue(cacheKey, out string? storedHash) || storedHash == null)
                throw new UnauthorizedAccessException("Reset token is invalid or expired. Please verify OTP again.");

            if (storedHash != HashValue(dto.ResetToken))
                throw new UnauthorizedAccessException("Reset token is invalid or expired. Please verify OTP again.");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            await _userRepository.SaveChangesAsync();

            _cache.Remove(cacheKey);
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
            return Convert.ToHexString(bytes); // hex, viết hoa
        }
    }
}