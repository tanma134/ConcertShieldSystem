using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using NotificationAPI.DTOs;
using NotificationAPI.Hubs;
using NotificationAPI.Models;
using NotificationAPI.Repositories;

using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NotificationAPI.Data;

namespace NotificationAPI.Services
{
    public class NotificationService : INotificationService
    {
        private readonly INotificationRepository _repository;
        private readonly NotificationDbContext? _context;
        private readonly IHubContext<NotificationHub>? _hubContext;
        private readonly IHttpClientFactory? _httpClientFactory;
        private readonly IConfiguration? _configuration;

        [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
        public NotificationService(
            INotificationRepository repository,
            NotificationDbContext context,
            IHubContext<NotificationHub> hubContext,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _repository = repository;
            _context = context;
            _hubContext = hubContext;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public NotificationService(INotificationRepository repository)
        {
            _repository = repository;
        }

        public async Task<NotificationListResponseDTO> GetUserNotificationsAsync(int userId, bool strictValidation = false, bool userExists = true)
        {
            if (strictValidation)
            {
                if (userId <= 0)
                    throw new UnauthorizedAccessException("User authentication required");

                if (!userExists)
                    throw new KeyNotFoundException("User account not found");
            }

            var notifications = await _repository.GetUserNotificationsAsync(userId);
            if (!notifications.Any() && _context != null && !strictValidation)
            {
                await SeedNotificationsFromEventsAsync();
                notifications = await _repository.GetUserNotificationsAsync(userId);
            }

            var unreadCount = await _repository.GetUnreadCountAsync(userId);

            var items = notifications.Select(n => new NotificationResponseDTO
            {
                NotificationId = n.NotificationId,
                UserId = n.UserId,
                Title = n.Title,
                Message = n.Message,
                Category = n.Category,
                TargetUrl = n.TargetUrl,
                IsRead = n.IsRead,
                ReadAt = n.ReadAt,
                CreatedAt = n.CreatedAt
            }).ToList();

            return new NotificationListResponseDTO
            {
                Items = items,
                UnreadCount = unreadCount
            };
        }

        public async Task<int> MarkAsReadAsync(int notificationId, int userId, bool strictValidation = false)
        {
            if (strictValidation && userId <= 0)
                throw new UnauthorizedAccessException("Authentication required");

            if (notificationId <= 0)
                throw new ArgumentException("Notification ID must be positive");

            var notification = await _repository.GetByIdAsync(notificationId);
            if (notification == null)
            {
                throw new KeyNotFoundException(strictValidation ? "Notification not found" : "Notification not found.");
            }

            if (userId > 0 && notification.UserId != 0 && notification.UserId != userId)
            {
                throw new UnauthorizedAccessException(strictValidation ? "You do not have access to this notification" : "Unauthorized to access this notification.");
            }

            var now = DateTime.UtcNow;

            if (_context != null)
            {
                // Mark the target notification and any duplicate rows (same title & message for this user or broadcast) as read
                var matchingUnread = await _context.Notifications
                    .Where(n => !n.IsDeleted && !n.IsRead &&
                                (n.NotificationId == notificationId ||
                                 ((n.UserId == userId || n.UserId == 0 || n.UserId == notification.UserId) &&
                                  n.Title == notification.Title &&
                                  n.Message == notification.Message)))
                    .ToListAsync();

                if (matchingUnread.Count > 0)
                {
                    foreach (var item in matchingUnread)
                    {
                        item.IsRead = true;
                        item.ReadAt = now;
                    }
                    await _context.SaveChangesAsync();
                }
                else if (!notification.IsRead)
                {
                    notification.IsRead = true;
                    notification.ReadAt = now;
                    await _repository.UpdateAsync(notification);
                    await _repository.SaveChangesAsync();
                }
            }
            else if (!notification.IsRead)
            {
                notification.IsRead = true;
                notification.ReadAt = now;
                await _repository.UpdateAsync(notification);
                await _repository.SaveChangesAsync();
            }

            return await _repository.GetUnreadCountAsync(userId);
        }

        public async Task<int> MarkAllAsReadAsync(int userId)
        {
            await _repository.MarkAllAsReadAsync(userId);
            return 0;
        }

        public async Task<int> DeleteNotificationAsync(int notificationId, int userId, bool strictValidation = false)
        {
            if (strictValidation && userId <= 0)
                throw new UnauthorizedAccessException("Authentication required");

            if (notificationId <= 0)
                throw new ArgumentException("Notification ID must be positive");

            var notification = await _repository.GetByIdAsync(notificationId);
            if (notification == null)
            {
                throw new KeyNotFoundException(strictValidation ? "Notification not found" : "Notification not found.");
            }

            if (userId > 0 && notification.UserId != 0 && notification.UserId != userId)
            {
                throw new UnauthorizedAccessException(strictValidation ? "You do not have access to this notification" : "Unauthorized notification access attempt.");
            }

            if (_context != null)
            {
                var matchingToDelete = await _context.Notifications
                    .Where(n => !n.IsDeleted &&
                                (n.NotificationId == notificationId ||
                                 ((n.UserId == userId || n.UserId == 0 || n.UserId == notification.UserId) &&
                                  n.Title == notification.Title &&
                                  n.Message == notification.Message)))
                    .ToListAsync();

                foreach (var item in matchingToDelete)
                {
                    item.IsDeleted = true;
                }
                await _context.SaveChangesAsync();
            }
            else
            {
                notification.IsDeleted = true;
                await _repository.UpdateAsync(notification);
                await _repository.SaveChangesAsync();
            }

            return await _repository.GetUnreadCountAsync(userId);
        }

        public async Task BroadcastNewEventAsync(BroadcastEventNotificationDTO dto)
        {
            var title = $"New Event: {dto.Title}";
            var message = $"Event '{dto.Title}' is now published and tickets are available. Click here to view details and book your seats!";
            var targetUrl = $"/events/{dto.Slug}";

            // Create broadcast notification record (UserId = 0 represents system broadcast to all customers)
            var notification = new Notification
            {
                UserId = 0,
                Title = title,
                Message = message,
                Category = "event_new",
                TargetUrl = targetUrl,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            await _repository.AddAsync(notification);
            await _repository.SaveChangesAsync();

            var responseDto = new NotificationResponseDTO
            {
                NotificationId = notification.NotificationId,
                UserId = 0,
                Title = notification.Title,
                Message = notification.Message,
                Category = notification.Category,
                TargetUrl = notification.TargetUrl,
                IsRead = false,
                CreatedAt = notification.CreatedAt
            };

            // Push real-time notification to all connected customer sessions via SignalR
            await _hubContext.Clients.All.SendAsync("ReceiveNotification", responseDto);
        }

        public async Task CreateNotificationAsync(CreateNotificationDTO dto)
        {
            var notification = new Notification
            {
                UserId = dto.UserId,
                Title = dto.Title,
                Message = dto.Message,
                Category = dto.Category,
                TargetUrl = dto.TargetUrl,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            await _repository.AddAsync(notification);
            await _repository.SaveChangesAsync();

            var responseDto = new NotificationResponseDTO
            {
                NotificationId = notification.NotificationId,
                UserId = notification.UserId,
                Title = notification.Title,
                Message = notification.Message,
                Category = notification.Category,
                TargetUrl = notification.TargetUrl,
                IsRead = false,
                CreatedAt = notification.CreatedAt
            };

            if (dto.UserId == 0)
            {
                await _hubContext.Clients.All.SendAsync("ReceiveNotification", responseDto);
            }
            else
            {
                await _hubContext.Clients.Group($"User_{dto.UserId}").SendAsync("ReceiveNotification", responseDto);
            }
        }

        public async Task SeedNotificationsFromEventsAsync()
        {
            var notificationsToInsert = new List<Notification>();

            // 1. Seed broadcast notifications (UserId = 0) if none exist
            var hasBroadcast = await _context.Notifications.AnyAsync(n => n.UserId == 0 && !n.IsDeleted);
            if (!hasBroadcast)
            {
                try
                {
                    var client = _httpClientFactory.CreateClient();
                    var eventApiUrl = _configuration["Services:EventApi"] ?? "http://localhost:7289";
                    var response = await client.GetAsync($"{eventApiUrl.TrimEnd('/')}/api/events?page=1&pageSize=10");
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        using var doc = System.Text.Json.JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("data", out var dataEl) &&
                            dataEl.TryGetProperty("items", out var itemsEl) &&
                            itemsEl.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var item in itemsEl.EnumerateArray())
                            {
                                var title = item.TryGetProperty("title", out var t) ? t.GetString() : null;
                                var slug = item.TryGetProperty("slug", out var s) ? s.GetString() : null;
                                if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(slug))
                                {
                                    notificationsToInsert.Add(new Notification
                                    {
                                        UserId = 0,
                                        Title = $"New Event: {title}",
                                        Message = $"Event '{title}' has been published and tickets are now on sale. Click here to view event details!",
                                        Category = "event_new",
                                        TargetUrl = $"/events/{slug}",
                                        IsRead = false,
                                        CreatedAt = DateTime.UtcNow.AddMinutes(-new Random().Next(5, 120))
                                    });
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Fallback if EventAPI is starting up concurrently
                }

                if (!notificationsToInsert.Any(n => n.UserId == 0))
                {
                    notificationsToInsert.AddRange(new[]
                    {
                        new Notification
                        {
                            UserId = 0,
                            Title = "New Event: Say Hi Live Concert 2026",
                            Message = "Tickets for 'Say Hi Live Concert 2026' are now open. Explore seating tiers and book your tickets today!",
                            Category = "event_new",
                            TargetUrl = "/events/anh-trai-say-hi-concert-2026",
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
                        },
                        new Notification
                        {
                            UserId = 0,
                            Title = "New Event: Tri Am Arena Concert",
                            Message = "Tri Am Arena Concert schedule has been finalized. Secure your tickets before sold out!",
                            Category = "event_new",
                            TargetUrl = "/events/tri-am-concert-my-tam",
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow.AddMinutes(-45)
                        },
                        new Notification
                        {
                            UserId = 0,
                            Title = "New Event: Ho Do International Music Festival 2026",
                            Message = "Early bird promo codes for Ho Do International Music Festival 2026 are now available. Check them out!",
                            Category = "event_new",
                            TargetUrl = "/events/ho-do-music-festival-2026",
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow.AddHours(-2)
                        }
                    });
                }
            }

            // 2. Explicitly seed notifications targeting User ID 3 if none exist
            var hasUser3 = await _context.Notifications.AnyAsync(n => n.UserId == 3 && !n.IsDeleted);
            if (!hasUser3)
            {
                notificationsToInsert.AddRange(new[]
                {
                    new Notification
                    {
                        UserId = 3,
                        Title = "Welcome to ConcertShield!",
                        Message = "Your customer account is active. Explore newly published concert events and reserve your tickets with dynamic QR security!",
                        Category = "account",
                        TargetUrl = "/events/anh-trai-say-hi-concert-2026",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow.AddMinutes(-5)
                    },
                    new Notification
                    {
                        UserId = 3,
                        Title = "Recommended Concert for You",
                        Message = "Concert 'Say Hi Live Concert 2026' is now selling tickets. Check out available zones for the best experience!",
                        Category = "event_new",
                        TargetUrl = "/events/anh-trai-say-hi-concert-2026",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow.AddMinutes(-25)
                    },
                    new Notification
                    {
                        UserId = 3,
                        Title = "Early-Bird Promotional Voucher",
                        Message = "A promotional discount is applicable for 'Tri Am Arena Concert'. Visit the event details page to claim your discount!",
                        Category = "event_new",
                        TargetUrl = "/events/tri-am-concert-my-tam",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow.AddHours(-1)
                    }
                });
            }

            if (notificationsToInsert.Any())
            {
                await _repository.AddRangeAsync(notificationsToInsert);
                await _repository.SaveChangesAsync();
            }
        }
    }
}
