using Microsoft.AspNetCore.Mvc;
using QueueAPI.DTOs;
using QueueAPI.Models;
using Xunit;

namespace QueueAPI.UnitTests;

public sealed class QueueSessionDateFilterTests
{
    [Fact]
    public async Task Sessions_FiltersByInclusiveVietnamDateRange()
    {
        using var context = new QueueAdminTestContext();
        await context.SeedSessionAsync(
            sessionId: "BEFORE",
            token: "TOKENBEFORE",
            updatedAt: new DateTime(2026, 11, 9, 23, 59, 59, DateTimeKind.Unspecified));
        await context.SeedSessionAsync(
            sessionId: "START",
            token: "TOKENSTART",
            userId: 8,
            updatedAt: new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Unspecified));
        await context.SeedSessionAsync(
            sessionId: "END",
            token: "TOKENEND00",
            userId: 9,
            updatedAt: new DateTime(2026, 11, 10, 23, 59, 59, DateTimeKind.Unspecified));
        await context.SeedSessionAsync(
            sessionId: "AFTER",
            token: "TOKENAFTER",
            userId: 10,
            updatedAt: new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Unspecified));

        var result = await context.Controller.Sessions(
            fromDate: new DateOnly(2026, 11, 10),
            toDate: new DateOnly(2026, 11, 10));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var sessions = Assert.IsAssignableFrom<IEnumerable<QueueAdminSessionResponse>>(ok.Value);
        Assert.Equal(new[] { "END", "START" }, sessions.Select(session => session.SessionId));
    }

    [Fact]
    public async Task Sessions_RejectsInvertedDateRange()
    {
        using var context = new QueueAdminTestContext();

        var result = await context.Controller.Sessions(
            fromDate: new DateOnly(2026, 11, 11),
            toDate: new DateOnly(2026, 11, 10));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Sessions_ReturnsAllSessionsWhenMoreThan500Exist()
    {
        using var context = new QueueAdminTestContext();
        context.Db.QueueSessions.AddRange(Enumerable.Range(1, 2000).Select(userId => new QueueSession
        {
            EventId = 12,
            UserId = userId,
            SessionId = $"SESS{userId}",
            QueueToken = $"TOKEN{userId:D4}",
            Status = "WAITING"
        }));
        await context.Db.SaveChangesAsync();

        var result = await context.Controller.Sessions();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var sessions = Assert.IsAssignableFrom<IEnumerable<QueueAdminSessionResponse>>(ok.Value);
        Assert.Equal(2000, sessions.Count());
    }
}
