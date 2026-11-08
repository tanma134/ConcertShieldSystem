using System.Net;
using System.Security.Claims;
using AdminAPI.Data;
using AdminAPI.Models;
using AdminAPI.Services;
using AdminAPI.Services.Risks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AdminAPI.Tests.Support;

/// <summary>AdminDbContext for tests: EF InMemory, IPAddress stored as text, SaveChanges can be forced to fail.</summary>
public sealed class TestDb : AdminDbContext
{
    public Exception? ThrowOnSave { get; set; }
    public Action? BeforeThrow { get; set; }
    public int SaveCalls { get; private set; }

    public TestDb(DbContextOptions<AdminDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<RiskDecision>().Property(x => x.IpAddress)
            .HasConversion(v => v == null ? null : v.ToString(), v => v == null ? null : IPAddress.Parse(v));
        modelBuilder.Entity<AuditLog>().Property(x => x.IpAddress)
            .HasConversion(v => v == null ? null : v.ToString(), v => v == null ? null : IPAddress.Parse(v));
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCalls++;
        if (ThrowOnSave != null) { BeforeThrow?.Invoke(); throw ThrowOnSave; }
        return base.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Base class: one InMemory database per test, mocked IRiskConfigProvider and seed helpers.</summary>
public abstract class DbTestBase
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    protected readonly TestDb Db;
    protected readonly RiskConfig Config = new();            // defaults: AlertSlaHours 2, AppealDeadlineDays 7, AppealSlaHours 48 ...
    protected readonly Mock<IRiskConfigProvider> Cfg = new();

    protected DbTestBase()
    {
        Db = NewContext();
        Cfg.Setup(c => c.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Config);
    }

    protected TestDb NewContext() => new(new DbContextOptionsBuilder<AdminDbContext>().UseInMemoryDatabase(_dbName).Options);

    protected FraudAlert Alert(int id = 10, string status = "Open", string level = "High", string type = "Bot", int? userId = 7,
        decimal score = 80m, DateTime? created = null, int? assignedTo = null, string? details = null, int? ticketId = null,
        int? orderId = null, string? session = null, string? ai = null)
    {
        var a = new FraudAlert
        {
            FraudAlertId = id, Status = status, RiskLevel = level, AlertType = type, UserId = userId, RiskScore = score,
            BotScore = score, FraudScore = 0, CreatedAt = created ?? DateTime.UtcNow.AddDays(-1), AssignedTo = assignedTo,
            AssignedAt = assignedTo.HasValue ? DateTime.UtcNow.AddHours(-1) : null, Details = details, TicketId = ticketId,
            OrderId = orderId, SessionId = session, AiSummary = ai
        };
        Db.FraudAlerts.Add(a); Db.SaveChanges(); return a;
    }

    protected RiskDecision Decision(int id = 20, string action = "BLOCK", string status = "Active", bool shadow = false,
        int? userId = 7, int? alertId = null, string? scope = "ACCOUNT", DateTime? expires = null, DateTime? created = null,
        DateTime? notified = null, string by = "SYSTEM", int? decidedBy = null, string? session = null, string? device = null,
        string? ip = null, int? ticketId = null, string? reasonPublic = "Your account has been restricted.")
    {
        var d = new RiskDecision
        {
            RiskDecisionId = id, DecisionCode = "RD-" + id, FraudAlertId = alertId, UserId = userId, TicketId = ticketId,
            SessionId = session, DeviceFp = device, IpAddress = ip == null ? null : IPAddress.Parse(ip), RiskLevel = "High",
            RuleVersion = "v1.0", Action = action, Scope = scope, ReasonPublic = reasonPublic, IsShadow = shadow, Status = status,
            ExpiresAt = expires, DecidedByType = by, DecidedBy = decidedBy, CreatedAt = created ?? DateTime.UtcNow.AddDays(-1),
            NotifiedAt = notified ?? DateTime.UtcNow.AddDays(-1)
        };
        Db.RiskDecisions.Add(d); Db.SaveChanges(); return d;
    }

    protected RiskAppeal Appeal(int id = 50, int decisionId = 30, int userId = 7, string status = "Pending", DateTime? sla = null, string? evidence = null)
    {
        var a = new RiskAppeal
        {
            RiskAppealId = id, RiskDecisionId = decisionId, UserId = userId, Reason = "My account was blocked by mistake",
            Evidence = evidence, Status = status, SlaDueAt = sla ?? DateTime.UtcNow.AddHours(24), CreatedAt = DateTime.UtcNow.AddHours(-1)
        };
        Db.RiskAppeals.Add(a); Db.SaveChanges(); return a;
    }

    protected SystemParameter Param(string key, string value, string category = "risk_config")
    {
        var p = new SystemParameter { ParamKey = key, ParamValue = value, Category = category, UpdatedBy = 1, UpdatedAt = DateTime.UtcNow.AddDays(-1) };
        Db.SystemParameters.Add(p); Db.SaveChanges(); return p;
    }

    protected static async Task Err(Func<Task> act, int status, string messagePart)
    {
        var ex = await Assert.ThrowsAsync<RiskServiceException>(act);
        Assert.Equal(status, ex.StatusCode);
        Assert.Contains(messagePart, ex.Message);
    }

    protected static async Task AErr(Func<Task> act, int status, string messagePart)
    {
        var ex = await Assert.ThrowsAsync<AppealServiceException>(act);
        Assert.Equal(status, ex.StatusCode);
        Assert.Contains(messagePart, ex.Message);
    }
}

