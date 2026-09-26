using System.Collections.Generic;
using System.Threading.Tasks;
using NotificationAPI.Models;

namespace NotificationAPI.Repositories
{
    public interface INotificationRepository
    {
        Task<List<Notification>> GetUserNotificationsAsync(int userId);
        Task<int> GetUnreadCountAsync(int userId);
        Task<Notification?> GetByIdAsync(int notificationId);
        Task AddAsync(Notification notification);
        Task AddRangeAsync(IEnumerable<Notification> notifications);
        Task UpdateAsync(Notification notification);
        Task SaveChangesAsync();
        Task<List<int>> GetAllUserIdsAsync();
        Task MarkAllAsReadAsync(int userId);
    }
}
