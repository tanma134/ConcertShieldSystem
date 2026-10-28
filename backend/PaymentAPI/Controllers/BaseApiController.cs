using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using PaymentAPI.DTOs;

namespace PaymentAPI.Controllers
{
    [ApiController]
    public abstract class BaseApiController : ControllerBase
    {
        protected int CurrentUserId
        {
            get
            {
                var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
                if (string.IsNullOrEmpty(raw) || !int.TryParse(raw, out var id))
                    throw new UnauthorizedAccessException("Không tìm thấy thông tin định danh người dùng trong token.");
                return id;
            }
        }

        protected bool IsAdmin => User.IsInRole("Admin")
            || User.HasClaim(ClaimTypes.Role, "Admin")
            || User.HasClaim("role", "Admin")
            || User.Claims.Any(c => (c.Type == "role" || c.Type == ClaimTypes.Role || c.Type.EndsWith("/role")) && c.Value.Equals("Admin", StringComparison.OrdinalIgnoreCase));

        protected bool IsOrganizer => User.IsInRole("Organizer")
            || User.HasClaim(ClaimTypes.Role, "Organizer")
            || User.HasClaim("role", "Organizer")
            || User.Claims.Any(c => (c.Type == "role" || c.Type == ClaimTypes.Role || c.Type.EndsWith("/role")) && c.Value.Equals("Organizer", StringComparison.OrdinalIgnoreCase));

        protected ActionResult HandleException(Exception ex)
        {
            return ex switch
            {
                KeyNotFoundException => NotFound(ApiResponseDTO<object>.FailResponse(ex.Message)),
                UnauthorizedAccessException => StatusCode(403, ApiResponseDTO<object>.FailResponse(ex.Message)),
                InvalidOperationException => BadRequest(ApiResponseDTO<object>.FailResponse(ex.Message)),
                ArgumentException => BadRequest(ApiResponseDTO<object>.FailResponse(ex.Message)),
                _ => StatusCode(500, ApiResponseDTO<object>.FailResponse("Lỗi máy chủ nội bộ: " + ex.Message))
            };
        }
    }
}
