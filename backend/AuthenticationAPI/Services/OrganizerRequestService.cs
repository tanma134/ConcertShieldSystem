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

        private const string RoleOrganizer = "Organizer";
        private const string StatusPending = "Pending";
        private const string StatusApproved = "Approved";
        private const string StatusRejected = "Rejected";

        public OrganizerRequestService(
            IOrganizerRequestRepository requestRepository,
            IUserRepository userRepository,
            IDocumentStorageService documentStorageService,
            IEmailService emailService,
            ILogger<OrganizerRequestService> logger)
        {
            _requestRepository = requestRepository;
            _userRepository = userRepository;
            _documentStorageService = documentStorageService;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<OrganizerRequestResponseDTO> CreateRequestAsync(int userId, CreateOrganizerRequestDTO dto)
        {
            var user = await _userRepository.GetByIdAsync(userId)
                ?? throw new KeyNotFoundException("User not found.");

            bool alreadyOrganizer = user.UserRoles.Any(ur => ur.Role.RoleName == RoleOrganizer);
            if (alreadyOrganizer)
            {
                _logger.LogWarning("Organizer request rejected: UserId={UserId} is already an Organizer.", userId);
                throw new InvalidOperationException("Tài khoản của bạn đã có vai trò Nhà tổ chức (Organizer).");
            }

            var pending = await _requestRepository.GetPendingByUserIdAsync(userId);
            if (pending != null)
            {
                _logger.LogWarning("Organizer request rejected: UserId={UserId} already has a pending request.", userId);
                throw new InvalidOperationException("Bạn đã có một yêu cầu đang chờ phê duyệt. Vui lòng đợi quản trị viên xử lý.");
            }

            // Upload mandatory documents to Cloudinary
            var licenseUpload = await _documentStorageService.UploadDocumentAsync(dto.BusinessLicenseFile, "BUSINESS_LICENSE");
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
                CompanyName = dto.CompanyName.Trim(),
                TaxCode = dto.TaxCode.Trim(),
                RepresentativeName = dto.RepresentativeName.Trim(),
                Website = string.IsNullOrWhiteSpace(dto.Website) ? null : dto.Website.Trim(),
                PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : dto.PhoneNumber.Trim(),
                Experience = string.IsNullOrWhiteSpace(dto.Experience) ? null : dto.Experience.Trim(),
                Status = StatusPending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

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
                throw new ArgumentException("Trạng thái phải là 'Approved' hoặc 'Rejected'.");

            if (dto.Status == StatusRejected && string.IsNullOrWhiteSpace(dto.RejectionReason))
                throw new ArgumentException("Lý do từ chối là bắt buộc khi từ chối yêu cầu.");

            var request = await _requestRepository.GetByIdAsync(requestId)
                ?? throw new KeyNotFoundException("Organizer request not found.");

            if (request.Status != StatusPending)
            {
                _logger.LogWarning("Review rejected: RequestId={RequestId} is not pending (current status: {Status}).", requestId, request.Status);
                throw new InvalidOperationException($"Yêu cầu này đã được {request.Status.ToLower()} trước đó.");
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
                var notifTitle = isApproved ? "Organizer Account Approved" : "Organizer Request Rejected";
                var notifMessage = isApproved
                    ? "Congratulations! Your organizer registration request has been approved. You now have full access to Organizer features and dashboard."
                    : $"Your organizer request has been rejected. Reason: {dto.RejectionReason ?? "Information did not meet verification criteria."}";
                var notifCategory = isApproved ? "organizer_approved" : "organizer_rejected";
                var notifUrl = isApproved ? "/organizer/dashboard" : "/organizer/request";

                _ = Task.Run(async () =>
                {
                    // 1. Direct persistence to notification_db
                    try
                    {
                        using var conn = new Npgsql.NpgsqlConnection("Host=localhost;Port=5432;Database=notification_db;Username=postgres;Password=123456");
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

                    // 3. Email notification to applicant
                    try
                    {
                        var userEmail = request.User?.Email;
                        var userFullName = request.User?.FullName ?? "Customer";
                        if (!string.IsNullOrWhiteSpace(userEmail))
                        {
                            await _emailService.SendOrganizerRequestResultAsync(
                                userEmail,
                                userFullName,
                                isApproved,
                                dto.RejectionReason,
                                dto.ReviewNote
                            );
                            _logger.LogInformation("Sent organizer review email to {Email} (Status={Status})", userEmail, dto.Status);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send organizer review email to user for request {RequestId}", requestId);
                    }
                });
            }

            return MapToDto(request, request.User);
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
