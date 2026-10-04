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
            var title = $"Sự kiện mới: {dto.Title}";
            var message = $"Sự kiện '{dto.Title}' vừa được mở bán/đăng tải. Nhấp vào đây để xem chi tiết ngay!";
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
                                        Title = $"Sự kiện mới: {title}",
                                        Message = $"Sự kiện '{title}' vừa được phát hành và mở bán vé. Nhấp vào đây để xem chi tiết ngay!",
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
                            Title = "Sự kiện mới: Anh Trai Say Hi Concert 2026",
                            Message = "Sự kiện 'Anh Trai Say Hi Concert 2026' vừa được mở bán vé. Nhấp vào đây để xem chi tiết và đặt vé ngay!",
                            Category = "event_new",
                            TargetUrl = "/events/anh-trai-say-hi-concert-2026",
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
                        },
                        new Notification
                        {
                            UserId = 0,
                            Title = "Sự kiện mới: Tri Âm Concert - Mỹ Tâm",
                            Message = "Sự kiện 'Tri Âm Concert - Mỹ Tâm' vừa được cập nhật lịch biểu diễn mới. Đặt vé ngay hôm nay!",
                            Category = "event_new",
                            TargetUrl = "/events/tri-am-concert-my-tam",
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow.AddMinutes(-45)
                        },
                        new Notification
                        {
                            UserId = 0,
                            Title = "Sự kiện mới: Lễ Hội Âm Nhạc Hò Dô 2026",
                            Message = "Lễ Hội Âm Nhạc Hò Dô 2026 chính thức phát hành mã giảm giá sớm. Nhấp để xem thông tin chi tiết!",
                            Category = "event_new",
                            TargetUrl = "/events/ho-do-music-festival-2026",
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow.AddHours(-2)
                        }
                    });
                }
            }

            // 2. Explicitly seed notifications targeting User ID 3 (Nguyễn Văn Khách) if none exist
            var hasUser3 = await _context.Notifications.AnyAsync(n => n.UserId == 3 && !n.IsDeleted);
            if (!hasUser3)
            {
                notificationsToInsert.AddRange(new[]
                {
                    new Notification
                    {
                        UserId = 3,
                        Title = "Chào mừng Nguyễn Văn Khách!",
                        Message = "Tài khoản của bạn đã được kích hoạt thành công. Đã có các sự kiện concert mới mở bán vé, nhấp vào đây để xem chi tiết!",
                        Category = "account",
                        TargetUrl = "/events/anh-trai-say-hi-concert-2026",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow.AddMinutes(-5)
                    },
                    new Notification
                    {
                        UserId = 3,
                        Title = "Sự kiện gợi ý dành riêng cho bạn",
                        Message = "Sự kiện 'Anh Trai Say Hi Concert 2026' đang mở bán vé. Đặt vé ngay hôm nay để nhận được vị trí tốt nhất!",
                        Category = "event_new",
                        TargetUrl = "/events/anh-trai-say-hi-concert-2026",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow.AddMinutes(-25)
                    },
                    new Notification
                    {
                        UserId = 3,
                        Title = "Ưu đãi đặt vé sớm",
                        Message = "Bạn nhận được ưu đãi giảm giá khi đăng ký đặt vé sự kiện 'Tri Âm Concert - Mỹ Tâm'. Nhấp vào đây để xem chi tiết!",
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
