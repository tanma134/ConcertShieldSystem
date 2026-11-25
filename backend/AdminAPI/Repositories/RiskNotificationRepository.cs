using AdminAPI.Data;
using AdminAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace AdminAPI.Repositories;

public interface IRiskNotificationRepository
{
    Task AddAsync(RiskNotification notification, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>Các thông báo chưa gửi, cũ nhất trước (worker gửi dần sang NotificationAPI).</summary>
    Task<List<RiskNotification>> GetPendingAsync(int batchSize, CancellationToken ct = default);
}

public sealed class RiskNotificationRepository(AdminDbContext db) : IRiskNotificationRepository
{
    public Task AddAsync(RiskNotification notification, CancellationToken ct = default) =>
        db.RiskNotifications.AddAsync(notification, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

    public Task<List<RiskNotification>> GetPendingAsync(int batchSize, CancellationToken ct = default) =>
        db.RiskNotifications
            .Where(n => n.DeliveredAt == null)
            .OrderBy(n => n.RiskNotificationId)
            .Take(batchSize)
            .ToListAsync(ct);
}
