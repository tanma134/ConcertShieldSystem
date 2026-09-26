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
        private readonly ILogger<OrganizerRequestService> _logger;

        private const string RoleOrganizer = "Organizer";
        private const string StatusPending = "Pending";
        private const string StatusApproved = "Approved";
        private const string StatusRejected = "Rejected";

        public OrganizerRequestService(
            IOrganizerRequestRepository requestRepository,
            IUserRepository userRepository,
            ILogger<OrganizerRequestService> logger)
        {
            _requestRepository = requestRepository;
            _userRepository = userRepository;
            _logger = logger;
        }

        public async Task<OrganizerRequestResponseDTO> CreateRequestAsync(int userId, CreateOrganizerRequestDTO dto)
        {
            // TODO: doi ten ham cho khop IUserRepository hien co (vi du GetByIdAsync(int))
            var user = await _userRepository.GetByIdAsync(userId)
                ?? throw new KeyNotFoundException("User not found.");

            bool alreadyOrganizer = user.UserRoles.Any(ur => ur.Role.RoleName == RoleOrganizer);
            if (alreadyOrganizer)
            {
                _logger.LogWarning("Organizer request rejected at creation: UserId={UserId} is already an Organizer.", userId);
                throw new InvalidOperationException("Your account already has the Organizer role.");
            }

            var pending = await _requestRepository.GetPendingByUserIdAsync(userId);
            if (pending != null)
            {
                _logger.LogWarning("Organizer request rejected: UserId={UserId} already has a pending request.", userId);
                throw new InvalidOperationException("You already have a pending organizer request.");
            }

            var request = new OrganizerRequest
            {
                UserId = userId,
                Reason = dto.Reason,
                CompanyName = dto.CompanyName,
                Website = dto.Website,
                PhoneNumber = dto.PhoneNumber,
                Experience = dto.Experience,
                Status = StatusPending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            await _requestRepository.AddAsync(request);
            await _requestRepository.SaveChangesAsync();

            _logger.LogInformation("Organizer request created: UserId={UserId}, RequestId={RequestId}.", userId, request.RequestId);

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

            if (dto.Status == StatusApproved)
            {
                var targetUserId = request.UserId;
                _ = Task.Run(async () =>
                {
                    // 1. Direct persistence to notification_db
                    try
                    {
                        using var conn = new Npgsql.NpgsqlConnection("Host=localhost;Port=5432;Database=notification_db;Username=postgres;Password=123456");
                        await conn.OpenAsync();
                        using var cmd = new Npgsql.NpgsqlCommand(
                            "INSERT INTO notifications (user_id, title, message, content, type, category, target_url, is_read, created_at, is_deleted) VALUES (@uid, @title, @msg, @content, @type, @cat, @url, false, NOW(), false);", conn);
                        cmd.Parameters.AddWithValue("uid", targetUserId);
                        cmd.Parameters.AddWithValue("title", "Yêu cầu nâng cấp Organizer đã được duyệt");
                        var msg = "Chúc mừng! Yêu cầu đăng ký làm Nhà tổ chức sự kiện (Organizer) của bạn đã được Admin phê duyệt.";
                        cmd.Parameters.AddWithValue("msg", msg);
                        cmd.Parameters.AddWithValue("content", msg);
                        cmd.Parameters.AddWithValue("type", "organizer_approved");
                        cmd.Parameters.AddWithValue("cat", "organizer_approved");
                        cmd.Parameters.AddWithValue("url", "/organizer/dashboard");
                        await cmd.ExecuteNonQueryAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Direct DB notification failed for organizer request {RequestId}", requestId);
                    }

                    // 2. HTTP Realtime ping
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
                            title = "Yêu cầu nâng cấp Organizer đã được duyệt",
                            message = "Chúc mừng! Yêu cầu đăng ký làm Nhà tổ chức sự kiện (Organizer) của bạn đã được Admin phê duyệt.",
                            category = "organizer_approved",
                            targetUrl = "/organizer/dashboard"
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
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send HTTP notification for approved organizer request {RequestId}", requestId);
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
                UpdatedAt = request.UpdatedAt
            };
        }
    }
}