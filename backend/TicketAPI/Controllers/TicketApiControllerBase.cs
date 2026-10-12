using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace TicketAPI.Controllers
{
    // Shared helpers for the controllers added for UC_9, UC_13 and UC_14.
    [ApiController]
    public abstract class TicketApiControllerBase : ControllerBase
    {
        // Id of the signed-in user, read from the JWT.
        protected int CurrentUserId
        {
            get
            {
                var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");

                if (!int.TryParse(raw, out var id))
                    throw new UnauthorizedAccessException("Missing or invalid user id in the token.");

                return id;
            }
        }

        // True when the caller holds the Admin role.
        protected bool IsAdmin => User.IsInRole("Admin");

        // The caller's raw JWT, forwarded to EventAPI to prove event ownership.
        protected string? BearerToken
        {
            get
            {
                var header = Request.Headers.Authorization.ToString();
                const string prefix = "Bearer ";

                if (string.IsNullOrWhiteSpace(header))
                    return null;

                return header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    ? header[prefix.Length..].Trim()
                    : header.Trim();
            }
        }

        // Wraps a successful result in the { success, message, data } shape used by the other APIs.
        protected IActionResult Success(object? data, string message = "Success")
        {
            return Ok(new { success = true, message, data });
        }

        // Maps a service exception to the matching HTTP status and the standard error body.
        protected IActionResult Fail(Exception ex)
        {
            var (status, message) = ex switch
            {
                KeyNotFoundException => (404, ex.Message),
                UnauthorizedAccessException => (403, ex.Message),
                InvalidOperationException => (409, ex.Message),
                ArgumentException => (400, ex.Message),
                _ => (500, "Unexpected server error.")
            };

            return StatusCode(status, new { success = false, message });
        }
    }
}
