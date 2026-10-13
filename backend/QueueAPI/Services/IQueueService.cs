using QueueAPI.DTOs;

namespace QueueAPI.Services;

public interface IQueueService
{
    Task<ConfigureQueueResponse> ConfigureAsync(int eventId, ConfigureQueueRequest request, int userId);
    Task<QueueStatusResponse> RegisterAsync(int eventId, int userId);
    Task<QueueStatusResponse> JoinAsync(int eventId, int userId, string? token);
    Task<QueueStatusResponse> GetStatusAsync(int eventId, int userId, string? token);
    Task LeaveAsync(int eventId, int userId, string? token);
}
