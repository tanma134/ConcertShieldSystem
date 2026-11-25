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
        public decimal PlatformFeePercent { get; set; } = 5.0m;
        public int PayoutDays { get; set; } = 7;
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
        public string Scope { get; set; } = "All Platform Attendees & Event Organizers";
        public string ChangeSummary { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public decimal? PlatformFeePercent { get; set; } = 5.0m;
        public int? PayoutDays { get; set; } = 7;
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
                           platform_fee_percent, payout_days,
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
                        PlatformFeePercent = reader["platform_fee_percent"] != DBNull.Value ? Convert.ToDecimal(reader["platform_fee_percent"]) : 5.0m,
                        PayoutDays = reader["payout_days"] != DBNull.Value ? Convert.ToInt32(reader["payout_days"]) : 7,
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
                return StatusCode(500, new { success = false, message = "Could not load policy version history from database." });
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
                           platform_fee_percent, payout_days,
                           status, effective_date, created_at, created_by_admin_id, created_by_admin_name
                    FROM system_policies
                    WHERE LOWER(status) = 'active'
                    ORDER BY category, version_number DESC, policy_id DESC;";

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
                        PlatformFeePercent = reader["platform_fee_percent"] != DBNull.Value ? Convert.ToDecimal(reader["platform_fee_percent"]) : 5.0m,
                        PayoutDays = reader["payout_days"] != DBNull.Value ? Convert.ToInt32(reader["payout_days"]) : 7,
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

            var pricingPolicy = activePolicies.ContainsKey("PRICE_TICKETING") ? activePolicies["PRICE_TICKETING"]
                              : activePolicies.Values.FirstOrDefault();

            return Ok(new
            {
                success = true,
                data = activePolicies,
                currentPricing = new
                {
                    platformFeePercent = pricingPolicy?.PlatformFeePercent ?? 5.0m,
                    payoutDays = pricingPolicy?.PayoutDays ?? 7,
                    versionCode = pricingPolicy?.VersionCode ?? "v1.0",
                    effectiveDate = pricingPolicy?.EffectiveDate ?? DateTime.UtcNow
                }
            });
        }

        /// <summary>
        /// Public endpoint for Become Organizer page and registration/login policy consent.
        /// Returns the currently active platform fee percentage and payout settlement days.
        /// </summary>
        [AllowAnonymous]
        [HttpGet("pricing-terms")]
        public async Task<IActionResult> GetPricingTerms()
        {
            try
            {
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                var sql = @"
                    SELECT policy_id, category, version_code, version_number, title, scope, change_summary, content, 
                           platform_fee_percent, payout_days, effective_date, created_at
                    FROM system_policies
                    WHERE category = 'PRICE_TICKETING' AND LOWER(status) = 'active'
                    ORDER BY version_number DESC, policy_id DESC
                    LIMIT 1;";

                using var cmd = new NpgsqlCommand(sql, conn);
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return Ok(new
                    {
                        success = true,
                        data = new
                        {
                            policyId = Convert.ToInt32(reader["policy_id"]),
                            category = Convert.ToString(reader["category"]),
                            versionCode = Convert.ToString(reader["version_code"]),
                            versionNumber = Convert.ToInt32(reader["version_number"]),
                            title = Convert.ToString(reader["title"]),
                            scope = Convert.ToString(reader["scope"]),
                            changeSummary = Convert.ToString(reader["change_summary"]),
                            content = Convert.ToString(reader["content"]),
                            platformFeePercent = reader["platform_fee_percent"] != DBNull.Value ? Convert.ToDecimal(reader["platform_fee_percent"]) : 5.0m,
                            payoutDays = reader["payout_days"] != DBNull.Value ? Convert.ToInt32(reader["payout_days"]) : 7,
                            effectiveDate = Convert.ToDateTime(reader["effective_date"]),
                            createdAt = Convert.ToDateTime(reader["created_at"])
                        }
                    });
                }

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        policyId = 0,
                        category = "PRICE_TICKETING",
                        versionCode = "POL-PRICE-V1.0",
                        versionNumber = 1,
                        title = "Standard Platform Pricing & Fee Policy",
                        scope = "All Platform Attendees & Event Organizers",
                        changeSummary = "Default standard policy terms",
                        content = "ARTICLE 1. OBJECTIVES AND APPLICABLE SCOPE\n1. This platform policy establishes binding operational, ticketing, and financial standards across ConcertShield.\n\nARTICLE 2. OPERATIONAL REQUIREMENTS AND COMPLIANCE MANDATES\n1. All participants must adhere strictly to platform regulations regarding ticket issuance, platform fee deductions (5.0%), and payout schedules (7 business days).\n\nARTICLE 3. EFFECTIVE DATE AND ENFORCEMENT\n1. This policy version takes legal effect immediately upon publication on the system.",
                        platformFeePercent = 5.0m,
                        payoutDays = 7,
                        effectiveDate = DateTime.UtcNow,
                        createdAt = DateTime.UtcNow
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load active pricing policy from admin_db");
                return StatusCode(500, new { success = false, message = "Could not load pricing policy terms." });
            }
        }

        /// <summary>
        /// UC_11.2: Create / Publish New Policy Version (Actor: Admin)
        /// Archives previous active versions and inserts new active policy document into admin_db.
        /// </summary>
        [HttpPost("create")]
        public async Task<IActionResult> CreatePolicyVersion([FromBody] CreatePolicyVersionRequestDTO request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return BadRequest(new { success = false, message = "Policy document title is required." });

            if (string.IsNullOrWhiteSpace(request.ChangeSummary))
                return BadRequest(new { success = false, message = "Enactment justification / change summary is required." });

            if (string.IsNullOrWhiteSpace(request.Content))
                return BadRequest(new { success = false, message = "Full policy terms and content cannot be empty." });

            var feePercent = request.PlatformFeePercent ?? 5.0m;
            if (feePercent < 0 || feePercent > 100)
                return BadRequest(new { success = false, message = "Platform fee percentage must be between 0% and 100%." });

            var payoutDays = request.PayoutDays ?? 7;
            if (payoutDays < 1 || payoutDays > 60)
                return BadRequest(new { success = false, message = "Payout settlement timeline must be between 1 and 60 business days." });

            if (request.EffectiveDate.HasValue && request.EffectiveDate.Value.ToUniversalTime().Date < DateTime.UtcNow.Date)
            {
                return BadRequest(new { success = false, message = "Effective start date cannot be earlier than today." });
            }

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
                var archiveSql = "UPDATE system_policies SET status = 'Archived' WHERE category = @cat AND LOWER(status) = 'active';";
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
                                                platform_fee_percent, payout_days,
                                                status, effective_date, created_at, created_by_admin_id, created_by_admin_name)
                    VALUES (@cat, @code, @num, @title, @scope, @sum, @content, @fee, @payout, 'Active', @eff, NOW(), @aid, @aname)
                    RETURNING policy_id, created_at;";

                int newPolicyId;
                DateTime createdAt;

                using (var cmd = new NpgsqlCommand(insertSql, conn, tx))
                {
                    cmd.Parameters.AddWithValue("cat", category);
                    cmd.Parameters.AddWithValue("code", versionCode);
                    cmd.Parameters.AddWithValue("num", nextVer);
                    cmd.Parameters.AddWithValue("title", request.Title.Trim());
                    cmd.Parameters.AddWithValue("scope", string.IsNullOrWhiteSpace(request.Scope) ? "All Platform Attendees & Event Organizers" : request.Scope.Trim());
                    cmd.Parameters.AddWithValue("sum", request.ChangeSummary.Trim());
                    cmd.Parameters.AddWithValue("content", request.Content.Trim());
                    cmd.Parameters.AddWithValue("fee", feePercent);
                    cmd.Parameters.AddWithValue("payout", payoutDays);
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
                    Scope = string.IsNullOrWhiteSpace(request.Scope) ? "All Platform Attendees & Event Organizers" : request.Scope.Trim(),
                    ChangeSummary = request.ChangeSummary.Trim(),
                    Content = request.Content.Trim(),
                    PlatformFeePercent = feePercent,
                    PayoutDays = payoutDays,
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
                        platformFeePercent = feePercent,
                        payoutDays,
                        effectiveDate,
                        notes = $"Enacted new policy version {versionCode}: {newPolicy.Title} (Fee: {feePercent}%, Payout: {payoutDays} days)"
                    }
                );

                _logger.LogInformation("Policy version {VersionCode} published by Admin {AdminName} in admin_db. Fee: {Fee}%, Payout: {Payout} days", versionCode, adminName, feePercent, payoutDays);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create policy version in admin_db for category {Category}", category);
                return StatusCode(500, new { success = false, message = "Server error while saving policy to database: " + ex.Message });
            }

            return Ok(new
            {
                success = true,
                message = $"Successfully published new policy version {newPolicy.VersionCode}!",
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
