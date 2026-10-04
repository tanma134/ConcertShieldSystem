using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NotificationAPI.DTOs;
using NotificationAPI.Services;

namespace NotificationAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly ILogger<NotificationsController> _logger;

        public NotificationsController(INotificationService notificationService, ILogger<NotificationsController> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        private int? GetCurrentUserId()
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier) 
                     ?? User.FindFirstValue("sub")
                     ?? User.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")
                     ?? User.FindFirstValue("userId");
            return int.TryParse(value, out var id) ? id : null;
        }

        /// <summary>
        /// UC_22 View Notifications: Get user's notification list and unread badge count.
        /// If user is authenticated, returns their notifications + broadcast. If not, returns broadcast (UserId = 0).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetNotifications()
        {
            try
            {
                var userId = GetCurrentUserId() ?? 0;
                var result = await _notificationService.GetUserNotificationsAsync(userId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetNotifications");
                return StatusCode(500, new { message = "An unexpected system error occurred. Please try again later." });
            }
        }

        /// <summary>
        /// UC_22.1 Mark Single Notification as Read.
        /// </summary>
        [HttpPut("{id}/read")]
        [Authorize]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                {
                    return Unauthorized(new { message = "Missing, invalid, or expired Access Token." });
                }

                var unreadCount = await _notificationService.MarkAsReadAsync(id, userId.Value);
                return Ok(new { success = true, unreadCount });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("Unauthorized notification access attempt by user {UserId} on notification {Id}", GetCurrentUserId(), id);
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database update failure in MarkAsRead");
                return StatusCode(500, new { message = "An unexpected system error occurred. Please try again later." });
            }
        }

        /// <summary>
        /// UC_22.1 Mark All Notifications as Read.
        /// </summary>
        [HttpPut("read-all")]
        [Authorize]
        public async Task<IActionResult> MarkAllAsRead()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                {
                    return Unauthorized(new { message = "Missing, invalid, or expired Access Token." });
                }

                var unreadCount = await _notificationService.MarkAllAsReadAsync(userId.Value);
                return Ok(new { success = true, unreadCount });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database update failure in MarkAllAsRead");
                return StatusCode(500, new { message = "An unexpected system error occurred. Please try again later." });
            }
        }

        /// <summary>
        /// UC_22.2 Delete Notification: Dismiss entry from user tray without altering underlying business entities (BR-119).
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteNotification(int id)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                {
                    return Unauthorized(new { message = "Missing, invalid, or expired Access Token." });
                }

                var unreadCount = await _notificationService.DeleteNotificationAsync(id, userId.Value);
                return Ok(new { message = "Delete asset(s) successfully.", unreadCount });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("Unauthorized notification delete attempt by user {UserId} on notification {Id}", GetCurrentUserId(), id);
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in DeleteNotification");
                return StatusCode(500, new { message = "An unexpected system error occurred. Please try again later." });
            }
        }

        /// <summary>
        /// Dispatch notification when a new event is published.
        /// </summary>
        [HttpPost("broadcast-event")]
        public async Task<IActionResult> BroadcastEventNotification([FromBody] BroadcastEventNotificationDTO dto)
        {
            try
            {
                await _notificationService.BroadcastNewEventAsync(dto);
                return Ok(new { success = true, message = "Event notification broadcasted successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in BroadcastEventNotification");
                return StatusCode(500, new { message = "Failed to broadcast event notification." });
            }
        }
        /// <summary>
        /// Create single or broadcast notification.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateNotification([FromBody] CreateNotificationDTO dto)
        {
            try
            {
                await _notificationService.CreateNotificationAsync(dto);
                return Ok(new { success = true, message = "Notification created successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in CreateNotification");
                return StatusCode(500, new { message = "Failed to create notification." });
            }
        }

        /// <summary>
        /// Seed sample event notifications for testing notification redirection.
        /// </summary>
        [HttpPost("seed")]
        public async Task<IActionResult> SeedNotifications()
        {
            try
            {
                await _notificationService.SeedNotificationsFromEventsAsync();
                return Ok(new { success = true, message = "Seed event notifications created successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in SeedNotifications");
                return StatusCode(500, new { message = "Failed to seed event notifications." });
            }
        }
    }
}
