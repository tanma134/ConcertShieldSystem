using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using NotificationAPI.Models;
using NotificationAPI.Repositories;
using NotificationAPI.Services;
using Xunit;

namespace TicketAPI.UnitTests;

/// <summary>
/// Unit Tests for Notification Operations:
/// - Function_142_ViewNotifications (GetUserNotificationsAsync: UTCID01..UTCID08)
/// - Function_143_MarkNotifRead (MarkAsReadAsync: UTCID01..UTCID08)
/// - Function_144_DeleteNotification (DeleteNotificationAsync: UTCID01..UTCID08)
/// </summary>
public class NotificationServiceUnitTests
{
    private static List<Notification> CreateSampleNotifications(int userId, int totalCount, int unreadCount)
    {
        var list = new List<Notification>();
        for (int i = 1; i <= totalCount; i++)
        {
            list.Add(new Notification
            {
                NotificationId = 1000 + i,
                UserId = userId,
                Title = $"Notification #{i}",
                Message = $"Notification message #{i}",
                Category = "event_new",
                TargetUrl = "/events/sample",
                IsRead = i > unreadCount,
                ReadAt = i > unreadCount ? DateTime.UtcNow.AddMinutes(-5) : null,
                CreatedAt = DateTime.UtcNow.AddMinutes(-10 * i),
                IsDeleted = false
            });
        }
        return list;
    }

    // =========================================================================
    // 1. Function_142_ViewNotifications (GetUserNotificationsAsync: UTCID01..UTCID08)
    // =========================================================================

    [Fact]
    public async Task ViewNotifications_UTCID01_Customer101With5UnreadAnd10Total_ReturnsUnread5AndCount10()
    {
        var repo = new Mock<INotificationRepository>();
        repo.Setup(r => r.GetUserNotificationsAsync(101))
            .ReturnsAsync(CreateSampleNotifications(101, 10, 5));
        repo.Setup(r => r.GetUnreadCountAsync(101)).ReturnsAsync(5);
        var service = new NotificationService(repo.Object);

        var res = await service.GetUserNotificationsAsync(101, strictValidation: true);

        Assert.Equal(5, res.UnreadCount);
        Assert.Equal(10, res.Items.Count);
    }

    [Fact]
    public async Task ViewNotifications_UTCID02_Organizer201WithNotifications_ReturnsUnreadAndItems()
    {
        var repo = new Mock<INotificationRepository>();
        repo.Setup(r => r.GetUserNotificationsAsync(201))
            .ReturnsAsync(CreateSampleNotifications(201, 4, 2));
        repo.Setup(r => r.GetUnreadCountAsync(201)).ReturnsAsync(2);
        var service = new NotificationService(repo.Object);

        var res = await service.GetUserNotificationsAsync(201, strictValidation: true);

        Assert.True(res.UnreadCount > 0);
        Assert.True(res.Items.Count > 0);
    }

    [Fact]
    public async Task ViewNotifications_UTCID03_BoundaryNewUser301WithZeroNotifications_ReturnsZeroUnreadAndEmptyList()
    {
        var repo = new Mock<INotificationRepository>();
        repo.Setup(r => r.GetUserNotificationsAsync(301))
            .ReturnsAsync(new List<Notification>());
        repo.Setup(r => r.GetUnreadCountAsync(301)).ReturnsAsync(0);
        var service = new NotificationService(repo.Object);

        var res = await service.GetUserNotificationsAsync(301, strictValidation: true);

        Assert.Equal(0, res.UnreadCount);
        Assert.Empty(res.Items);
    }

    [Fact]
    public async Task ViewNotifications_UTCID04_BoundaryMinUserId1_ReturnsNotifications()
    {
        var repo = new Mock<INotificationRepository>();
        repo.Setup(r => r.GetUserNotificationsAsync(1))
            .ReturnsAsync(CreateSampleNotifications(1, 3, 1));
        repo.Setup(r => r.GetUnreadCountAsync(1)).ReturnsAsync(1);
        var service = new NotificationService(repo.Object);

        var res = await service.GetUserNotificationsAsync(1, strictValidation: true);

        Assert.True(res.UnreadCount > 0);
        Assert.Equal(3, res.Items.Count);
    }

    [Fact]
    public async Task ViewNotifications_UTCID05_BoundaryHighUserId999999_ReturnsNotifications()
    {
        var repo = new Mock<INotificationRepository>();
        repo.Setup(r => r.GetUserNotificationsAsync(999999))
            .ReturnsAsync(CreateSampleNotifications(999999, 2, 1));
        repo.Setup(r => r.GetUnreadCountAsync(999999)).ReturnsAsync(1);
        var service = new NotificationService(repo.Object);

        var res = await service.GetUserNotificationsAsync(999999, strictValidation: true);

        Assert.True(res.UnreadCount > 0);
        Assert.Equal(2, res.Items.Count);
    }

    [Fact]
    public async Task ViewNotifications_UTCID06_DeletedOrNonExistentUserAccount88888_ThrowsKeyNotFoundException()
    {
        var repo = new Mock<INotificationRepository>();
        var service = new NotificationService(repo.Object);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.GetUserNotificationsAsync(88888, strictValidation: true, userExists: false));

        Assert.Equal("User account not found", ex.Message);
    }

    [Fact]
    public async Task ViewNotifications_UTCID07_BoundaryInvalidZeroUserId_ThrowsUnauthorizedAccessException()
    {
        var repo = new Mock<INotificationRepository>();
        var service = new NotificationService(repo.Object);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetUserNotificationsAsync(0, strictValidation: true));

        Assert.Equal("User authentication required", ex.Message);
    }

    [Fact]
    public async Task ViewNotifications_UTCID08_AbnormalNegativeUserId_ThrowsUnauthorizedAccessException()
    {
        var repo = new Mock<INotificationRepository>();
        var service = new NotificationService(repo.Object);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetUserNotificationsAsync(-1, strictValidation: true));

        Assert.Equal("User authentication required", ex.Message);
    }

    // =========================================================================
    // 2. Function_143_MarkNotifRead (MarkAsReadAsync: UTCID01..UTCID08)
    // =========================================================================

    [Fact]
    public async Task MarkNotificationAsRead_UTCID01_UnreadNotification1001OwnedBy101_MarksIsReadTrue()
    {
        var notif = new Notification { NotificationId = 1001, UserId = 101, IsRead = false };
        var repo = new Mock<INotificationRepository>();
        repo.Setup(r => r.GetByIdAsync(1001)).ReturnsAsync(notif);
        repo.Setup(r => r.GetUnreadCountAsync(101)).ReturnsAsync(0);
        var service = new NotificationService(repo.Object);

        await service.MarkAsReadAsync(1001, 101, strictValidation: true);

        Assert.True(notif.IsRead);
        Assert.NotNull(notif.ReadAt);
        repo.Verify(r => r.UpdateAsync(notif), Times.Once);
        repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task MarkNotificationAsRead_UTCID02_AlreadyReadNotification1002_SucceedsIdempotently()
    {
        var notif = new Notification { NotificationId = 1002, UserId = 101, IsRead = true, ReadAt = DateTime.UtcNow.AddMinutes(-5) };
        var repo = new Mock<INotificationRepository>();
        repo.Setup(r => r.GetByIdAsync(1002)).ReturnsAsync(notif);
        repo.Setup(r => r.GetUnreadCountAsync(101)).ReturnsAsync(0);
        var service = new NotificationService(repo.Object);

        await service.MarkAsReadAsync(1002, 101, strictValidation: true);

        Assert.True(notif.IsRead);
    }

    [Fact]
    public async Task MarkNotificationAsRead_UTCID03_BoundaryMinNotificationId1_MarksIsReadTrue()
    {
        var notif = new Notification { NotificationId = 1, UserId = 101, IsRead = false };
        var repo = new Mock<INotificationRepository>();
        repo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(notif);
        var service = new NotificationService(repo.Object);

        await service.MarkAsReadAsync(1, 101, strictValidation: true);

        Assert.True(notif.IsRead);
    }

    [Fact]
    public async Task MarkNotificationAsRead_UTCID04_BoundaryHighNotificationId999999_MarksIsReadTrue()
    {
        var notif = new Notification { NotificationId = 999999, UserId = 101, IsRead = false };
        var repo = new Mock<INotificationRepository>();
        repo.Setup(r => r.GetByIdAsync(999999)).ReturnsAsync(notif);
        var service = new NotificationService(repo.Object);

        await service.MarkAsReadAsync(999999, 101, strictValidation: true);

        Assert.True(notif.IsRead);
    }

    [Fact]
    public async Task MarkNotificationAsRead_UTCID05_NonExistentNotificationId88888_ThrowsKeyNotFoundException()
    {
        var repo = new Mock<INotificationRepository>();
        repo.Setup(r => r.GetByIdAsync(88888)).ReturnsAsync((Notification?)null);
        var service = new NotificationService(repo.Object);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.MarkAsReadAsync(88888, 101, strictValidation: true));

        Assert.Equal("Notification not found", ex.Message);
    }

    [Fact]
    public async Task MarkNotificationAsRead_UTCID06_NotificationOwnedByAnotherUser99_ThrowsUnauthorizedAccessException()
    {
        var notif = new Notification { NotificationId = 2001, UserId = 99, IsRead = false };
        var repo = new Mock<INotificationRepository>();
        repo.Setup(r => r.GetByIdAsync(2001)).ReturnsAsync(notif);
        var service = new NotificationService(repo.Object);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.MarkAsReadAsync(2001, 101, strictValidation: true));

        Assert.Equal("You do not have access to this notification", ex.Message);
    }

    [Fact]
    public async Task MarkNotificationAsRead_UTCID07_InvalidZeroOrNegativeNotificationId_ThrowsArgumentException()
    {
        var repo = new Mock<INotificationRepository>();
        var service = new NotificationService(repo.Object);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.MarkAsReadAsync(0, 101, strictValidation: true));

        Assert.Equal("Notification ID must be positive", ex.Message);
    }

    [Fact]
    public async Task MarkNotificationAsRead_UTCID08_UnauthenticatedCaller_ThrowsUnauthorizedAccessException()
    {
        var repo = new Mock<INotificationRepository>();
        var service = new NotificationService(repo.Object);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.MarkAsReadAsync(1001, 0, strictValidation: true));

        Assert.Equal("Authentication required", ex.Message);
    }

    // =========================================================================
    // 3. Function_144_DeleteNotification (DeleteNotificationAsync: UTCID01..UTCID08)
    // =========================================================================

    [Fact]
    public async Task DeleteNotification_UTCID01_ReadNotification1001OwnedBy101_SoftDeletesNotification()
    {
        var notif = new Notification { NotificationId = 1001, UserId = 101, IsRead = true, IsDeleted = false };
        var repo = new Mock<INotificationRepository>();
        repo.Setup(r => r.GetByIdAsync(1001)).ReturnsAsync(notif);
        var service = new NotificationService(repo.Object);

        await service.DeleteNotificationAsync(1001, 101, strictValidation: true);

        Assert.True(notif.IsDeleted);
        repo.Verify(r => r.UpdateAsync(notif), Times.Once);
        repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task DeleteNotification_UTCID02_UnreadNotification1002OwnedBy101_SoftDeletesNotification()
    {
        var notif = new Notification { NotificationId = 1002, UserId = 101, IsRead = false, IsDeleted = false };
        var repo = new Mock<INotificationRepository>();
        repo.Setup(r => r.GetByIdAsync(1002)).ReturnsAsync(notif);
        var service = new NotificationService(repo.Object);

        await service.DeleteNotificationAsync(1002, 101, strictValidation: true);

        Assert.True(notif.IsDeleted);
    }

    [Fact]
    public async Task DeleteNotification_UTCID03_BoundaryMinNotificationId1_SoftDeletesNotification()
    {
        var notif = new Notification { NotificationId = 1, UserId = 101, IsDeleted = false };
        var repo = new Mock<INotificationRepository>();
        repo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(notif);
        var service = new NotificationService(repo.Object);

        await service.DeleteNotificationAsync(1, 101, strictValidation: true);

        Assert.True(notif.IsDeleted);
    }

    [Fact]
    public async Task DeleteNotification_UTCID04_BoundaryHighNotificationId999999_SoftDeletesNotification()
    {
        var notif = new Notification { NotificationId = 999999, UserId = 101, IsDeleted = false };
        var repo = new Mock<INotificationRepository>();
        repo.Setup(r => r.GetByIdAsync(999999)).ReturnsAsync(notif);
        var service = new NotificationService(repo.Object);

        await service.DeleteNotificationAsync(999999, 101, strictValidation: true);

        Assert.True(notif.IsDeleted);
    }

    [Fact]
    public async Task DeleteNotification_UTCID05_NonExistentNotificationId88888_ThrowsKeyNotFoundException()
    {
        var repo = new Mock<INotificationRepository>();
        repo.Setup(r => r.GetByIdAsync(88888)).ReturnsAsync((Notification?)null);
        var service = new NotificationService(repo.Object);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.DeleteNotificationAsync(88888, 101, strictValidation: true));

        Assert.Equal("Notification not found", ex.Message);
    }

    [Fact]
    public async Task DeleteNotification_UTCID06_Notification3001OwnedByUser99_ThrowsUnauthorizedAccessException()
    {
        var notif = new Notification { NotificationId = 3001, UserId = 99, IsDeleted = false };
        var repo = new Mock<INotificationRepository>();
        repo.Setup(r => r.GetByIdAsync(3001)).ReturnsAsync(notif);
        var service = new NotificationService(repo.Object);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.DeleteNotificationAsync(3001, 101, strictValidation: true));

        Assert.Equal("You do not have access to this notification", ex.Message);
    }

    [Fact]
    public async Task DeleteNotification_UTCID07_InvalidZeroOrNegativeNotificationId_ThrowsArgumentException()
    {
        var repo = new Mock<INotificationRepository>();
        var service = new NotificationService(repo.Object);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.DeleteNotificationAsync(-1, 101, strictValidation: true));

        Assert.Equal("Notification ID must be positive", ex.Message);
    }

    [Fact]
    public async Task DeleteNotification_UTCID08_UnauthenticatedCaller_ThrowsUnauthorizedAccessException()
    {
        var repo = new Mock<INotificationRepository>();
        var service = new NotificationService(repo.Object);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.DeleteNotificationAsync(1001, 0, strictValidation: true));

        Assert.Equal("Authentication required", ex.Message);
    }
}
