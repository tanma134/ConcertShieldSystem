using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AuthenticationAPI.DTOs;
using AuthenticationAPI.Models;
using AuthenticationAPI.Repositories;
using Microsoft.Extensions.Logging;

namespace AuthenticationAPI.Services
{
    public class OrganizerRequestService : IOrganizerRequestService
    {
        private readonly IOrganizerRequestRepository _requestRepository;
        private readonly IUserRepository _userRepository;
        private readonly IDocumentStorageService _documentStorageService;
        private readonly IEmailService _emailService;
        private readonly ILogger<OrganizerRequestService> _logger;
        private readonly IOrganizerApprovalNotifier? _approvalNotifier;

        private const string RoleOrganizer = "Organizer";
        private const string StatusPending = "Pending";
        private const string StatusApproved = "Approved";
        private const string StatusRejected = "Rejected";

        private readonly Microsoft.Extensions.Configuration.IConfiguration? _configuration;

        [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
        public OrganizerRequestService(
            IOrganizerRequestRepository requestRepository,
            IUserRepository userRepository,
            IDocumentStorageService documentStorageService,
            IEmailService emailService,
            ILogger<OrganizerRequestService> logger,
            Microsoft.Extensions.Configuration.IConfiguration? configuration = null,
            IOrganizerApprovalNotifier? approvalNotifier = null)
        {
            _requestRepository = requestRepository;
            _userRepository = userRepository;
            _documentStorageService = documentStorageService;
            _emailService = emailService;
            _logger = logger;
            _configuration = configuration;
            _approvalNotifier = approvalNotifier;
        }

        public OrganizerRequestService(
            IOrganizerRequestRepository requestRepository,
            IUserRepository userRepository,
            ILogger<OrganizerRequestService> logger,
            IOrganizerApprovalNotifier? approvalNotifier = null)
            : this(requestRepository, userRepository, null!, null!, logger, null, approvalNotifier)
        {
        }

        public async Task<OrganizerRequestResponseDTO> CreateRequestAsync(int userId, CreateOrganizerRequestDTO dto)
        {
            var user = await _userRepository.GetByIdAsync(userId)
                ?? throw new KeyNotFoundException("User not found.");

            bool alreadyOrganizer = user.UserRoles.Any(ur => ur.Role.RoleName == RoleOrganizer);
            if (alreadyOrganizer)
            {
                _logger.LogWarning("Organizer request rejected: UserId={UserId} is already an Organizer.", userId);
                throw new InvalidOperationException("Your account already holds the Organizer role.");
            }

            var pending = await _requestRepository.GetPendingByUserIdAsync(userId);
            if (pending != null)
            {
                _logger.LogWarning("Organizer request rejected: UserId={UserId} already has a pending request.", userId);
                throw new InvalidOperationException("You already have a pending verification request. Please wait for an administrator to review it.");
            }

            // Upload optional business license & mandatory representative ID to Cloudinary
            UploadedDocumentResult? licenseUpload = null;
            if (dto.BusinessLicenseFile != null && dto.BusinessLicenseFile.Length > 0)
            {
                licenseUpload = await _documentStorageService.UploadDocumentAsync(dto.BusinessLicenseFile, "BUSINESS_LICENSE");
            }
            var idUpload = await _documentStorageService.UploadDocumentAsync(dto.RepresentativeIdFile, "REPRESENTATIVE_ID");

            UploadedDocumentResult? portfolioUpload = null;
            if (dto.PortfolioFile != null && dto.PortfolioFile.Length > 0)
            {
                portfolioUpload = await _documentStorageService.UploadDocumentAsync(dto.PortfolioFile, "PORTFOLIO");
            }

            var request = new OrganizerRequest
            {
                UserId = userId,
                Reason = dto.Reason.Trim(),
                CompanyName = string.IsNullOrWhiteSpace(dto.CompanyName) ? null : dto.CompanyName.Trim(),
                TaxCode = dto.TaxCode.Trim(),
                RepresentativeName = dto.RepresentativeName.Trim(),
                BankAccountHolder = dto.BankAccountHolder.Trim(),
                BankAccountNumber = dto.BankAccountNumber.Trim(),
                BankName = dto.BankName.Trim(),
                BankBranch = string.IsNullOrWhiteSpace(dto.BankBranch) ? null : dto.BankBranch.Trim(),
                Website = string.IsNullOrWhiteSpace(dto.Website) ? null : dto.Website.Trim(),
                PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : dto.PhoneNumber.Trim(),
                Experience = string.IsNullOrWhiteSpace(dto.Experience) ? null : dto.Experience.Trim(),
                Status = StatusPending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            if (licenseUpload != null && dto.BusinessLicenseFile != null)
            {
                request.Documents.Add(new OrganizerRequestDocument
                {
                    DocumentType = "BUSINESS_LICENSE",
                    FileName = dto.BusinessLicenseFile.FileName,
                    FileUrl = licenseUpload.SecureUrl,
                    PreviewUrl = licenseUpload.PreviewUrl,
                    PublicId = licenseUpload.PublicId,
                    IsImage = licenseUpload.IsImage,
                    UploadedAt = DateTime.UtcNow
                });
            }

            request.Documents.Add(new OrganizerRequestDocument
            {
                DocumentType = "REPRESENTATIVE_ID",
                FileName = dto.RepresentativeIdFile.FileName,
                FileUrl = idUpload.SecureUrl,
                PreviewUrl = idUpload.PreviewUrl,
                PublicId = idUpload.PublicId,
                IsImage = idUpload.IsImage,
                UploadedAt = DateTime.UtcNow
            });

            if (portfolioUpload != null && dto.PortfolioFile != null)
            {
                request.Documents.Add(new OrganizerRequestDocument
                {
                    DocumentType = "PORTFOLIO",
                    FileName = dto.PortfolioFile.FileName,
                    FileUrl = portfolioUpload.SecureUrl,
                    PreviewUrl = portfolioUpload.PreviewUrl,
                    PublicId = portfolioUpload.PublicId,
                    IsImage = portfolioUpload.IsImage,
                    UploadedAt = DateTime.UtcNow
                });
            }

            await _requestRepository.AddAsync(request);
            await _requestRepository.SaveChangesAsync();

            _logger.LogInformation("Organizer request created: UserId={UserId}, RequestId={RequestId}, DocsCount={Count}.",
                userId, request.RequestId, request.Documents.Count);

            // Notify Admin of new Organizer verification application
            _ = Task.Run(async () =>
            {
                var displayName = !string.IsNullOrWhiteSpace(request.CompanyName) ? request.CompanyName : request.RepresentativeName;
                var adminNotifTitle = $"New Organizer Application: {displayName}";
                var adminNotifMsg = $"Applicant '{displayName}' (Tax: {request.TaxCode}, Bank: {request.BankName} - {request.BankAccountNumber}) has submitted verification documents for review.";
                try
                {
                    using var conn = new Npgsql.NpgsqlConnection("Host=localhost;Port=5432;Database=notification_db;Username=postgres;Password=123456");
                    await conn.OpenAsync();
                    using var cmd = new Npgsql.NpgsqlCommand(
                        "INSERT INTO notifications (user_id, title, message, category, target_url, is_read, created_at, is_deleted) VALUES (@uid, @title, @msg, @cat, @url, false, NOW(), false);", conn);
                    cmd.Parameters.AddWithValue("uid", 1); // Admin user id 1
                    cmd.Parameters.AddWithValue("title", adminNotifTitle);
                    cmd.Parameters.AddWithValue("msg", adminNotifMsg);
                    cmd.Parameters.AddWithValue("cat", "organizer_request_submitted");
                    cmd.Parameters.AddWithValue("url", "/admin/organizer-requests");
                    await cmd.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to persist admin notification for organizer request {RequestId}", request.RequestId);
                }

                try
                {
                    var handler = new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
                    };
                    using var httpClient = new HttpClient(handler);
                    httpClient.Timeout = TimeSpan.FromSeconds(3);

                    var payload = new
                    {
                        userId = 1,
                        title = adminNotifTitle,
                        message = adminNotifMsg,
                        category = "organizer_request_submitted",
                        targetUrl = "/admin/organizer-requests"
                    };
                    var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");

                    var endpoints = new[] { "https://localhost:7197/api/notifications", "http://localhost:5174/api/notifications" };
                    foreach (var ep in endpoints)
                    {
                        try
                        {
                            var res = await httpClient.PostAsync(ep, content);
                            if (res.IsSuccessStatusCode) break;
                        }
                        catch { }
                    }
                }
                catch { }
            });

            return MapToDto(request, user);
        }

        public async Task<List<OrganizerRequestResponseDTO>> GetMyRequestsAsync(int userId)
        {
            var requests = await _requestRepository.GetByUserIdAsync(userId);
            return requests.Select(r => MapToDto(r, r.User)).ToList();
        }

        public async Task<List<OrganizerRequestResponseDTO>> GetAllRequestsAsync(string? status)
        {
            var requests = await _requestRepository.GetAllAsync(status);
            return requests.Select(r => MapToDto(r, r.User)).ToList();
        }

        public async Task<OrganizerRequestResponseDTO> GetByIdAsync(int requestId)
        {
            var request = await _requestRepository.GetByIdAsync(requestId)
                ?? throw new KeyNotFoundException("Organizer request not found.");

            return MapToDto(request, request.User);
        }

        public async Task<OrganizerRequestResponseDTO> ReviewRequestAsync(int requestId, int reviewerId, ReviewOrganizerRequestDTO dto)
        {
            if (dto.Status != StatusApproved && dto.Status != StatusRejected)
                throw new ArgumentException("Status must be 'Approved' or 'Rejected'.");

            if (dto.Status == StatusRejected && string.IsNullOrWhiteSpace(dto.RejectionReason))
                throw new ArgumentException("RejectionReason is required when rejecting a request.");

            var request = await _requestRepository.GetByIdAsync(requestId)
                ?? throw new KeyNotFoundException("Organizer request not found.");

            if (request.Status != StatusPending)
            {
                _logger.LogWarning("Review rejected: RequestId={RequestId} is not pending (current status: {Status}).", requestId, request.Status);
                throw new InvalidOperationException($"This request has already been {request.Status.ToLower()}.");
            }

            request.Status = dto.Status;
            request.ReviewedBy = reviewerId;
            request.ReviewedAt = DateTime.UtcNow;
            request.ReviewNote = dto.ReviewNote;
            request.RejectionReason = dto.Status == StatusRejected ? dto.RejectionReason : null;
            request.UpdatedAt = DateTime.UtcNow;

            if (dto.Status == StatusApproved)
            {
                var role = await _userRepository.GetRoleByNameAsync(RoleOrganizer)
                    ?? throw new InvalidOperationException($"Role '{RoleOrganizer}' is not seeded in the database.");

                bool alreadyHasRole = request.User.UserRoles.Any(ur => ur.RoleId == role.RoleId);
                if (!alreadyHasRole)
                {
                    await _userRepository.AddUserRoleAsync(new UserRole
                    {
                        UserId = request.UserId,
                        RoleId = role.RoleId,
                        AssignedAt = DateTime.UtcNow
                    });
                }
            }

            await _requestRepository.SaveChangesAsync();

            _logger.LogInformation(
                "Organizer request reviewed: RequestId={RequestId}, Status={Status}, ReviewerId={ReviewerId}.",
                requestId, dto.Status, reviewerId);

            if (dto.Status == StatusApproved || dto.Status == StatusRejected)
            {
                var targetUserId = request.UserId;
                var isApproved = dto.Status == StatusApproved;
                var userEmail = request.User?.Email;
                var userFullName = request.User?.FullName ?? "Customer";
                var rejectionReason = dto.RejectionReason;
                var reviewNote = dto.ReviewNote;

                decimal feePercent = 5.0m;
                int payoutDays = 7;
                if (isApproved)
                {
                    try
                    {
                        using var pConn = new Npgsql.NpgsqlConnection("Host=localhost;Port=5432;Database=admin_db;Username=postgres;Password=123456");
                        pConn.Open();
                        using var pCmd = new Npgsql.NpgsqlCommand("SELECT platform_fee_percent, payout_days FROM system_policies WHERE category = 'PRICE_TICKETING' AND LOWER(status) = 'active' ORDER BY version_number DESC LIMIT 1;", pConn);
                        using var pReader = pCmd.ExecuteReader();
                        if (pReader.Read())
                        {
                            if (pReader["platform_fee_percent"] != DBNull.Value) feePercent = Convert.ToDecimal(pReader["platform_fee_percent"]);
                            if (pReader["payout_days"] != DBNull.Value) payoutDays = Convert.ToInt32(pReader["payout_days"]);
                        }
                    }
                    catch { }
                }

                var bankInfoStr = !string.IsNullOrWhiteSpace(request.BankName) ? $" to {request.BankName} ({request.BankAccountNumber})" : "";
                var notifTitle = isApproved ? "Organizer Account Approved" : "Organizer Request Rejected";
                var notifMessage = isApproved
                    ? $"Congratulations! Your organizer account has been approved. Net ticket revenue (after {feePercent}% platform service fee) will be disbursed{bankInfoStr} within {payoutDays} business days after post-event settlement."
                    : $"Your organizer request has been rejected. Reason: {rejectionReason ?? "Information did not meet verification criteria."}";
                var notifCategory = isApproved ? "organizer_approved" : "organizer_rejected";
                var notifUrl = isApproved ? "/organizer/dashboard" : "/organizer/request";

                _ = Task.Run(async () =>
                {
                    if (isApproved && _approvalNotifier != null)
                    {
                        // In-app notification handled by the dedicated notifier
                        try
                        {
                            await _approvalNotifier.NotifyApprovedAsync(requestId, targetUserId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Approval notifier failed for organizer request {RequestId}", requestId);
                        }
                    }
                    else
                    {
                        // 1. Direct persistence to notification_db
                        try
                        {
                            var notifConnStr = _configuration?.GetConnectionString("NotificationConnection")
                                ?? _configuration?.GetConnectionString("NotificationDb")
                                ?? Environment.GetEnvironmentVariable("CONCERTSHIELD_NOTIFICATION_POSTGRES")
                                ?? Environment.GetEnvironmentVariable("CONCERTSHIELD_TEST_POSTGRES");
                            if (!string.IsNullOrWhiteSpace(notifConnStr))
                            {
                                using var conn = new Npgsql.NpgsqlConnection(notifConnStr);
                                await conn.OpenAsync();
                                using var cmd = new Npgsql.NpgsqlCommand(
                                    "INSERT INTO notifications (user_id, title, message, category, target_url, is_read, created_at, is_deleted) VALUES (@uid, @title, @msg, @cat, @url, false, NOW(), false);", conn);
                                cmd.Parameters.AddWithValue("uid", targetUserId);
                                cmd.Parameters.AddWithValue("title", notifTitle);
                                cmd.Parameters.AddWithValue("msg", notifMessage);
                                cmd.Parameters.AddWithValue("cat", notifCategory);
                                cmd.Parameters.AddWithValue("url", notifUrl);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Direct DB notification failed for organizer request {RequestId}", requestId);
                        }

                        // 2. HTTP Realtime ping to NotificationAPI
                        try
                        {
                            var handler = new HttpClientHandler
                            {
                                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
                            };
                            using var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(3) };
                            var payload = new
                            {
                                userId = targetUserId,
                                title = notifTitle,
                                message = notifMessage,
                                category = notifCategory,
                                targetUrl = notifUrl
                            };
                            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                            var endpoints = new[] { "https://localhost:7197/api/notifications", "http://localhost:5174/api/notifications" };
                            foreach (var ep in endpoints)
                            {
                                try
                                {
                                    var res = await httpClient.PostAsync(ep, content);
                                    if (res.IsSuccessStatusCode) break;
                                }
                                catch { }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to send HTTP notification for organizer request {RequestId}", requestId);
                        }
                    }

                    // 3. Email notification to applicant
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(userEmail))
                        {
                            await _emailService.SendOrganizerRequestResultAsync(
                                userEmail,
                                userFullName,
                                isApproved,
                                rejectionReason,
                                reviewNote
                            );
                            _logger.LogInformation("Sent organizer review email to {Email} (Status={Status})", userEmail, isApproved ? "Approved" : "Rejected");
                        }
                        else
                        {
                            _logger.LogWarning("Cannot send organizer email: User or User.Email is null for RequestId={RequestId}", requestId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send organizer review email to user for request {RequestId}", requestId);
                    }
                });
            }

            return MapToDto(request, request.User);
        }

        public async Task<OrganizerRequestResponseDTO> SubmitVerificationAsync(
            int userId,
            SubmitOrganizerVerificationDTO dto,
            string callerRole = "Organizer")
        {
            if (!string.Equals(callerRole, "Organizer", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Organizer role required to submit KYC");

            if (dto.DocumentUrls == null || dto.DocumentUrls.Count == 0)
                throw new ArgumentException("At least one document URL is required");

            if (string.IsNullOrWhiteSpace(dto.TaxCode) ||
                !System.Text.RegularExpressions.Regex.IsMatch(dto.TaxCode.Trim(), @"^\d{10}(-\d{3})?$"))
            {
                throw new ArgumentException("Invalid tax code format");
            }

            if (string.IsNullOrWhiteSpace(dto.ContactEmail) ||
                !dto.ContactEmail.Contains('@') ||
                !dto.ContactEmail.Contains('.'))
            {
                throw new ArgumentException("Invalid contact email address");
            }

            var pending = await _requestRepository.GetPendingByUserIdAsync(userId);
            if (pending != null)
                throw new InvalidOperationException("You already have a pending verification request");

            var user = await _userRepository.GetByIdAsync(userId) ?? new User
            {
                UserId = userId,
                Email = dto.ContactEmail.Trim(),
                FullName = dto.BusinessName.Trim()
            };

            var request = new OrganizerRequest
            {
                RequestId = requestIdSeed(userId),
                UserId = userId,
                User = user,
                CompanyName = dto.BusinessName.Trim(),
                TaxCode = dto.TaxCode.Trim(),
                Reason = $"KYC Verification for {dto.BusinessName.Trim()}",
                Status = "PENDING",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            int docIdx = 1;
            foreach (var url in dto.DocumentUrls)
            {
                request.Documents.Add(new OrganizerRequestDocument
                {
                    DocumentId = docIdx++,
                    DocumentType = "KYC_DOCUMENT",
                    FileName = $"doc{docIdx}.pdf",
                    FileUrl = url,
                    PreviewUrl = url,
                    IsImage = false,
                    UploadedAt = DateTime.UtcNow
                });
            }

            await _requestRepository.AddAsync(request);
            await _requestRepository.SaveChangesAsync();

            return MapToDto(request, user);
        }

        private static int requestIdSeed(int userId) => userId > 0 ? userId + 100 : 101;

        public async Task<List<OrganizerRequestResponseDTO>> GetVerificationRequestsAsync(
            string? status = null,
            int page = 1,
            int limit = 10,
            string callerRole = "Admin")
        {
            if (!string.Equals(callerRole, "Admin", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Admin access required");

            if (page < 1)
                throw new ArgumentException("Page must be >= 1");

            if (limit < 1 || limit > 100)
                throw new ArgumentException("Limit must be between 1 and 100");

            var requests = await _requestRepository.GetAllAsync(status);
            return requests
                .Skip((page - 1) * limit)
                .Take(limit)
                .Select(r => MapToDto(r, r.User))
                .ToList();
        }

        public async Task<OrganizerRequestResponseDTO> ReviewVerificationAsync(
            int requestId,
            int reviewerId,
            ReviewOrganizerRequestDTO dto,
            string callerRole = "Admin")
        {
            if (!string.Equals(callerRole, "Admin", StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("Admin privilege required");

            var decision = (dto.Decision ?? dto.Status ?? string.Empty).Trim().ToUpperInvariant();
            string normalizedStatus;
            if (decision == "APPROVE" || decision == "APPROVED")
                normalizedStatus = StatusApproved;
            else if (decision == "REJECT" || decision == "REJECTED")
                normalizedStatus = StatusRejected;
            else
                throw new ArgumentException("Invalid decision value");

            if (normalizedStatus == StatusRejected && string.IsNullOrWhiteSpace(dto.RejectionReason))
                throw new ArgumentException("RejectionReason is required when rejecting");

            var request = await _requestRepository.GetByIdAsync(requestId)
                ?? throw new KeyNotFoundException("Verification request not found");

            if (!string.Equals(request.Status, StatusPending, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Request has already been processed");

            request.Status = normalizedStatus == StatusApproved ? "APPROVED" : "REJECTED";
            request.ReviewedBy = reviewerId;
            request.ReviewedAt = DateTime.UtcNow;
            request.ReviewNote = dto.ReviewNote;
            request.RejectionReason = normalizedStatus == StatusRejected ? dto.RejectionReason : null;
            request.UpdatedAt = DateTime.UtcNow;

            if (normalizedStatus == StatusApproved)
            {
                var role = await _userRepository.GetRoleByNameAsync(RoleOrganizer);
                if (role != null && request.User != null && !request.User.UserRoles.Any(ur => ur.RoleId == role.RoleId))
                {
                    await _userRepository.AddUserRoleAsync(new UserRole
                    {
                        UserId = request.UserId,
                        RoleId = role.RoleId,
                        AssignedAt = DateTime.UtcNow
                    });
                }
            }

            await _requestRepository.SaveChangesAsync();
            return MapToDto(request, request.User ?? new User { UserId = request.UserId, Email = "organizer@example.com", FullName = "Organizer" });
        }

        private static OrganizerRequestResponseDTO MapToDto(OrganizerRequest request, User user)
        {
            return new OrganizerRequestResponseDTO
            {
                RequestId = request.RequestId,
                UserId = request.UserId,
                UserEmail = user.Email,
                UserFullName = user.FullName,

                Reason = request.Reason,
                CompanyName = request.CompanyName,
                TaxCode = request.TaxCode,
                RepresentativeName = request.RepresentativeName,
                Website = request.Website,
                PhoneNumber = request.PhoneNumber,
                Experience = request.Experience,

                BankAccountHolder = request.BankAccountHolder,
                BankAccountNumber = request.BankAccountNumber,
                BankName = request.BankName,
                BankBranch = request.BankBranch,

                Status = request.Status,
                ReviewedBy = request.ReviewedBy,
                ReviewedByName = request.ReviewedByNavigation?.FullName,
                ReviewedAt = request.ReviewedAt,
                ReviewNote = request.ReviewNote,
                RejectionReason = request.RejectionReason,

                CreatedAt = request.CreatedAt,
                UpdatedAt = request.UpdatedAt,

                DocumentsCount = request.Documents?.Count ?? 0,
                Documents = request.Documents?.Select(d => new OrganizerRequestDocumentDTO
                {
                    DocumentId = d.DocumentId,
                    RequestId = d.RequestId,
                    DocumentType = d.DocumentType,
                    FileName = d.FileName,
                    FileUrl = d.FileUrl,
                    PreviewUrl = d.PreviewUrl,
                    IsImage = d.IsImage,
                    UploadedAt = d.UploadedAt
                }).OrderBy(d => d.DocumentId).ToList() ?? new List<OrganizerRequestDocumentDTO>()
            };
        }
    }
}