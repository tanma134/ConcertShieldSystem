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
        private readonly NotificationDbContext _context;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

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

        public async Task<NotificationListResponseDTO> GetUserNotificationsAsync(int userId)
        {
            var notifications = await _repository.GetUserNotificationsAsync(userId);
            if (!notifications.Any())
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

        public async Task<int> MarkAsReadAsync(int notificationId, int userId)
        {
            var notification = await _repository.GetByIdAsync(notificationId);
            if (notification == null)
            {
                throw new KeyNotFoundException("Notification not found.");
            }

            if (notification.UserId != 0 && notification.UserId != userId)
            {
                throw new UnauthorizedAccessException("Unauthorized to access this notification.");
            }

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
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

        public async Task<int> DeleteNotificationAsync(int notificationId, int userId)
        {
            var notification = await _repository.GetByIdAsync(notificationId);
            if (notification == null)
            {
                throw new KeyNotFoundException("Notification not found.");
            }

            if (notification.UserId != 0 && notification.UserId != userId)
            {
                throw new UnauthorizedAccessException("Unauthorized notification access attempt.");
            }

            notification.IsDeleted = true;
            await _repository.UpdateAsync(notification);
            await _repository.SaveChangesAsync();

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
