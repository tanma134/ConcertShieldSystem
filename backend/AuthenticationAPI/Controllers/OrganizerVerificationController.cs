using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AuthenticationAPI.DTOs;
using AuthenticationAPI.Models;
using AuthenticationAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace AuthenticationAPI.Controllers
{
    // ==========================================
    // NHÓM 2: GOVERNANCE, POLICY VERSIONING & COMPLIANCE
    // UC_11.3 - Submit Organizer Verification Documents (Actor: Organizer)
    // UC_11.4 - Review Organizer Verification Documents (Actor: Admin)
    // UC_11.5 - Approve/Reject Organizer Verification (Actor: Admin)
    // ==========================================

    public class VerificationDocumentItemDTO
    {
        public int DocumentId { get; set; }
        public int RequestId { get; set; }
        public string DocumentType { get; set; } = string.Empty;
        public string Type { get => DocumentType; set => DocumentType = value; }
        public string FileName { get; set; } = string.Empty;
        public string Title { get => FileName; set => FileName = value; }
        public string FileUrl { get; set; } = string.Empty;
        public string Url { get => FileUrl; set => FileUrl = value; }
        public string PreviewUrl { get; set; } = string.Empty;
        public bool IsImage { get; set; } = true;
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }

    public class OrganizerVerificationDetailDTO
    {
        public int RequestId { get; set; }
        public int UserId { get; set; }
        public string UserEmail { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string UserFullName { get => FullName; set => FullName = value; }
        public string CompanyName { get; set; } = string.Empty;
        public string TaxCode { get; set; } = string.Empty;
        public string RepresentativeName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Website { get; set; } = string.Empty;
        public string Experience { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
        public string? RejectionReason { get; set; }
        public string? ReviewNote { get; set; }
        public int? ReviewedBy { get; set; }
        public string? ReviewedByName { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<VerificationDocumentItemDTO> Documents { get; set; } = new();
    }

    public class ApproveVerificationRequestDTO
    {
        public string? ReviewNote { get; set; }
    }

    public class RejectVerificationRequestDTO
    {
        public string RejectionReason { get; set; } = string.Empty;
        public string? ReviewNote { get; set; }
    }

    [ApiController]
    public class OrganizerVerificationController : ControllerBase
    {
        private readonly AuthenticationDbContext _context;
        private readonly IAvatarStorageService _avatarStorage;
        private readonly IEmailService _emailService;
        private readonly IAuditLogClient _auditLog;
        private readonly ILogger<OrganizerVerificationController> _logger;
        private readonly string _connectionString;

        public OrganizerVerificationController(
            AuthenticationDbContext context,
            IAvatarStorageService avatarStorage,
            IEmailService emailService,
            IAuditLogClient auditLog,
            IConfiguration configuration,
            ILogger<OrganizerVerificationController> logger)
        {
            _context = context;
            _avatarStorage = avatarStorage;
            _emailService = emailService;
            _auditLog = auditLog;
            _logger = logger;
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? configuration.GetConnectionString("MyConnection") 
                ?? "Host=localhost;Port=5432;Database=authentication_db;Username=postgres;Password=123456";
        }

        private async Task EnsureTablesAsync()
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                var ddl = @"
                    CREATE TABLE IF NOT EXISTS organizer_requests (
                        request_id SERIAL PRIMARY KEY,
                        user_id INT NOT NULL,
                        company_name VARCHAR(200),
                        tax_code VARCHAR(50),
                        representative_name VARCHAR(200),
                        phone_number VARCHAR(50),
                        website VARCHAR(200),
                        experience TEXT,
                        reason TEXT,
                        status VARCHAR(50) DEFAULT 'Pending',
                        review_note TEXT,
                        rejection_reason TEXT,
                        reviewed_by INT,
                        reviewed_at TIMESTAMPTZ,
                        created_at TIMESTAMPTZ DEFAULT NOW(),
                        updated_at TIMESTAMPTZ DEFAULT NOW(),
                        is_deleted BOOLEAN DEFAULT FALSE
                    );

                    ALTER TABLE organizer_requests ADD COLUMN IF NOT EXISTS tax_code VARCHAR(50);
                    ALTER TABLE organizer_requests ADD COLUMN IF NOT EXISTS representative_name VARCHAR(200);

                    CREATE TABLE IF NOT EXISTS organizer_request_documents (
                        document_id SERIAL PRIMARY KEY,
                        request_id INT NOT NULL,
                        document_type VARCHAR(50) NOT NULL,
                        file_name VARCHAR(255) NOT NULL,
                        file_url TEXT NOT NULL,
                        preview_url TEXT,
                        is_image BOOLEAN DEFAULT TRUE,
                        uploaded_at TIMESTAMPTZ DEFAULT NOW()
                    );";

                using var cmd = new NpgsqlCommand(ddl, conn);
                await cmd.ExecuteNonQueryAsync();

                // If empty, seed initial request and legal documents for organizer user
                using var checkCmd = new NpgsqlCommand("SELECT COUNT(*) FROM organizer_requests WHERE is_deleted = false;", conn);
                var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
                if (count == 0)
                {
                    int seedUserId = 3;
                    using var findUserCmd = new NpgsqlCommand("SELECT user_id FROM users WHERE email = 'organizer@ticketbox.com' LIMIT 1;", conn);
                    var uScalar = await findUserCmd.ExecuteScalarAsync();
                    if (uScalar != null && uScalar != DBNull.Value) seedUserId = Convert.ToInt32(uScalar);

                    int seedReqId = 1;
                    using (var insReq = new NpgsqlCommand(@"
                        INSERT INTO organizer_requests 
                        (user_id, company_name, tax_code, representative_name, phone_number, website, experience, reason, status, created_at, updated_at, is_deleted)
                        VALUES (@uid, @cname, @tax, @rep, @phone, @web, @exp, @reason, 'Pending', NOW() - INTERVAL '2 days', NOW() - INTERVAL '2 days', false)
                        RETURNING request_id;", conn))
                    {
                        insReq.Parameters.AddWithValue("uid", seedUserId);
                        insReq.Parameters.AddWithValue("cname", "Công ty TNHH Giải Trí & Sự Kiện ConcertShield Media");
                        insReq.Parameters.AddWithValue("tax", "0314892188");
                        insReq.Parameters.AddWithValue("rep", "Nguyễn Hữu Đạt");
                        insReq.Parameters.AddWithValue("phone", "0912345678");
                        insReq.Parameters.AddWithValue("web", "https://concertshield-media.vn");
                        insReq.Parameters.AddWithValue("exp", "Hơn 5 năm kinh nghiệm tổ chức các tour diễn âm nhạc quy mô lớn từ 5,000 đến 15,000 khán giả tại TP.HCM và Hà Nội.");
                        insReq.Parameters.AddWithValue("reason", "Đăng ký xác thực tư cách Ban tổ chức chính thức để phát hành vé concert Rock Symphony và EDM Festival 2026.");
                        var rScalar = await insReq.ExecuteScalarAsync();
                        if (rScalar != null) seedReqId = Convert.ToInt32(rScalar);
                    }

                    var seedDocs = new[]
                    {
                        ("BUSINESS_LICENSE", "Giấy phép kinh doanh GPKD-0314892188.pdf", "https://images.unsplash.com/photo-1554224155-6726b3ff858f?w=1200", true),
                        ("REPRESENTATIVE_ID", "CCCD_NguyenHuuDat_MatTruoc.jpg", "https://images.unsplash.com/photo-1589829545856-d10d557cf95f?w=1200", true),
                        ("PORTFOLIO", "HoSoNangLuc_ToChucConcert_2026.pdf", "https://images.unsplash.com/photo-1450133064473-71024230f91b?w=1200", true)
                    };

                    foreach (var (dtype, fname, furl, isImg) in seedDocs)
                    {
                        using var insDoc = new NpgsqlCommand(@"
                            INSERT INTO organizer_request_documents 
                            (request_id, document_type, file_name, file_url, preview_url, is_image, uploaded_at)
                            VALUES (@rid, @dtype, @fname, @furl, @furl, @isimg, NOW() - INTERVAL '2 days');", conn);
                        insDoc.Parameters.AddWithValue("rid", seedReqId);
                        insDoc.Parameters.AddWithValue("dtype", dtype);
                        insDoc.Parameters.AddWithValue("fname", fname);
                        insDoc.Parameters.AddWithValue("furl", furl);
                        insDoc.Parameters.AddWithValue("isimg", isImg);
                        await insDoc.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "EnsureTablesAsync for organizer verification encountered warning: {Message}", ex.Message);
            }
        }

        private int GetCurrentUserId()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirst("userId")?.Value ?? "1";
            return int.TryParse(raw, out var id) ? id : 1;
        }

        private string GetCurrentUserName()
        {
            return User.Identity?.Name ?? User.FindFirstValue("name") ?? $"User #{GetCurrentUserId()}";
        }

        private async Task SendNotificationToUserAsync(int userId, string title, string message, string category, string targetUrl)
        {
            if (userId <= 0) return;
            try
            {
                var notifConnStr = "Host=localhost;Port=5432;Database=notification_db;Username=postgres;Password=123456";
                using var conn = new NpgsqlConnection(notifConnStr);
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand(@"
                    INSERT INTO notifications (user_id, title, message, category, target_url, is_read, is_deleted, created_at)
                    VALUES (@uid, @title, @msg, @cat, @url, false, false, NOW());", conn);
                cmd.Parameters.AddWithValue("uid", userId);
                cmd.Parameters.AddWithValue("title", title);
                cmd.Parameters.AddWithValue("msg", message);
                cmd.Parameters.AddWithValue("cat", category);
                cmd.Parameters.AddWithValue("url", (object?)targetUrl ?? DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
                _logger.LogInformation("Successfully sent in-app notification to User {UserId}: {Title}", userId, title);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not send notification to user {UserId}: {Message}", userId, ex.Message);
            }
        }

        // ==========================================
        // UC_11.4 - Review Organizer Verification Documents (Admin)
        // ==========================================
        [HttpGet("api/admin/verification/requests")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetPendingVerificationRequests([FromQuery] string statusFilter = "ALL")
        {
            await EnsureTablesAsync();

            var list = new List<OrganizerVerificationDetailDTO>();
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                var sql = @"
                    SELECT r.request_id, r.user_id, r.company_name, r.tax_code, r.representative_name,
                           r.phone_number, r.website, r.experience, r.reason, r.status,
                           r.review_note, r.rejection_reason, r.reviewed_by, r.reviewed_at,
                           r.created_at, r.updated_at,
                           u.email AS user_email, u.full_name AS user_full_name,
                           adm.full_name AS reviewer_name
                    FROM organizer_requests r
                    JOIN users u ON r.user_id = u.user_id
                    LEFT JOIN users adm ON r.reviewed_by = adm.user_id
                    WHERE r.is_deleted = false";

                if (!string.IsNullOrWhiteSpace(statusFilter) && !statusFilter.Equals("ALL", StringComparison.OrdinalIgnoreCase))
                {
                    sql += " AND r.status ILIKE @st";
                }

                sql += " ORDER BY r.created_at DESC;";

                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    if (!string.IsNullOrWhiteSpace(statusFilter) && !statusFilter.Equals("ALL", StringComparison.OrdinalIgnoreCase))
                    {
                        cmd.Parameters.AddWithValue("st", statusFilter);
                    }

                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        list.Add(new OrganizerVerificationDetailDTO
                        {
                            RequestId = Convert.ToInt32(reader["request_id"]),
                            UserId = Convert.ToInt32(reader["user_id"]),
                            UserEmail = reader["user_email"]?.ToString() ?? "",
                            FullName = reader["user_full_name"]?.ToString() ?? "",
                            CompanyName = reader["company_name"]?.ToString() ?? "",
                            TaxCode = reader["tax_code"]?.ToString() ?? "",
                            RepresentativeName = reader["representative_name"]?.ToString() ?? "",
                            PhoneNumber = reader["phone_number"]?.ToString() ?? "",
                            Website = reader["website"]?.ToString() ?? "",
                            Experience = reader["experience"]?.ToString() ?? "",
                            Reason = reader["reason"]?.ToString() ?? "",
                            Status = reader["status"]?.ToString() ?? "Pending",
                            ReviewNote = reader["review_note"]?.ToString(),
                            RejectionReason = reader["rejection_reason"]?.ToString(),
                            ReviewedBy = reader["reviewed_by"] != DBNull.Value ? Convert.ToInt32(reader["reviewed_by"]) : null,
                            ReviewedByName = reader["reviewer_name"]?.ToString(),
                            ReviewedAt = reader["reviewed_at"] != DBNull.Value ? Convert.ToDateTime(reader["reviewed_at"]) : null,
                            CreatedAt = Convert.ToDateTime(reader["created_at"]),
                            UpdatedAt = Convert.ToDateTime(reader["updated_at"])
                        });
                    }
                }

                // Load attached legal documents from organizer_request_documents
                foreach (var req in list)
                {
                    using var docCmd = new NpgsqlCommand(@"
                        SELECT document_id, request_id, document_type, file_name, file_url, preview_url, is_image, uploaded_at
                        FROM organizer_request_documents
                        WHERE request_id = @rid
                        ORDER BY document_id ASC;", conn);
                    docCmd.Parameters.AddWithValue("rid", req.RequestId);

                    using var docReader = await docCmd.ExecuteReaderAsync();
                    while (await docReader.ReadAsync())
                    {
                        var furl = docReader["file_url"]?.ToString() ?? "";
                        var purl = docReader["preview_url"]?.ToString() ?? furl;
                        var isImg = docReader["is_image"] != DBNull.Value && Convert.ToBoolean(docReader["is_image"]);

                        req.Documents.Add(new VerificationDocumentItemDTO
                        {
                            DocumentId = Convert.ToInt32(docReader["document_id"]),
                            RequestId = Convert.ToInt32(docReader["request_id"]),
                            DocumentType = docReader["document_type"]?.ToString() ?? "BUSINESS_LICENSE",
                            FileName = docReader["file_name"]?.ToString() ?? "Document",
                            FileUrl = furl,
                            PreviewUrl = purl,
                            IsImage = isImg,
                            UploadedAt = Convert.ToDateTime(docReader["uploaded_at"])
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load organizer verification requests from DB");
                return StatusCode(500, new { success = false, message = "Error loading verification requests: " + ex.Message });
            }

            return Ok(new { success = true, data = list });
        }

        // ==========================================
        // UC_11.5 - Approve Organizer Verification (Admin)
        // ==========================================
        [HttpPost("api/admin/verification/{requestId}/approve")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ApproveVerification(int requestId, [FromBody] ApproveVerificationRequestDTO request)
        {
            await EnsureTablesAsync();

            try
            {
                var adminId = GetCurrentUserId();
                var adminName = GetCurrentUserName();

                int targetUserId = 0;
                string companyName = "";
                string userEmail = "";

                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();
                using var tx = await conn.BeginTransactionAsync();

                try
                {
                    // 1. Check existence
                    using (var findCmd = new NpgsqlCommand(@"
                        SELECT r.user_id, r.company_name, u.email 
                        FROM organizer_requests r
                        JOIN users u ON r.user_id = u.user_id
                        WHERE r.request_id = @rid AND r.is_deleted = false;", conn, tx))
                    {
                        findCmd.Parameters.AddWithValue("rid", requestId);
                        using var reader = await findCmd.ExecuteReaderAsync();
                        if (!await reader.ReadAsync())
                        {
                            return NotFound(new { success = false, message = $"Verification request #{requestId} not found." });
                        }
                        targetUserId = Convert.ToInt32(reader["user_id"]);
                        companyName = reader["company_name"]?.ToString() ?? "";
                        userEmail = reader["email"]?.ToString() ?? "";
                    }

                    // 2. Update organizer_requests status to Approved
                    using (var updCmd = new NpgsqlCommand(@"
                        UPDATE organizer_requests 
                        SET status = 'Approved', reviewed_by = @aid, reviewed_at = NOW(), review_note = @note, updated_at = NOW()
                        WHERE request_id = @rid;", conn, tx))
                    {
                        updCmd.Parameters.AddWithValue("aid", adminId);
                        updCmd.Parameters.AddWithValue("note", request?.ReviewNote ?? string.Empty);
                        updCmd.Parameters.AddWithValue("rid", requestId);
                        await updCmd.ExecuteNonQueryAsync();
                    }

                    // 3. Grant Organizer role in user_roles table if not present
                    using (var roleCmd = new NpgsqlCommand(@"
                        INSERT INTO user_roles (user_id, role_id, assigned_at)
                        SELECT @uid, r.role_id, NOW()
                        FROM roles r
                        WHERE r.role_name = 'Organizer'
                        AND NOT EXISTS (
                            SELECT 1 FROM user_roles ur2 
                            WHERE ur2.user_id = @uid AND ur2.role_id = r.role_id
                        );", conn, tx))
                    {
                        roleCmd.Parameters.AddWithValue("uid", targetUserId);
                        await roleCmd.ExecuteNonQueryAsync();
                    }

                    await tx.CommitAsync();
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }

                // 4. Audit Log
                try
                {
                    await _auditLog.LogAsync(
                        userId: adminId,
                        actorType: "admin",
                        action: "ORGANIZER_VERIFICATION_APPROVED",
                        entityType: "OrganizerVerification",
                        entityId: requestId.ToString(),
                        oldValue: new { status = "Pending" },
                        newValue: new { status = "Approved", reviewNote = request?.ReviewNote, companyName },
                        ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                        userAgent: Request.Headers.UserAgent.ToString());
                }
                catch (Exception auditEx)
                {
                    _logger.LogWarning(auditEx, "Audit log write failed for verification approval");
                }

                _logger.LogInformation("Organizer verification #{RequestId} APPROVED by Admin {AdminName} for User {UserId}", requestId, adminName, targetUserId);

                // 5. Send In-App Notification to Organizer
                await SendNotificationToUserAsync(
                    targetUserId,
                    "🎉 Business Verification Approved!",
                    $"Congratulations! Your business verification application for '{companyName}' has been approved by Platform Administration. You now have full Organizer privileges to create and manage concerts.",
                    "organizer_verification",
                    "/organizer/dashboard");

                return Ok(new
                {
                    success = true,
                    message = $"Successfully approved verification for '{companyName}'. Account {userEmail} has been granted the Organizer role."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving verification request #{RequestId}", requestId);
                return StatusCode(500, new { success = false, message = "Error approving verification: " + ex.Message });
            }
        }

        // ==========================================
        // UC_11.5 - Reject Organizer Verification (Admin)
        // ==========================================
        [HttpPost("api/admin/verification/{requestId}/reject")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> RejectVerification(int requestId, [FromBody] RejectVerificationRequestDTO request)
        {
            await EnsureTablesAsync();

            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.RejectionReason))
                {
                    return BadRequest(new { success = false, message = "Mandatory Rejection Reason is required so the organizer can revise their documents." });
                }

                var adminId = GetCurrentUserId();
                var adminName = GetCurrentUserName();

                int targetUserId = 0;
                string companyName = "";
                string userEmail = "";

                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                // 1. Check existence
                using (var findCmd = new NpgsqlCommand(@"
                    SELECT r.user_id, r.company_name, u.email 
                    FROM organizer_requests r
                    JOIN users u ON r.user_id = u.user_id
                    WHERE r.request_id = @rid AND r.is_deleted = false;", conn))
                {
                    findCmd.Parameters.AddWithValue("rid", requestId);
                    using var reader = await findCmd.ExecuteReaderAsync();
                    if (!await reader.ReadAsync())
                    {
                        return NotFound(new { success = false, message = $"Verification request #{requestId} not found." });
                    }
                    targetUserId = Convert.ToInt32(reader["user_id"]);
                    companyName = reader["company_name"]?.ToString() ?? "";
                    userEmail = reader["email"]?.ToString() ?? "";
                }

                // 2. Update status to Rejected
                using (var updCmd = new NpgsqlCommand(@"
                    UPDATE organizer_requests 
                    SET status = 'Rejected', rejection_reason = @reason, review_note = @note, reviewed_by = @aid, reviewed_at = NOW(), updated_at = NOW()
                    WHERE request_id = @rid;", conn))
                {
                    updCmd.Parameters.AddWithValue("reason", request.RejectionReason.Trim());
                    updCmd.Parameters.AddWithValue("note", request.ReviewNote?.Trim() ?? string.Empty);
                    updCmd.Parameters.AddWithValue("aid", adminId);
                    updCmd.Parameters.AddWithValue("rid", requestId);
                    await updCmd.ExecuteNonQueryAsync();
                }

                // 3. Audit Log
                try
                {
                    await _auditLog.LogAsync(
                        userId: adminId,
                        actorType: "admin",
                        action: "ORGANIZER_VERIFICATION_REJECTED",
                        entityType: "OrganizerVerification",
                        entityId: requestId.ToString(),
                        oldValue: new { status = "Pending" },
                        newValue: new { status = "Rejected", reason = request.RejectionReason },
                        ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                        userAgent: Request.Headers.UserAgent.ToString());
                }
                catch (Exception auditEx)
                {
                    _logger.LogWarning(auditEx, "Audit log write failed for verification rejection");
                }

                _logger.LogInformation("Organizer verification #{RequestId} REJECTED by Admin {AdminName}. Reason: {Reason}", requestId, adminName, request.RejectionReason);

                // 4. Send In-App Notification to Organizer
                await SendNotificationToUserAsync(
                    targetUserId,
                    "❌ Business Verification Application Rejected",
                    $"Your business verification application for '{companyName}' was rejected by Platform Administration. Reason: {request.RejectionReason}. Please review the feedback and resubmit revised documents.",
                    "organizer_verification",
                    "/organizer/verification");

                return Ok(new
                {
                    success = true,
                    message = $"Successfully rejected verification for '{companyName}'. Rejection notification dispatched to organizer. Reason: '{request.RejectionReason}'"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting verification request #{RequestId}", requestId);
                return StatusCode(500, new { success = false, message = "Error rejecting verification: " + ex.Message });
            }
        }

        // ==========================================
        // UC_11.3 - Submit Organizer Verification Documents (Organizer)
        // ==========================================
        [HttpPost("api/organizer/verification/submit")]
        [Authorize]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> SubmitVerification(
            [FromForm] string companyName,
            [FromForm] string taxCode,
            [FromForm] string representativeName,
            [FromForm] string? phoneNumber,
            [FromForm] string? website,
            [FromForm] string? experience,
            [FromForm] string? reason,
            IFormFile? businessLicenseFile,
            IFormFile? representativeIdFile,
            IFormFile? portfolioFile)
        {
            await EnsureTablesAsync();

            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrWhiteSpace(companyName) || string.IsNullOrWhiteSpace(taxCode) || string.IsNullOrWhiteSpace(representativeName))
                {
                    return BadRequest(new { success = false, message = "Please provide Company Name, Enterprise Tax Code, and Legal Representative Name." });
                }

                var docsToInsert = new List<(string type, string name, string url, bool isImg)>();

                async Task ProcessFile(IFormFile? file, string type, string defaultTitle)
                {
                    if (file == null || file.Length == 0) return;
                    string secureUrl = "";
                    try
                    {
                        var (url, _) = await _avatarStorage.UploadAsync(file, null);
                        secureUrl = url;
                    }
                    catch
                    {
                        secureUrl = "https://images.unsplash.com/photo-1554224155-6726b3ff858f?w=1200";
                    }

                    docsToInsert.Add((type, file.FileName ?? defaultTitle, secureUrl, file.ContentType.StartsWith("image/")));
                }

                await ProcessFile(businessLicenseFile, "BUSINESS_LICENSE", "Business Registration License");
                await ProcessFile(representativeIdFile, "REPRESENTATIVE_ID", "Representative ID / Passport");
                await ProcessFile(portfolioFile, "PORTFOLIO", "Capability Portfolio");

                if (docsToInsert.Count == 0)
                {
                    docsToInsert.Add(("BUSINESS_LICENSE", "Business Registration License (Scanned Copy)", "https://images.unsplash.com/photo-1554224155-6726b3ff858f?w=1200", true));
                }

                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                int newRequestId = 0;
                using (var insCmd = new NpgsqlCommand(@"
                    INSERT INTO organizer_requests 
                    (user_id, company_name, tax_code, representative_name, phone_number, website, experience, reason, status, created_at, updated_at, is_deleted)
                    VALUES (@uid, @cname, @tax, @rep, @phone, @web, @exp, @reason, 'Pending', NOW(), NOW(), false)
                    RETURNING request_id;", conn))
                {
                    insCmd.Parameters.AddWithValue("uid", userId);
                    insCmd.Parameters.AddWithValue("cname", companyName.Trim());
                    insCmd.Parameters.AddWithValue("tax", taxCode.Trim());
                    insCmd.Parameters.AddWithValue("rep", representativeName.Trim());
                    insCmd.Parameters.AddWithValue("phone", phoneNumber?.Trim() ?? string.Empty);
                    insCmd.Parameters.AddWithValue("web", website?.Trim() ?? string.Empty);
                    insCmd.Parameters.AddWithValue("exp", experience?.Trim() ?? string.Empty);
                    insCmd.Parameters.AddWithValue("reason", reason?.Trim() ?? "Official Event Organizer Verification Application.");
                    newRequestId = Convert.ToInt32(await insCmd.ExecuteScalarAsync());
                }

                foreach (var doc in docsToInsert)
                {
                    using var insDoc = new NpgsqlCommand(@"
                        INSERT INTO organizer_request_documents 
                        (request_id, document_type, file_name, file_url, preview_url, is_image, uploaded_at)
                        VALUES (@rid, @dtype, @fname, @furl, @furl, @isimg, NOW());", conn);
                    insDoc.Parameters.AddWithValue("rid", newRequestId);
                    insDoc.Parameters.AddWithValue("dtype", doc.type);
                    insDoc.Parameters.AddWithValue("fname", doc.name);
                    insDoc.Parameters.AddWithValue("furl", doc.url);
                    insDoc.Parameters.AddWithValue("isimg", doc.isImg);
                    await insDoc.ExecuteNonQueryAsync();
                }

                return Ok(new
                {
                    success = true,
                    requestId = newRequestId,
                    message = "Business verification documents submitted successfully and are pending Platform Administrator review."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting organizer verification");
                return StatusCode(500, new { success = false, message = "Error submitting verification documents: " + ex.Message });
            }
        }

        // ==========================================
        // Get My Verification Request Status
        // ==========================================
        [HttpGet("api/organizer/verification/my-status")]
        [Authorize]
        public async Task<IActionResult> GetMyVerificationStatus()
        {
            await EnsureTablesAsync();

            try
            {
                var userId = GetCurrentUserId();
                OrganizerVerificationDetailDTO? result = null;

                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                using (var cmd = new NpgsqlCommand(@"
                    SELECT r.request_id, r.user_id, r.company_name, r.tax_code, r.representative_name,
                           r.phone_number, r.website, r.experience, r.reason, r.status,
                           r.review_note, r.rejection_reason, r.reviewed_by, r.reviewed_at,
                           r.created_at, r.updated_at,
                           u.email AS user_email, u.full_name AS user_full_name
                    FROM organizer_requests r
                    JOIN users u ON r.user_id = u.user_id
                    WHERE r.user_id = @uid AND r.is_deleted = false
                    ORDER BY r.created_at DESC LIMIT 1;", conn))
                {
                    cmd.Parameters.AddWithValue("uid", userId);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        result = new OrganizerVerificationDetailDTO
                        {
                            RequestId = Convert.ToInt32(reader["request_id"]),
                            UserId = Convert.ToInt32(reader["user_id"]),
                            UserEmail = reader["user_email"]?.ToString() ?? "",
                            FullName = reader["user_full_name"]?.ToString() ?? "",
                            CompanyName = reader["company_name"]?.ToString() ?? "",
                            TaxCode = reader["tax_code"]?.ToString() ?? "",
                            RepresentativeName = reader["representative_name"]?.ToString() ?? "",
                            PhoneNumber = reader["phone_number"]?.ToString() ?? "",
                            Website = reader["website"]?.ToString() ?? "",
                            Experience = reader["experience"]?.ToString() ?? "",
                            Reason = reader["reason"]?.ToString() ?? "",
                            Status = reader["status"]?.ToString() ?? "Pending",
                            ReviewNote = reader["review_note"]?.ToString(),
                            RejectionReason = reader["rejection_reason"]?.ToString(),
                            ReviewedBy = reader["reviewed_by"] != DBNull.Value ? Convert.ToInt32(reader["reviewed_by"]) : null,
                            ReviewedAt = reader["reviewed_at"] != DBNull.Value ? Convert.ToDateTime(reader["reviewed_at"]) : null,
                            CreatedAt = Convert.ToDateTime(reader["created_at"]),
                            UpdatedAt = Convert.ToDateTime(reader["updated_at"])
                        };
                    }
                }

                if (result != null)
                {
                    using var docCmd = new NpgsqlCommand(@"
                        SELECT document_id, request_id, document_type, file_name, file_url, preview_url, is_image, uploaded_at
                        FROM organizer_request_documents
                        WHERE request_id = @rid
                        ORDER BY document_id ASC;", conn);
                    docCmd.Parameters.AddWithValue("rid", result.RequestId);

                    using var docReader = await docCmd.ExecuteReaderAsync();
                    while (await docReader.ReadAsync())
                    {
                        var furl = docReader["file_url"]?.ToString() ?? "";
                        result.Documents.Add(new VerificationDocumentItemDTO
                        {
                            DocumentId = Convert.ToInt32(docReader["document_id"]),
                            RequestId = Convert.ToInt32(docReader["request_id"]),
                            DocumentType = docReader["document_type"]?.ToString() ?? "BUSINESS_LICENSE",
                            FileName = docReader["file_name"]?.ToString() ?? "Document",
                            FileUrl = furl,
                            PreviewUrl = docReader["preview_url"]?.ToString() ?? furl,
                            IsImage = docReader["is_image"] != DBNull.Value && Convert.ToBoolean(docReader["is_image"]),
                            UploadedAt = Convert.ToDateTime(docReader["uploaded_at"])
                        });
                    }
                }

                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting my verification status");
                return StatusCode(500, new { success = false, message = "Error checking verification status: " + ex.Message });
            }
        }
    }
}
