using System.Security.Claims;
using AdminAPI.DTOs;
using AdminAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminAPI.Controllers
{
    // ==========================================
    // NHÓM 2: GOVERNANCE, POLICY VERSIONING & COMPLIANCE
    // View Policy Version History (Actor: Admin)
    // Create New Policy Version (Actor: Admin)
    // ==========================================

    [ApiController]
    [Route("api/admin/policies")]
    [Authorize(Roles = "Admin")]
    public class SystemPoliciesController : ControllerBase
    {
        private readonly ISystemPolicyService _service;
        private readonly IAuditLogService _auditLog;
        private readonly ILogger<SystemPoliciesController> _logger;

        public SystemPoliciesController(
            ISystemPolicyService service,
            IAuditLogService auditLog,
            ILogger<SystemPoliciesController> logger)
        {
            _service = service;
            _auditLog = auditLog;
            _logger = logger;
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
        /// View Policy Version History (Category: PRICE_TICKETING, REFUND_CANCELLATION, VIRTUAL_QUEUE, RISK_ANTI_FRAUD, TERMS_OF_SERVICE)
        /// </summary>
        [HttpGet("history")]
        public async Task<IActionResult> GetPolicyHistory([FromQuery] string? category = "PRICE_TICKETING")
        {
            try
            {
                var items = await _service.GetHistoryAsync(category);
                return Ok(new
                {
                    success = true,
                    category = SystemPolicyService.NormalizeCategory(category),
                    totalVersions = items.Count,
                    data = items
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load policy history for category {Category} from admin_db", category);
                return StatusCode(500, new { success = false, message = "Không thể tải lịch sử chính sách từ cơ sở dữ liệu." });
            }
        }

        // Lay danh sach cac ban ghi chinh sach dang co hieu luc theo tung danh muc danh cho moi nguoi dung
        [AllowAnonymous]
        [HttpGet("active")]
        public async Task<IActionResult> GetActivePolicies()
        {
            var activePolicies = new Dictionary<string, PolicyVersionDTO>();

            try
            {
                activePolicies = await _service.GetActiveAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load active policies from admin_db");
            }

            var pricingPolicy = activePolicies.GetValueOrDefault("PRICE_TICKETING")
                ?? activePolicies.Values.FirstOrDefault();

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
                var policies = await _service.GetActiveAsync();
                if (policies.TryGetValue("PRICE_TICKETING", out var policy))
                    return Ok(new { success = true, data = policy });

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

        // Ban hanh phien ban chinh sach he thong moi va luu tru phien ban cu
        [HttpPost("create")]
        public async Task<IActionResult> CreatePolicyVersion([FromBody] CreatePolicyVersionRequestDTO request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return BadRequest(new { success = false, message = "Tiêu đề văn bản chính sách không được để trống." });

            if (string.IsNullOrWhiteSpace(request.ChangeSummary))
                return BadRequest(new { success = false, message = "Lý do ban hành và tóm tắt nội dung thay đổi là bắt buộc." });

            if (string.IsNullOrWhiteSpace(request.Content))
                return BadRequest(new { success = false, message = "Toàn văn nội dung chính sách không được để trống." });

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

            var adminId = GetCurrentAdminId();
            var adminName = GetCurrentAdminName();

            PolicyVersionDTO newPolicy;

            try
            {
                newPolicy = await _service.CreateAsync(request, adminId, adminName);

                // Audit Log to AuditLogs table in database
                await _auditLog.LogAsync(
                    userId: adminId,
                    actorType: "ADMIN",
                    action: "POLICY_PUBLISHED",
                    entityType: "SystemPolicy",
                    entityId: newPolicy.PolicyId.ToString(),
                    oldValue: null,
                    newValue: new
                    {
                        newPolicy.Category,
                        newPolicy.VersionCode,
                        newPolicy.VersionNumber,
                        newPolicy.Title,
                        newPolicy.Scope,
                        newPolicy.EffectiveDate,
                        newPolicy.PlatformFeePercent,
                        newPolicy.PayoutDays,
                        notes = $"Ban hành chính sách mới {newPolicy.VersionCode}: {newPolicy.Title} (Lý do: {newPolicy.ChangeSummary})"
                    }
                );

                _logger.LogInformation("Policy version {VersionCode} published by Admin {AdminName} in admin_db", newPolicy.VersionCode, adminName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create policy version in admin_db for category {Category}", request.Category);
                return StatusCode(500, new { success = false, message = "Lỗi máy chủ khi lưu chính sách vào cơ sở dữ liệu: " + ex.Message });
            }

            return Ok(new
            {
                success = true,
                message = $"Ban hành thành công phiên bản chính sách mới {newPolicy.VersionCode}!",
                data = newPolicy
            });
        }
    }
}