using System;
using System.Security.Claims;
using System.Threading.Tasks;
using AuthenticationAPI.DTOs;
using AuthenticationAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthenticationAPI.Controllers
{
    [ApiController]
    [Route("api/organizer-requests")]
    [Authorize]
    public class OrganizerRequestController : ControllerBase
    {
        private readonly IOrganizerRequestService _service;

        public OrganizerRequestController(IOrganizerRequestService service)
        {
            _service = service;
        }

        private int CurrentUserId
        {
            get
            {
                var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) 
                    ?? User.FindFirst("userId")?.Value 
                    ?? User.FindFirst("sub")?.Value;
                return int.TryParse(raw, out var id) ? id : 0;
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateRequest([FromForm] CreateOrganizerRequestDTO dto)
        {
            try
            {
                if (CurrentUserId <= 0)
                {
                    return Unauthorized(new { message = "Invalid user session. Please log in again." });
                }

                var result = await _service.CreateRequestAsync(CurrentUserId, dto);
                return CreatedAtAction(nameof(GetById), new { requestId = result.RequestId }, result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMyRequests()
        {
            if (CurrentUserId <= 0)
            {
                return Unauthorized(new { message = "Invalid user session. Please log in again." });
            }
            var result = await _service.GetMyRequestsAsync(CurrentUserId);
            return Ok(result);
        }

        [HttpGet("notifications")]
        public async Task<IActionResult> GetNotifications()
        {
            if (CurrentUserId <= 0)
            {
                return Unauthorized(new { message = "Invalid user session. Please log in again." });
            }

            var items = new List<object>();
            try
            {
                using var conn = new Npgsql.NpgsqlConnection("Host=localhost;Port=5432;Database=notification_db;Username=postgres;Password=123456");
                await conn.OpenAsync();
                using var cmd = new Npgsql.NpgsqlCommand(
                    "SELECT notification_id, user_id, title, message, category, target_url, is_read, created_at FROM notifications WHERE (user_id = @uid OR user_id = 0) AND is_deleted = false ORDER BY created_at DESC LIMIT 50;", conn);
                cmd.Parameters.AddWithValue("uid", CurrentUserId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    items.Add(new
                    {
                        notificationId = reader.GetInt32(0),
                        userId = reader.GetInt32(1),
                        title = reader.GetString(2),
                        message = reader.GetString(3),
                        category = reader.IsDBNull(4) ? "event_new" : reader.GetString(4),
                        targetUrl = reader.IsDBNull(5) ? null : reader.GetString(5),
                        isRead = reader.GetBoolean(6),
                        createdAt = reader.GetDateTime(7)
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching notifications from DB: {ex.Message}");
            }

            return Ok(new
            {
                items,
                unreadCount = items.Count(x => !((dynamic)x).isRead)
            });
        }

        [HttpGet("{requestId:int}")]
        public async Task<IActionResult> GetById(int requestId)
        {
            try
            {
                var result = await _service.GetByIdAsync(requestId);
                var isAdmin = User.IsInRole("Admin");
                if (!isAdmin && result.UserId != CurrentUserId)
                    return Forbid();
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAll([FromQuery] string? status)
        {
            try
            {
                var result = await _service.GetAllRequestsAsync(status);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPut("{requestId:int}/review")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Review(int requestId, [FromBody] ReviewOrganizerRequestDTO dto)
        {
            try
            {
                var result = await _service.ReviewRequestAsync(requestId, CurrentUserId, dto);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}
