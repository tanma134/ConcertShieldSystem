using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NotificationAPI.Data;
using NotificationAPI.Models;

namespace NotificationAPI.Repositories
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly NotificationDbContext _context;

        public NotificationRepository(NotificationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Notification>> GetUserNotificationsAsync(int userId)
        {
            return await _context.Notifications
                .Where(n => (n.UserId == userId || n.UserId == 0) && !n.IsDeleted)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            return await _context.Notifications
                .Where(n => (n.UserId == userId || n.UserId == 0) && !n.IsRead && !n.IsDeleted)
                .CountAsync();
        }

        public async Task<Notification?> GetByIdAsync(int notificationId)
        {
            return await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == notificationId && !n.IsDeleted);
        }

        public async Task AddAsync(Notification notification)
        {
            await _context.Notifications.AddAsync(notification);
        }

        public async Task AddRangeAsync(IEnumerable<Notification> notifications)
        {
            await _context.Notifications.AddRangeAsync(notifications);
        }

        public async Task UpdateAsync(Notification notification)
        {
            _context.Notifications.Update(notification);
            await Task.CompletedTask;
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        public async Task<List<int>> GetAllUserIdsAsync()
        {
            // Return empty or fetch distinct user IDs from notifications table or identity API
            return await _context.Notifications
                .Select(n => n.UserId)
                .Where(u => u > 0)
                .Distinct()
                .ToListAsync();
        }

        public async Task MarkAllAsReadAsync(int userId)
        {
            var now = DateTime.UtcNow;
            var unread = await _context.Notifications
                .Where(n => (n.UserId == userId || n.UserId == 0) && !n.IsRead && !n.IsDeleted)
                .ToListAsync();

            foreach (var item in unread)
            {
                item.IsRead = true;
                item.ReadAt = now;
            }

            await _context.SaveChangesAsync();
        }
    }
}
