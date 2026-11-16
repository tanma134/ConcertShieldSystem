// Generated from the Unit Test Case matrix (AdminAPI_Unit_Test_Case.xlsx). Method names = TestMapping.
using System.Security.Claims;
using AdminAPI.Controllers;
using AdminAPI.DTOs;
using AdminAPI.Models;
using AdminAPI.Services;
using AdminAPI.Services.Risks;
using AdminAPI.Tests.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AdminAPI.Tests.Services;

public class AuditLogServiceTests : DbTestBase
{
    private static readonly DateTime From = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private AuditLogService Sut() => new(Db);

    private AuditLog Log(int id, DateTime? created = null, int? userId = 7, string actor = "admin", string action = "LOGIN",
        string entity = "User", string? entityId = "1", string? ip = "203.0.113.9", string? oldV = null, string? newV = null)
    {
        var l = new AuditLog
        {
            AuditLogId = id,
            UserId = userId,
            ActorType = actor,
            Action = action,
            EntityType = entity,
            EntityId = entityId,
            IpAddress = ip == null ? null : System.Net.IPAddress.Parse(ip),
            OldValue = oldV,
            NewValue = newV,
            UserAgent = "xunit",
            RequestId = "req-" + id,
            CreatedAt = created ?? DateTime.UtcNow.AddHours(-id)
        };
        Db.AuditLogs.Add(l); Db.SaveChanges(); return l;
    }

    private void SeedMany(int count, DateTime created)
    {
        Db.AuditLogs.AddRange(Enumerable.Range(1, count).Select(i => new AuditLog
        { AuditLogId = i, ActorType = "system", Action = "LOGIN", EntityType = "User", CreatedAt = created.AddSeconds(i) }));
        Db.SaveChanges();
    }

    private static AuditLogQuery Q(DateTime? to = null) => new() { From = From, To = to ?? From.AddDays(30) };
    private static string Text(byte[] content) => System.Text.Encoding.UTF8.GetString(content, 3, content.Length - 3);
    private static IEnumerable<string> DataRows(byte[] content) =>
        Text(content).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).Skip(1);

    private static async Task IErr(Func<Task> act, string messagePart)
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(act);
        Assert.Contains(messagePart, ex.Message);
    }

    // ===== Function12_ActivityLogList =====

    [Fact] // N
    public async Task UTCID01_Search_NoFilter_ReturnsNewestFirstWithPaging()
    {
        Log(1, created: DateTime.UtcNow.AddHours(-3)); Log(2, created: DateTime.UtcNow.AddHours(-2), oldV: "{\"a\":1}", newV: "{\"a\":2}"); Log(3, created: DateTime.UtcNow.AddHours(-1));
        var r = await Sut().SearchAsync(new AuditLogQuery { Page = 1, PageSize = 20 });
        Assert.Equal(new long[] { 3, 2, 1 }, r.Items.Select(i => i.AuditLogId).ToArray());
        var mapped = r.Items.Single(i => i.AuditLogId == 2);
        Assert.Equal(7, mapped.UserId);
        Assert.Equal("admin", mapped.ActorType);
        Assert.Equal("203.0.113.9", mapped.IpAddress);
        Assert.Equal("{\"a\":2}", mapped.NewValue);
        Assert.Equal(3, r.TotalCount);
        Assert.Equal(1, r.Page);
        Assert.Equal(20, r.PageSize);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID02_Search_SameCreatedAt_OrderedByIdDescending()
    {
        var t = DateTime.UtcNow.AddHours(-1);
        Log(1, created: t); Log(2, created: t); Log(3, created: t);
        var r = await Sut().SearchAsync(new AuditLogQuery { Page = 1, PageSize = 20 });
        Assert.Equal(new long[] { 3, 2, 1 }, r.Items.Select(i => i.AuditLogId).ToArray());
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // N
    public async Task UTCID03_Search_FilterByUserActorActionAndEntity()
    {
        Log(1, userId: 7, actor: "admin", action: "AUDIT_LOG_EXPORTED", entity: "AuditLog", entityId: "50");
        Log(2, userId: 8, actor: "admin", action: "AUDIT_LOG_EXPORTED", entity: "AuditLog", entityId: "50");
        Log(3, userId: 7, actor: "staff", action: "AUDIT_LOG_EXPORTED", entity: "AuditLog", entityId: "50");
        Log(4, userId: 7, actor: "admin", action: "LOGIN", entity: "AuditLog", entityId: "50");
        Log(5, userId: 7, actor: "admin", action: "AUDIT_LOG_EXPORTED", entity: "User", entityId: "50");
        Log(6, userId: 7, actor: "admin", action: "AUDIT_LOG_EXPORTED", entity: "AuditLog", entityId: "51");
        var r = await Sut().SearchAsync(new AuditLogQuery { UserId = 7, ActorType = "admin", Action = "AUDIT_LOG_EXPORTED", EntityType = "AuditLog", EntityId = "50", Page = 1, PageSize = 20 });
        Assert.Single(r.Items);
        Assert.Equal(1, r.Items[0].AuditLogId);
        Assert.Equal(1, r.TotalCount);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID04_Search_DateRange_IncludesBothEnds()
    {
        var from = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc); var to = new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);
        Log(1, created: from.AddSeconds(-1)); Log(2, created: from); Log(3, created: from.AddDays(3)); Log(4, created: to); Log(5, created: to.AddSeconds(1));
        var r = await Sut().SearchAsync(new AuditLogQuery { From = from, To = to, Page = 1, PageSize = 20 });
        Assert.Equal(new long[] { 2, 3, 4 }, r.Items.Select(i => i.AuditLogId).OrderBy(x => x).ToArray());
        Assert.Equal(3, r.TotalCount);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // A
    public async Task UTCID05_Search_FromLaterThanTo_Throws()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Sut().SearchAsync(new AuditLogQuery { From = DateTime.UtcNow, To = DateTime.UtcNow.AddDays(-1) }));
        Assert.Contains("phải nhỏ hơn hoặc bằng", ex.Message);
    }

    [Fact] // B
    public async Task UTCID06_Search_NoLogs_ReturnsEmptyPage()
    {
        var r = await Sut().SearchAsync(new AuditLogQuery { Page = 1, PageSize = 20 });
        Assert.Empty(r.Items);
        Assert.Equal(0, r.TotalCount);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // N
    public async Task UTCID07_Search_LogWithoutIp_ReturnsNullIp()
    {
        Log(1, ip: null);
        var r = await Sut().SearchAsync(new AuditLogQuery { Page = 1, PageSize = 20 });
        Assert.Single(r.Items);
        Assert.Null(r.Items[0].IpAddress);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // N
    public async Task UTCID08_Search_PageTwo_ReturnsRemainingItems()
    {
        Log(1, created: DateTime.UtcNow.AddHours(-5)); Log(2, created: DateTime.UtcNow.AddHours(-4)); Log(3, created: DateTime.UtcNow.AddHours(-3));
        Log(4, created: DateTime.UtcNow.AddHours(-2)); Log(5, created: DateTime.UtcNow.AddHours(-1));
        var r = await Sut().SearchAsync(new AuditLogQuery { Page = 2, PageSize = 2 });
        Assert.Equal(new long[] { 3, 2 }, r.Items.Select(i => i.AuditLogId).ToArray());
        Assert.Equal(2, r.Page);
        Assert.Equal(2, r.PageSize);
        Assert.Equal(5, r.TotalCount);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID09_Search_PageZero_TreatedAsPageOne()
    {
        Log(1);
        var r = await Sut().SearchAsync(new AuditLogQuery { Page = 0, PageSize = 20 });
        Assert.Equal(1, r.Page);
        Assert.Single(r.Items);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID10_Search_PageSizeZero_ResetTo20()
    {
        Log(1);
        var r = await Sut().SearchAsync(new AuditLogQuery { Page = 1, PageSize = 0 });
        Assert.Equal(20, r.PageSize);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID11_Search_PageSize100_IsAccepted()
    {
        Log(1);
        var r = await Sut().SearchAsync(new AuditLogQuery { Page = 1, PageSize = 100 });
        Assert.Equal(100, r.PageSize);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID12_Search_PageSize101_ResetTo20()
    {
        Log(1);
        var r = await Sut().SearchAsync(new AuditLogQuery { Page = 1, PageSize = 101 });
        Assert.Equal(20, r.PageSize);
        Assert.Equal(0, Db.SaveCalls);
    }

    // ===== Function13_ExportAuditLog =====

    [Fact] // N
    public async Task UTCID01_Export_ValidRangeCsv_ReturnsFileWithBomAndHeader()
    {
        Log(1, created: From.AddDays(1));
        var (content, type, name) = await Sut().ExportAsync(Q(), "csv");
        Assert.Equal("text/csv", type);
        Assert.Equal("audit-logs_20261001_20261031.csv", name);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, content.Take(3).ToArray());
        Assert.StartsWith("AuditLogId,CreatedAt,UserId,ActorType,Action,EntityType,EntityId,IpAddress,RequestId,OldValue,NewValue,UserAgent", Text(content));
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // N
    public async Task UTCID02_Export_RowsOrderedByCreatedAtAscending()
    {
        Log(1, created: From.AddDays(3)); Log(2, created: From.AddDays(1)); Log(3, created: From.AddDays(2));
        var (content, _, _) = await Sut().ExportAsync(Q(), "csv");
        var ids = DataRows(content).Select(l => l.Split(',')[0]).ToArray();
        Assert.Equal(new[] { "2", "3", "1" }, ids);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // N
    public async Task UTCID03_Export_FilterByAction_ExportsOnlyMatches()
    {
        Log(1, created: From.AddDays(1), action: "AUDIT_LOG_EXPORTED"); Log(2, created: From.AddDays(2), action: "LOGIN"); Log(3, created: From.AddDays(3), action: "AUDIT_LOG_EXPORTED");
        var q = Q(); q.Action = "AUDIT_LOG_EXPORTED";
        var (content, _, _) = await Sut().ExportAsync(q, "csv");
        Assert.Equal(new[] { "1", "3" }, DataRows(content).Select(l => l.Split(',')[0]).ToArray());
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // N
    public async Task UTCID04_Export_ValuesWithCommaQuoteAndNewline_AreEscaped()
    {
        Log(1, created: From.AddDays(1), newV: "a,b \"q\"\nline2");
        var (content, _, _) = await Sut().ExportAsync(Q(), "csv");
        Assert.Contains("\"a,b \"\"q\"\"\nline2\"", Text(content).Replace("\r\n", "\n"));
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID05_Export_UpperCaseFormat_IsAccepted()
    {
        Log(1, created: From.AddDays(1));
        var (_, type, name) = await Sut().ExportAsync(Q(), "CSV");
        Assert.Equal("text/csv", type);
        Assert.EndsWith(".csv", name);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID06_Export_NoLogsInRange_ReturnsHeaderOnly()
    {
        var (content, _, _) = await Sut().ExportAsync(Q(), "csv");
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, content.Take(3).ToArray());
        Assert.Empty(DataRows(content));
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID07_Export_Exactly90Days_IsAccepted()
    {
        Log(1, created: From.AddDays(1));
        var (_, type, _) = await Sut().ExportAsync(Q(to: From.AddDays(90)), "csv");
        Assert.Equal("text/csv", type);
        Assert.Equal(0, Db.SaveCalls);
    }

    [Fact] // B
    public async Task UTCID08_Export_90DaysPlusOneSecond_Throws()
    {
        await IErr(() => Sut().ExportAsync(Q(to: From.AddDays(90).AddSeconds(1)), "csv"), "Khoảng thời gian export tối đa");
    }

    [Fact] // A
    public async Task UTCID09_Export_FromMissing_Throws()
    {
        await IErr(() => Sut().ExportAsync(new AuditLogQuery { From = null, To = From.AddDays(30) }, "csv"), "Phải chỉ định khoảng thời gian");
    }

    [Fact] // A
    public async Task UTCID10_Export_ToMissing_Throws()
    {
        await IErr(() => Sut().ExportAsync(new AuditLogQuery { From = From, To = null }, "csv"), "Phải chỉ định khoảng thời gian");
    }

    [Fact] // A
    public async Task UTCID11_Export_FromLaterThanTo_Throws()
    {
        await IErr(() => Sut().ExportAsync(Q(to: From.AddDays(-1)), "csv"), "phải nhỏ hơn hoặc bằng");
    }

    [Fact] // A
    public async Task UTCID12_Export_UnsupportedFormat_Throws()
    {
        Log(1, created: From.AddDays(1));
        await IErr(() => Sut().ExportAsync(Q(), "xlsx"), "Định dạng export");
    }

    [Fact] // B
    public async Task UTCID13_Export_MoreThan50000Rows_Throws()
    {
        SeedMany(50_001, From.AddDays(1));
        await IErr(() => Sut().ExportAsync(Q(), "csv"), "Kết quả vượt quá");
    }

    [Fact] // B
    public async Task UTCID14_Export_Exactly50000Rows_IsAccepted()
    {
        SeedMany(50_000, From.AddDays(1));
        var (content, type, _) = await Sut().ExportAsync(Q(), "csv");
        Assert.Equal("text/csv", type);
        Assert.Equal(50_000, DataRows(content).Count());
        Assert.Equal(0, Db.SaveCalls);
    }

}