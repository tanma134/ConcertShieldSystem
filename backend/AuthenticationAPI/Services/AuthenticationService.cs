using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AuthenticationAPI.DTOs;
using AuthenticationAPI.Models;
using AuthenticationAPI.Repositories;
using AuthenticationAPI.Risk;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using Google.Apis.Auth;

namespace AuthenticationAPI.Services
{
    /// <summary>
    /// BR-112: the account is under an active risk block, so no new session may start.
    /// The controller turns this into 403 RISK_BLOCKED.
    /// </summary>
    public sealed class AccountRiskBlockedException : Exception
    {
        public AccountRiskBlockedException(string message) : base(message) { }
    }

    public class AuthenticationService : IAuthenticationService
    {
        private readonly IUserRepository _userRepository;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AuthenticationService> _logger;
        private readonly IAuditLogClient _auditLog;
        private readonly IRiskClient _riskClient;

        private const string DefaultRoleName = "Customer";
        private const string AdminRoleName = "Admin";
        private const string GenericOtpInvalidMessage = "Invalid or expired OTP.";

        // An OTP is 6 digits: unlimited guesses inside 5 minutes would make it brute-forceable.
        private const int MaxOtpAttempts = 5;
        // Wrong passwords are counted per account and reset on a successful login.
        private const int MaxLoginFailures = 5;
        private static readonly TimeSpan LoginLockDuration = TimeSpan.FromMinutes(15);

        public AuthenticationService(
            IUserRepository userRepository,
            IEmailService emailService,
            IConfiguration configuration,
            IMemoryCache cache,
            ILogger<AuthenticationService> logger,
            IAuditLogClient auditLog,
            IRiskClient riskClient)
        {
            _userRepository = userRepository;
            _emailService = emailService;
            _configuration = configuration;
            _cache = cache;
            _logger = logger;
            _auditLog = auditLog;
            _riskClient = riskClient;
        }

        // Xác định actor_type ghi vào audit log dựa theo role THẬT của user, thay vì hardcode "user".
        private static string ResolveActorType(User user) =>
            user.UserRoles != null && user.UserRoles.Any(ur => ur.Role.RoleName == AdminRoleName)
                ? "admin"
                : user.UserRoles != null && user.UserRoles.Any(ur => ur.Role.RoleName.Equals("Staff", StringComparison.OrdinalIgnoreCase))
                    ? "staff"
                    : user.UserRoles != null && user.UserRoles.Any(ur => ur.Role.RoleName.Equals("Organizer", StringComparison.OrdinalIgnoreCase))
                        ? "organizer"
                        : "user";

        public async Task RegisterAsync(RegisterDTO dto)
        {
            dto.Email = (dto.Email ?? string.Empty).Trim().ToLowerInvariant();
            var existingUser = await _userRepository.GetByEmailAsync(dto.Email);
            if (existingUser != null)
            {
                _logger.LogWarning("Registration failed: email {Email} already exists.", dto.Email);
                throw new InvalidOperationException("Email already exists.");
            }

            string cacheKey = $"pending_register:{dto.Email}";

            // BR-22: wrong-OTP attempts belong to the email's registration round, not to
            // one particular code. Requesting a new code must not reset the counter.
            // The counter starts over only when the round is gone: 5 wrong codes cancel
            // the round, and an expired round is dropped from the cache.
            var carriedAttempts =
                _cache.TryGetValue(cacheKey, out PendingRegistration? previous) && previous != null
                    ? previous.FailedOtpAttempts
                    : 0;

            string otp = GenerateOTP();
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            var pending = new PendingRegistration
            {
                FullName = dto.FullName,
                Email = dto.Email,
                PhoneNumber = dto.PhoneNumber,
                PasswordHash = passwordHash,
                OtpCode = otp,
                ExpiredAt = DateTime.UtcNow.AddMinutes(5),
                FailedOtpAttempts = carriedAttempts
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
                pending.FailedOtpAttempts++;
                if (pending.FailedOtpAttempts >= MaxOtpAttempts)
                {
                    // Too many guesses: the code is dead and the customer must register again.
                    _cache.Remove(cacheKey);
                    _logger.LogWarning("Email verification locked after {Attempts} wrong OTPs for {Email}.",
                        pending.FailedOtpAttempts, dto.Email);
                    throw new UnauthorizedAccessException(
                        "Too many incorrect codes. Please register again to receive a new one.");
                }

                // Keep the counter, and the remaining lifetime, in the cache.
                _cache.Set(cacheKey, pending, pending.ExpiredAt - DateTime.UtcNow);
                _logger.LogWarning("Email verification failed: invalid OTP for {Email} (attempt {Attempt}).",
                    dto.Email, pending.FailedOtpAttempts);
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
                CreatedAt = DateTime.UtcNow,
                AuthProvider = "local",
                HasPassword = true
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

        public async Task<LoginResponseDTO> LoginAsync(LoginDTO dto, string? ipAddress = null, string? userAgent = null)
        {
            dto.Email = (dto.Email ?? string.Empty).Trim().ToLowerInvariant();
            // Accounts are created only through registration or an administrator.
            // A hard-coded demo account on the login path was a backdoor: knowing the
            // address and password was enough to obtain a Staff session.
            var user = await _userRepository.GetByEmailAsync(dto.Email);

            if (user == null)
                throw new KeyNotFoundException("Invalid email or password.");

            if (!user.IsVerified)
                throw new InvalidOperationException("Account is not verified. Please verify your email first.");

            if (!user.IsActive)
            {
                _logger.LogWarning("Login blocked: UserId={UserId} account is locked.", user.UserId);

                await _auditLog.LogAsync(user.UserId, ResolveActorType(user), "LOGIN_BLOCKED_ACCOUNT", "User", user.UserId.ToString(),
                    ipAddress: ipAddress, userAgent: userAgent);

                throw new UnauthorizedAccessException("Your account has been locked. Please contact the administrator.");
            }

            if (!user.HasPassword)
            {
                _logger.LogWarning("Login failed: {Email} has no local password (registered via {Provider}).", dto.Email, user.AuthProvider);
                throw new UnauthorizedAccessException(
                    "This account was created with Google Sign-In. Please log in with Google, or use 'Forgot Password' to set a password.");
            }

            // A locked account is refused before the hash comparison, so a stolen
            // password cannot be confirmed by watching the response time or message.
            var lockedFor = GetLoginLockRemaining(user.UserId);
            if (lockedFor.HasValue)
            {
                _logger.LogWarning("Login blocked: UserId={UserId} is temporarily locked for {Minutes} more minute(s).",
                    user.UserId, (int)Math.Ceiling(lockedFor.Value.TotalMinutes));

                await _auditLog.LogAsync(user.UserId, ResolveActorType(user), "LOGIN_BLOCKED_LOCKOUT", "User", user.UserId.ToString(),
                    ipAddress: ipAddress, userAgent: userAgent);

                throw new UnauthorizedAccessException(
                    $"Too many failed attempts. Please try again in {(int)Math.Ceiling(lockedFor.Value.TotalMinutes)} minute(s).");
            }

            if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            {
                var failures = RegisterLoginFailure(user.UserId);
                _logger.LogWarning("Login failed: incorrect password for {Email} (attempt {Attempt}/{Max}).",
                    dto.Email, failures, MaxLoginFailures);

                await _auditLog.LogAsync(user.UserId, ResolveActorType(user), "LOGIN_FAILED", "User", user.UserId.ToString(),
                    newValue: new { attempt = failures, maxAttempts = MaxLoginFailures },
                    ipAddress: ipAddress, userAgent: userAgent);

                throw new UnauthorizedAccessException("Invalid email or password.");
            }

            ClearLoginFailures(user.UserId);

            // BR-112: the password is right, but an active risk block still refuses a new session.
            await EnsureNotRiskBlockedAsync(user.UserId);

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

            await _auditLog.LogAsync(user.UserId, ResolveActorType(user), "LOGIN", "User", user.UserId.ToString(),
                ipAddress: ipAddress, userAgent: userAgent);

            return new LoginResponseDTO
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                AccessTokenExpiresAt = accessTokenExpiry,
                Roles = user.UserRoles.Select(ur => ur.Role.RoleName).ToList(),
                User = new UserInfoDTO
                {
                    UserId = user.UserId,
                    Email = user.Email,
                    FullName = user.FullName,
                    AvatarUrl = user.AvatarUrl,
                    AuthProvider = user.AuthProvider,
                    HasPassword = user.HasPassword
                }
            };
        }

        public async Task<RefreshResponseDTO> RefreshTokenAsync(RefreshTokenDTO dto, string? ipAddress = null, string? userAgent = null)
        {
            string tokenHash = HashValue(dto.RefreshToken);

            var tokenEntity = await _userRepository.GetRefreshTokenByHashAsync(tokenHash)
                ?? throw new KeyNotFoundException("Invalid refresh token.");

            if (tokenEntity.IsRevoked)
            {
                _logger.LogWarning(
                    "Detected reuse of a revoked refresh token (UserId={UserId}). Revoking all sessions.",
                    tokenEntity.UserId);

                await _auditLog.LogAsync(tokenEntity.UserId, ResolveActorType(tokenEntity.User), "REFRESH_TOKEN_REUSE_DETECTED", "User", tokenEntity.UserId.ToString(),
                    ipAddress: ipAddress, userAgent: userAgent);

                // A revoked token coming back means someone kept a copy: end every
                // session of this account, not just the replayed one.
                var revokedSessions = await _userRepository.RevokeAllRefreshTokensAsync(tokenEntity.UserId);
                _logger.LogWarning("Revoked {Count} refresh token(s) for UserId={UserId} after token reuse.",
                    revokedSessions, tokenEntity.UserId);

                throw new UnauthorizedAccessException("Refresh token has been revoked.");
            }

            if (tokenEntity.ExpiresAt < DateTime.UtcNow)
                throw new UnauthorizedAccessException("Refresh token has expired. Please login again.");

            if (!tokenEntity.User.IsActive)
            {
                _logger.LogWarning("Refresh failed: account UserId={UserId} is locked.", tokenEntity.UserId);

                await _auditLog.LogAsync(tokenEntity.UserId, ResolveActorType(tokenEntity.User), "LOGIN_BLOCKED_ACCOUNT", "User", tokenEntity.UserId.ToString(),
                    ipAddress: ipAddress, userAgent: userAgent);

                throw new UnauthorizedAccessException("Your account has been locked. Please contact the administrator.");
            }

            // BR-112: an expired access token of a blocked account must not be renewed.
            await EnsureNotRiskBlockedAsync(tokenEntity.UserId);

            var (accessToken, accessTokenExpiry) = GenerateAccessToken(tokenEntity.User);

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
                RefreshToken = newRefreshToken,
                Roles = tokenEntity.User.UserRoles.Select(ur => ur.Role.RoleName).ToList()
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
            dto.Email = (dto.Email ?? string.Empty).Trim().ToLowerInvariant();
            var user = await _userRepository.GetByEmailAsync(dto.Email);

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
            dto.Email = (dto.Email ?? string.Empty).Trim().ToLowerInvariant();
            var user = await _userRepository.GetByEmailAsync(dto.Email);

            if (user == null || string.IsNullOrEmpty(user.OtpHash) || user.OtpHash != HashValue(dto.OTP))
            {
                if (user != null)
                    await CountResetOtpFailureAsync(dto.Email, user);

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
            _cache.Remove(ResetOtpAttemptKey(dto.Email));

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

        public async Task ResetPasswordAsync(ResetPasswordDTO dto, string? ipAddress = null, string? userAgent = null)
        {
            dto.Email = (dto.Email ?? string.Empty).Trim().ToLowerInvariant();
            var user = await _userRepository.GetByEmailAsync(dto.Email);

            string cacheKey = $"reset_token:{dto.Email}";
            bool hasValidCache = _cache.TryGetValue(cacheKey, out string? storedHash) && storedHash != null;

            if (user == null || !hasValidCache || storedHash != HashValue(dto.ResetToken))
            {
                _logger.LogWarning("Password reset failed for {Email}: reset token invalid or expired.", dto.Email);
                throw new UnauthorizedAccessException("Reset token is invalid or expired. Please verify OTP again.");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            user.HasPassword = true; // account nay da co password that, du tao qua Google hay khong
            await _userRepository.SaveChangesAsync();

            _cache.Remove(cacheKey);

            // Anyone holding an old refresh token must lose access once the password changes.
            await _userRepository.RevokeAllRefreshTokensAsync(user.UserId);

            _logger.LogInformation("Password reset successful for {Email} (UserId={UserId}).", user.Email, user.UserId);

            await _auditLog.LogAsync(user.UserId, ResolveActorType(user), "PASSWORD_RESET", "User", user.UserId.ToString(),
                ipAddress: ipAddress, userAgent: userAgent);
        }

        /// <summary>
        /// BR-112: asks AdminAPI whether the user is under an active block.
        /// Fail-open: when AdminAPI is unreachable the check returns null and the login
        /// proceeds, so a risk-service outage never locks everybody out.
        /// </summary>
        private async Task EnsureNotRiskBlockedAsync(int userId)
        {
            if (!_configuration.GetValue("Risk:Enabled", true))
                return;

            var check = await _riskClient.CheckBlockAsync(userId, null, null, null);
            if (check is { Blocked: true })
            {
                _logger.LogWarning("Login/refresh refused: UserId={UserId} is under an active risk block.", userId);
                throw new AccountRiskBlockedException(
                    check.Message ?? "Your account is temporarily restricted due to unusual activity.");
            }
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
                new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
                new("account_created_at",          DateTime.SpecifyKind(user.CreatedAt, DateTimeKind.Utc).ToString("O")),
                // Lets downstream services (TicketAPI) enforce "eKYC required to buy"
                // without an extra call on every request.
                new("ekyc_status",                 user.EkycStatus ?? "NotSubmitted")
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

        public async Task<LoginResponseDTO> GoogleLoginAsync(GoogleLoginDTO dto, string? ipAddress = null, string? userAgent = null)
        {
            GoogleJsonWebSignature.Payload payload;
            try
            {
                var settings = new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { _configuration["Google:ClientId"]! }
                };
                payload = await GoogleJsonWebSignature.ValidateAsync(dto.IdToken, settings);
            }
            catch (InvalidJwtException ex)
            {
                _logger.LogWarning(ex, "Google login failed: invalid ID token.");
                throw new UnauthorizedAccessException("Invalid Google token.");
            }

            if (!payload.EmailVerified)
            {
                _logger.LogWarning("Google login failed: email {Email} not verified by Google.", payload.Email);
                throw new UnauthorizedAccessException("Google email is not verified.");
            }

            var user = await _userRepository.GetByEmailAsync(payload.Email);
            bool isNewUser = user == null;

            if (user == null)
            {
                var role = await _userRepository.GetRoleByNameAsync(DefaultRoleName)
                    ?? throw new InvalidOperationException($"Default role '{DefaultRoleName}' is not seeded in the database.");

                user = new User
                {
                    Email = payload.Email,
                    FullName = payload.Name ?? payload.Email,
                    AvatarUrl = payload.Picture,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()),
                    IsVerified = true,
                    IsActive = true,
                    EkycStatus = "NotSubmitted",
                    CreatedAt = DateTime.UtcNow,
                    AuthProvider = "google",
                    HasPassword = false
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

                _logger.LogInformation("New account created via Google login: {Email} (UserId={UserId}).", user.Email, user.UserId);
            }
            else
            {
                if (!user.IsActive)
                {
                    await _auditLog.LogAsync(user.UserId, ResolveActorType(user), "LOGIN_BLOCKED_ACCOUNT", "User", user.UserId.ToString(),
                        ipAddress: ipAddress, userAgent: userAgent);

                    throw new UnauthorizedAccessException("Your account has been locked. Please contact the administrator.");
                }

                // BR-112: Google proves identity, not that the account is not risk-blocked.
                await EnsureNotRiskBlockedAsync(user.UserId);

                if (!user.IsVerified)
                {
                    user.IsVerified = true;
                    await _userRepository.SaveChangesAsync();
                }
            }

            // Load lại đầy đủ UserRoles (bắt buộc để ResolveActorType đọc đúng role,
            // nhất là với user vừa tạo mới ở nhánh trên, hoặc nếu GetByEmailAsync ban đầu chưa Include đủ)
            user = await _userRepository.GetByEmailAsync(user.Email) ?? user;

            var (accessToken, accessTokenExpiry) = GenerateAccessToken(user);
            string refreshToken = GenerateRefreshToken();

            await _userRepository.AddRefreshTokenAsync(new RefreshToken
            {
                UserId = user.UserId,
                TokenHash = HashValue(refreshToken),
                ExpiresAt = DateTime.UtcNow.AddDays(
                    _configuration.GetValue<int>("JwtSettings:RefreshTokenExpiryDays")),
                CreatedAt = DateTime.UtcNow,
                IsRevoked = false
            });

            _logger.LogInformation("Google login successful: {Email} (UserId={UserId}).", user.Email, user.UserId);

            await _auditLog.LogAsync(user.UserId, ResolveActorType(user), isNewUser ? "USER_REGISTERED" : "LOGIN", "User", user.UserId.ToString(),
                newValue: isNewUser ? new { provider = "google" } : null,
                ipAddress: ipAddress, userAgent: userAgent);

            return new LoginResponseDTO
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                AccessTokenExpiresAt = accessTokenExpiry,
                Roles = user.UserRoles.Select(ur => ur.Role.RoleName).ToList(),
                User = new UserInfoDTO
                {
                    UserId = user.UserId,
                    Email = user.Email,
                    FullName = user.FullName,
                    AvatarUrl = user.AvatarUrl,
                    AuthProvider = user.AuthProvider,
                    HasPassword = user.HasPassword
                }
            };
        }

        public async Task ChangePasswordAsync(int userId, ChangePasswordDTO dto, string? ipAddress = null, string? userAgent = null)
        {
            var user = await _userRepository.GetByIdAsync(userId)
                ?? throw new KeyNotFoundException("User not found.");
            if (!user.HasPassword)
            {
                _logger.LogWarning("Change-password failed: UserId={UserId} has no local password (provider {Provider}).", userId, user.AuthProvider);
                throw new InvalidOperationException(
                    "This account has no password yet. Please use 'Forgot Password' to set one first.");
            }

            if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
            {
                _logger.LogWarning("Change-password failed: incorrect current password for UserId={UserId}.", userId);
                throw new UnauthorizedAccessException("The current password is incorrect.");
            }

            if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 8)
                throw new InvalidOperationException("The new password must be at least 8 characters.");

            if (dto.NewPassword != dto.ConfirmPassword)
                throw new InvalidOperationException("New password and confirmation do not match.");

            if (dto.CurrentPassword == dto.NewPassword)
                throw new InvalidOperationException("The new password must be different from the current password.");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            await _userRepository.SaveChangesAsync();

            // Changing a password ends every other session. The caller's own refresh
            // token is revoked too; the client re-authenticates on the next refresh.
            var revokedSessions = await _userRepository.RevokeAllRefreshTokensAsync(userId);
            _logger.LogInformation("Password changed for UserId={UserId}; revoked {Count} refresh token(s).",
                userId, revokedSessions);

            await _auditLog.LogAsync(userId, ResolveActorType(user), "PASSWORD_CHANGED", "User", userId.ToString(),
                ipAddress: ipAddress, userAgent: userAgent);
        }

        private static string LoginFailureKey(int userId) => $"login_failures:{userId}";

        private sealed record LoginFailureState(int Count, DateTime? LockedUntil);

        private int RegisterLoginFailure(int userId)
        {
            var key = LoginFailureKey(userId);
            var current = _cache.TryGetValue(key, out LoginFailureState? state) && state != null
                ? state
                : new LoginFailureState(0, null);

            var count = current.Count + 1;
            // The lock starts on the attempt that reaches the limit, and lasts from that moment.
            var lockedUntil = count >= MaxLoginFailures ? DateTime.UtcNow.Add(LoginLockDuration) : current.LockedUntil;

            var next = new LoginFailureState(count, lockedUntil);
            // The cache entry lives exactly as long as the window it represents.
            var ttl = lockedUntil.HasValue ? lockedUntil.Value - DateTime.UtcNow : LoginLockDuration;
            _cache.Set(key, next, ttl > TimeSpan.Zero ? ttl : TimeSpan.FromSeconds(1));
            return count;
        }

        private TimeSpan? GetLoginLockRemaining(int userId)
        {
            var key = LoginFailureKey(userId);
            if (!_cache.TryGetValue(key, out LoginFailureState? state) || state?.LockedUntil == null)
                return null;

            var remaining = state.LockedUntil.Value - DateTime.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : null;
        }

        private void ClearLoginFailures(int userId) => _cache.Remove(LoginFailureKey(userId));

        private static string ResetOtpAttemptKey(string email) => $"reset_otp_attempts:{email}";

        /// <summary>
        /// Counts wrong reset-OTP entries. After MaxOtpAttempts the stored OTP is destroyed,
        /// so the only way forward is a new "forgot password" request.
        /// </summary>
        private async Task CountResetOtpFailureAsync(string email, User user)
        {
            var key = ResetOtpAttemptKey(email);
            var attempts = _cache.TryGetValue(key, out int n) ? n + 1 : 1;

            if (attempts >= MaxOtpAttempts)
            {
                user.OtpHash = null;
                user.OtpExpiredAt = null;
                await _userRepository.SaveChangesAsync();
                _cache.Remove(key);
                _logger.LogWarning("Password reset OTP invalidated after {Attempts} wrong attempts for {Email}.",
                    attempts, email);
                return;
            }

            _cache.Set(key, attempts, TimeSpan.FromMinutes(10));
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