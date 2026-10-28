using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using AdminAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace AdminAPI.Controllers
{
    // ==========================================
    // NHÓM 2: GOVERNANCE, POLICY VERSIONING & COMPLIANCE
    // UC_11.1 - View Policy Version History (Actor: Admin)
    // UC_11.2 - Create New Policy Version (Actor: Admin)
    // ==========================================

    public class PolicyVersionDTO
    {
        public int PolicyId { get; set; }
        public int VersionId => PolicyId;
        public string Category { get; set; } = string.Empty;
        public string VersionCode { get; set; } = string.Empty;
        public int VersionNumber { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public string ChangeSummary { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Status { get; set; } = "Active"; // Active, Archived
        public DateTime EffectiveDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public int CreatedByAdminId { get; set; }
        public string CreatedByAdminName { get; set; } = "Platform Administrator";
    }

    public class CreatePolicyVersionRequestDTO
    {
        public string Category { get; set; } = "PRICE_TICKETING";
        public string Title { get; set; } = string.Empty;
        public string Scope { get; set; } = "Toàn bộ người dùng & Ban tổ chức";
        public string ChangeSummary { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime? EffectiveDate { get; set; }
    }

    [ApiController]
    [Route("api/admin/policies")]
    [Authorize(Roles = "Admin")]
    public class SystemPoliciesController : ControllerBase
    {
        private readonly IAuditLogService _auditLog;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SystemPoliciesController> _logger;
        private readonly string _connectionString;

        public SystemPoliciesController(
            IAuditLogService auditLog,
            IConfiguration configuration,
            ILogger<SystemPoliciesController> logger)
        {
            _auditLog = auditLog;
            _configuration = configuration;
            _logger = logger;
            _connectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? "Host=localhost;Port=5432;Database=admin_db;Username=postgres;Password=123456";
        }

        private int GetCurrentAdminId()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirst("userId")?.Value ?? "1";
            return int.TryParse(raw, out var id) ? id : 1;
        }

        private string GetCurrentAdminName()
        {
            return User.FindFirstValue(ClaimTypes.Name)
                ?? User.FindFirstValue("fullName")
                ?? User.FindFirstValue("name")
                ?? "Platform Administrator";
        }

        /// <summary>
        /// UC_11.1: View Policy Version History (Category: PRICE_TICKETING, REFUND_CANCELLATION, VIRTUAL_QUEUE, RISK_ANTI_FRAUD, TERMS_OF_SERVICE)
        /// Directly queried from system_policies table in admin_db.
        /// </summary>
        [HttpGet("history")]
        public async Task<IActionResult> GetPolicyHistory([FromQuery] string? category = "PRICE_TICKETING")
        {
            category = NormalizeCategory(category);
            var items = new List<PolicyVersionDTO>();

            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                var sql = @"
                    SELECT policy_id, category, version_code, version_number, title, scope, change_summary, content, 
                           status, effective_date, created_at, created_by_admin_id, created_by_admin_name
                    FROM system_policies
                    WHERE category = @cat
                    ORDER BY version_number DESC, created_at DESC;";

                using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("cat", category);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    items.Add(new PolicyVersionDTO
                    {
                        PolicyId = Convert.ToInt32(reader["policy_id"]),
                        Category = Convert.ToString(reader["category"]) ?? category,
                        VersionCode = Convert.ToString(reader["version_code"]) ?? "",
                        VersionNumber = Convert.ToInt32(reader["version_number"]),
                        Title = Convert.ToString(reader["title"]) ?? "",
                        Scope = Convert.ToString(reader["scope"]) ?? "",
                        ChangeSummary = Convert.ToString(reader["change_summary"]) ?? "",
                        Content = Convert.ToString(reader["content"]) ?? "",
                        Status = Convert.ToString(reader["status"]) ?? "Active",
                        EffectiveDate = Convert.ToDateTime(reader["effective_date"]),
                        CreatedAt = Convert.ToDateTime(reader["created_at"]),
                        CreatedByAdminId = Convert.ToInt32(reader["created_by_admin_id"]),
                        CreatedByAdminName = Convert.ToString(reader["created_by_admin_name"]) ?? "Platform Administrator"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load policy history for category {Category} from admin_db", category);
                return StatusCode(500, new { success = false, message = "Không thể tải lịch sử chính sách từ cơ sở dữ liệu." });
            }

            return Ok(new
            {
                success = true,
                category,
                totalVersions = items.Count,
                data = items
            });
        }

        /// <summary>
        /// Get Active Policy Document for each Category from database (Public for registration & login policy review).
        /// </summary>
        [AllowAnonymous]
        [HttpGet("active")]
        public async Task<IActionResult> GetActivePolicies()
        {
            var activePolicies = new Dictionary<string, PolicyVersionDTO>();

            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                var sql = @"
                    SELECT DISTINCT ON (category) 
                           policy_id, category, version_code, version_number, title, scope, change_summary, content, 
                           status, effective_date, created_at, created_by_admin_id, created_by_admin_name
                    FROM system_policies
                    WHERE status = 'Active'
                    ORDER BY category, version_number DESC;";

                using var cmd = new NpgsqlCommand(sql, conn);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var cat = Convert.ToString(reader["category"]) ?? "";
                    activePolicies[cat] = new PolicyVersionDTO
                    {
                        PolicyId = Convert.ToInt32(reader["policy_id"]),
                        Category = cat,
                        VersionCode = Convert.ToString(reader["version_code"]) ?? "",
                        VersionNumber = Convert.ToInt32(reader["version_number"]),
                        Title = Convert.ToString(reader["title"]) ?? "",
                        Scope = Convert.ToString(reader["scope"]) ?? "",
                        ChangeSummary = Convert.ToString(reader["change_summary"]) ?? "",
                        Content = Convert.ToString(reader["content"]) ?? "",
                        Status = "Active",
                        EffectiveDate = Convert.ToDateTime(reader["effective_date"]),
                        CreatedAt = Convert.ToDateTime(reader["created_at"]),
                        CreatedByAdminId = Convert.ToInt32(reader["created_by_admin_id"]),
                        CreatedByAdminName = Convert.ToString(reader["created_by_admin_name"]) ?? "Platform Administrator"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load active policies from admin_db");
            }

            return Ok(new
            {
                success = true,
                data = activePolicies
            });
        }

        /// <summary>
        /// UC_11.2: Create / Publish New Policy Version (Actor: Admin)
        /// Archives previous active versions and inserts new active policy document into admin_db.
        /// </summary>
        [HttpPost("create")]
        public async Task<IActionResult> CreatePolicyVersion([FromBody] CreatePolicyVersionRequestDTO request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return BadRequest(new { success = false, message = "Tiêu đề văn bản chính sách không được để trống." });

            if (string.IsNullOrWhiteSpace(request.ChangeSummary))
                return BadRequest(new { success = false, message = "Lý do ban hành và tóm tắt nội dung thay đổi là bắt buộc." });

            if (string.IsNullOrWhiteSpace(request.Content))
                return BadRequest(new { success = false, message = "Toàn văn nội dung chính sách không được để trống." });

            var category = NormalizeCategory(request.Category);
            var adminId = GetCurrentAdminId();
            var adminName = GetCurrentAdminName();
            var effectiveDate = request.EffectiveDate ?? DateTime.UtcNow;

            PolicyVersionDTO newPolicy;

            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();
                using var tx = await conn.BeginTransactionAsync();

                // 1. Determine next version number
                var maxVerSql = "SELECT COALESCE(MAX(version_number), 0) FROM system_policies WHERE category = @cat;";
                int nextVer;
                using (var cmd = new NpgsqlCommand(maxVerSql, conn, tx))
                {
                    cmd.Parameters.AddWithValue("cat", category);
                    nextVer = Convert.ToInt32(await cmd.ExecuteScalarAsync()) + 1;
                }

                // 2. Archive previous active versions for this category
                var archiveSql = "UPDATE system_policies SET status = 'Archived' WHERE category = @cat AND status = 'Active';";
                using (var cmd = new NpgsqlCommand(archiveSql, conn, tx))
                {
                    cmd.Parameters.AddWithValue("cat", category);
                    await cmd.ExecuteNonQueryAsync();
                }

                // 3. Generate version code (e.g. POL-PRICE-V2.0)
                var prefix = category switch
                {
                    "PRICE_TICKETING" => "PRICE",
                    "REFUND_CANCELLATION" => "REFUND",
                    "VIRTUAL_QUEUE" => "QUEUE",
                    "RISK_ANTI_FRAUD" => "RISK",
                    _ => "TOS"
                };
                var versionCode = $"POL-{prefix}-V{nextVer}.0";

                // 4. Insert new policy version
                var insertSql = @"
                    INSERT INTO system_policies (category, version_code, version_number, title, scope, change_summary, content, 
                                                status, effective_date, created_at, created_by_admin_id, created_by_admin_name)
                    VALUES (@cat, @code, @num, @title, @scope, @sum, @content, 'Active', @eff, NOW(), @aid, @aname)
                    RETURNING policy_id, created_at;";

                int newPolicyId;
                DateTime createdAt;

                using (var cmd = new NpgsqlCommand(insertSql, conn, tx))
                {
                    cmd.Parameters.AddWithValue("cat", category);
                    cmd.Parameters.AddWithValue("code", versionCode);
                    cmd.Parameters.AddWithValue("num", nextVer);
                    cmd.Parameters.AddWithValue("title", request.Title.Trim());
                    cmd.Parameters.AddWithValue("scope", string.IsNullOrWhiteSpace(request.Scope) ? "Toàn bộ người dùng & Ban tổ chức" : request.Scope.Trim());
                    cmd.Parameters.AddWithValue("sum", request.ChangeSummary.Trim());
                    cmd.Parameters.AddWithValue("content", request.Content.Trim());
                    cmd.Parameters.AddWithValue("eff", effectiveDate);
                    cmd.Parameters.AddWithValue("aid", adminId);
                    cmd.Parameters.AddWithValue("aname", adminName);

                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        newPolicyId = Convert.ToInt32(reader["policy_id"]);
                        createdAt = Convert.ToDateTime(reader["created_at"]);
                    }
                    else
                    {
                        throw new InvalidOperationException("Failed to insert policy version into database.");
                    }
                }

                await tx.CommitAsync();

                newPolicy = new PolicyVersionDTO
                {
                    PolicyId = newPolicyId,
                    Category = category,
                    VersionCode = versionCode,
                    VersionNumber = nextVer,
                    Title = request.Title.Trim(),
                    Scope = string.IsNullOrWhiteSpace(request.Scope) ? "Toàn bộ người dùng & Ban tổ chức" : request.Scope.Trim(),
                    ChangeSummary = request.ChangeSummary.Trim(),
                    Content = request.Content.Trim(),
                    Status = "Active",
                    EffectiveDate = effectiveDate,
                    CreatedAt = createdAt,
                    CreatedByAdminId = adminId,
                    CreatedByAdminName = adminName
                };

                // Audit Log to AuditLogs table in database
                await _auditLog.LogAsync(
                    userId: adminId,
                    actorType: "ADMIN",
                    action: "POLICY_PUBLISHED",
                    entityType: "SystemPolicy",
                    entityId: newPolicyId.ToString(),
                    oldValue: null,
                    newValue: new
                    {
                        category,
                        versionCode,
                        versionNumber = nextVer,
                        title = newPolicy.Title,
                        scope = newPolicy.Scope,
                        effectiveDate,
                        notes = $"Ban hành chính sách mới {versionCode}: {newPolicy.Title} (Lý do: {newPolicy.ChangeSummary})"
                    }
                );

                _logger.LogInformation("Policy version {VersionCode} published by Admin {AdminName} in admin_db", versionCode, adminName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create policy version in admin_db for category {Category}", category);
                return StatusCode(500, new { success = false, message = "Lỗi máy chủ khi lưu chính sách vào cơ sở dữ liệu: " + ex.Message });
            }

            return Ok(new
            {
                success = true,
                message = $"Ban hành thành công phiên bản chính sách mới {newPolicy.VersionCode}!",
                data = newPolicy
            });
        }

        private static string NormalizeCategory(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "PRICE_TICKETING";
            var upper = raw.Trim().ToUpperInvariant();
            if (upper.Contains("PRICE")) return "PRICE_TICKETING";
            if (upper.Contains("REFUND")) return "REFUND_CANCELLATION";
            if (upper.Contains("QUEUE")) return "VIRTUAL_QUEUE";
            if (upper.Contains("RISK")) return "RISK_ANTI_FRAUD";
            if (upper.Contains("TERMS") || upper.Contains("TOS")) return "TERMS_OF_SERVICE";
            return "PRICE_TICKETING";
        }
    }
}
