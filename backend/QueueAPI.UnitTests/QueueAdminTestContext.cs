using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using QueueAPI.Controllers;
using QueueAPI.Data;
using QueueAPI.Models;
using QueueAPI.Repositories;
using QueueAPI.Services;
using QueueAPI.Time;

namespace QueueAPI.UnitTests;

internal sealed class QueueAdminTestContext : IDisposable
{
    public QueueDbContext Db { get; }
    public Mock<IRedisQueueStore> Redis { get; } = new();
    public Mock<IQueueAuditLogClient> AuditLog { get; } = new();
    public QueueAdminController Controller { get; }

    public QueueAdminTestContext()
    {
        var options = new DbContextOptionsBuilder<QueueDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        Db = new QueueDbContext(options);
        var repository = new QueueRepository(Db);
        var adminService = new QueueAdminService(repository, Redis.Object, AuditLog.Object);
        Controller = new QueueAdminController(adminService);
        Controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    public async Task<QueueConfiguration> SeedConfigurationAsync(
        int eventId = 12,
        DateTime? end = null)
    {
        var configuration = new QueueConfiguration
        {
            EventId = eventId,
            PreQueueStartTime = new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Unspecified),
            PreQueueEndTime = end ?? new DateTime(2026, 11, 10, 10, 0, 0, DateTimeKind.Unspecified)
        };
        Db.QueueConfigurations.Add(configuration);
        await Db.SaveChangesAsync();
        return configuration;
    }

    public async Task<QueueSession> SeedSessionAsync(
        string sessionId = "SESS1",
        string token = "TKN1",
        string status = "WAITING",
        int eventId = 12,
        int userId = 7,
        DateTime? updatedAt = null)
    {
        var timestamp = updatedAt ?? QueueTimeZone.Now;
        var session = new QueueSession
        {
            EventId = eventId,
            UserId = userId,
            SessionId = sessionId,
            QueueToken = token,
            Status = status,
            CreatedAt = timestamp,
            UpdatedAt = timestamp
        };
        Db.QueueSessions.Add(session);
        await Db.SaveChangesAsync();
        return session;
    }

    public async Task<AbuseAlert> SeedAlertAsync(
        long alertId,
        string severity,
        string status,
        DateTime createdAt,
        string queueToken = "TOKEN1234567890",
        int eventId = 12,
        int userId = 7,
        string sessionId = "SESS1",
        string alertType = "MULTIPLE_REGISTRATION_ATTEMPTS",
        string detectedBehavior = "Test alert")
    {
        var alert = new AbuseAlert
        {
            AbuseAlertId = alertId,
            EventId = eventId,
            UserId = userId,
            SessionId = sessionId,
            TokenFingerprint = queueToken,
            AlertType = alertType,
            Severity = severity,
            Status = status,
            DetectedBehavior = detectedBehavior,
            CreatedAt = createdAt
        };
        Db.AbuseAlerts.Add(alert);
        await Db.SaveChangesAsync();
        return alert;
    }

    public void Dispose() => Db.Dispose();
}
