using System.Collections.Generic;
using System.Threading.Tasks;
using NotificationAPI.DTOs;

namespace NotificationAPI.Services
{
    public interface INotificationService
    {
        Task<NotificationListResponseDTO> GetUserNotificationsAsync(int userId, bool strictValidation = false, bool userExists = true);
        Task<int> MarkAsReadAsync(int notificationId, int userId, bool strictValidation = false);
        Task<int> MarkAllAsReadAsync(int userId);
        Task<int> DeleteNotificationAsync(int notificationId, int userId, bool strictValidation = false);
        Task BroadcastNewEventAsync(BroadcastEventNotificationDTO dto);
        Task CreateNotificationAsync(CreateNotificationDTO dto);
        Task SeedNotificationsFromEventsAsync();
    }
}
