using System.Security.Claims;
using EventAPI.DTOs;
using EventAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace EventAPI.Controllers
{
    [ApiController]
    public abstract class BaseApiController : ControllerBase
    {
        /// <summary>UserId from the JWT (ClaimTypes.NameIdentifier), set by AuthenticationAPI.</summary>
        protected int CurrentUserId
        {
            get
            {
                var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(raw) || !int.TryParse(raw, out var id))
                    throw new UnauthorizedAccessException("Missing or invalid user id claim in token.");
                return id;
            }
        }

        protected bool IsAdmin => User.IsInRole("Admin");

        protected bool IsOrganizer => User.IsInRole("Organizer");

        /// <summary>
        /// The raw JWT from the Authorization header, forwarded to AuthenticationAPI
        /// when EventAPI needs to act on the caller's behalf (e.g. granting the
        /// Organizer role during approval). Null when there is no bearer token.
        /// </summary>
        protected string? BearerToken
        {
            get
            {
                var header = Request.Headers.Authorization.ToString();

                if (string.IsNullOrWhiteSpace(header))
                    return null;

                const string prefix = "Bearer ";
                return header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                    ? header[prefix.Length..].Trim()
                    : header.Trim();
            }
        }

        /// <summary>
        /// Maps a service-layer exception to the correct HTTP status code + ApiResponseDTO body.
        /// Keeps controllers free of repeated try/catch boilerplate.
        /// </summary>
        protected ActionResult HandleException(Exception ex)
        {
            return ex switch
            {
                // Incomplete publication data — return every problem at once.
                SubmissionValidationException sve => BadRequest(new ApiResponseDTO<SubmitValidationResultDTO>
                {
                    Success = false,
                    Message = "The concert is missing required publication data.",
                    Data = sve.Result,
                    Errors = sve.Result.Errors
                }),
                KeyNotFoundException => NotFound(ApiResponseDTO<object>.FailResponse(ex.Message)),
                UnauthorizedAccessException => StatusCode(403, ApiResponseDTO<object>.FailResponse(ex.Message)),
                InvalidOperationException => BadRequest(ApiResponseDTO<object>.FailResponse(ex.Message)),
                ArgumentException => BadRequest(ApiResponseDTO<object>.FailResponse(ex.Message)),
                _ => StatusCode(500, ApiResponseDTO<object>.FailResponse("Unexpected server error: " + ex.Message))
            };
        }
    }
}
