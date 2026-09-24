using System.Security.Claims;
using AuthenticationAPI.Repositories;
using AuthenticationAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthenticationAPI.Controllers
{
    /// <summary>Chỉ Admin được xem ảnh KYC gốc. Mỗi lần xem đều ghi log (bảng kyc_access_logs).</summary>
    [ApiController]
    [Route("api/admin/kyc")]
    [Authorize(Policy = "CanManageUsers")]
    public class KycAdminController : ControllerBase
    {
        private readonly IEkycRepository _repository;
        private readonly IObjectStorage _storage;
        private readonly IKycAccessLogService _accessLog;
        private readonly ILogger<KycAdminController> _logger;

        public KycAdminController(IEkycRepository repository, IObjectStorage storage,
                                  IKycAccessLogService accessLog, ILogger<KycAdminController> logger)
        {
            _repository = repository;
            _storage = storage;
            _accessLog = accessLog;
            _logger = logger;
        }

        // GET api/admin/kyc/7/images/front|back|selfie
        [HttpGet("{ekycId:int}/images/{kind}")]
        public async Task<IActionResult> GetImage(int ekycId, string kind)
        {
            var record = await _repository.GetByIdAsync(ekycId);
            if (record is null)
                return NotFound();

            var normalizedKind = kind.ToLowerInvariant();
            var key = normalizedKind switch
            {
                "front" => record.CccdFrontObjectKey,
                "back" => record.CccdBackObjectKey,
                "selfie" => record.FaceCaptureObjectKey,
                _ => null
            };
            if (key is null)
                return NotFound();

            var stream = await _storage.DownloadAsync(key);
            if (stream is null)
                return NotFound();

            var adminIdText = (User.FindFirst("userId") ?? User.FindFirst(ClaimTypes.NameIdentifier))?.Value;
            int? adminId = int.TryParse(adminIdText, out var id) ? id : null;
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

            // Ghi log TRƯỚC khi trả ảnh: không ghi được log thì không cho xem
            await _accessLog.LogAsync(record.UserId, record.EkycId, adminId, KycActorTypes.Admin,
                KycAccessActions.ViewImage, ip, $"kind={normalizedKind}");
            _logger.LogWarning("KYC image access: admin {AdminId} xem {Kind} của ekyc {EkycId} (user {UserId})",
                adminIdText, normalizedKind, ekycId, record.UserId);

            Response.Headers["Cache-Control"] = "no-store";
            return File(stream, "image/jpeg");
        }
    }
}