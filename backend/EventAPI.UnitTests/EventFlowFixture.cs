using Npgsql;
using EventAPI.Models;
using EventAPI.Data;
using EventAPI.DTOs;
using EventAPI.Repositories;
using EventAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventAPI.UnitTests;

// Wires the real EventAPI services to an in-memory database, so a test can walk a
// concert through its whole life (Draft -> Pending -> Published ...) without PostgreSQL.
public sealed class EventFlowFixture : IDisposable
{
    public const int OrganizerId = 10;
    public const int OtherUserId = 99;
    public const int AdminId = 1;

    public EventDbContext Context { get; }
    public EventService Events { get; }
    public GovernanceService Governance { get; }
    public string ConnectionString { get; }
    private readonly string _adminConnection;
    private readonly string _databaseName;
    public TicketTypeService TicketTypes { get; }
    public RefundPolicyService RefundPolicies { get; }
    public EventAccessService Access { get; }

    public EventFlowFixture(bool requirePoster = true)
    {
        _adminConnection = Environment.GetEnvironmentVariable("CONCERTSHIELD_TEST_POSTGRES") ?? throw new InvalidOperationException("PostgreSQL test connection is required.");
        _databaseName = "concertshield_test_" + Guid.NewGuid().ToString("N");
        using (var admin = new NpgsqlConnection(_adminConnection))
        {
            admin.Open(); using var command = new NpgsqlCommand("CREATE DATABASE " + _databaseName, admin); command.ExecuteNonQuery();
        }
        ConnectionString = new NpgsqlConnectionStringBuilder(_adminConnection) { Database = _databaseName }.ConnectionString;
        Context = new EventDbContext(new DbContextOptionsBuilder<EventDbContext>().UseNpgsql(ConnectionString).Options);
        Context.Database.EnsureCreated();

        var eventRepo = new EventRepository(Context);
        var ticketRepo = new TicketTypeRepository(Context);
        var seatingRepo = new SeatingRepository(Context);
        var refundRepo = new RefundPolicyRepository(Context);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Publication:RequirePoster"] = requirePoster.ToString(),
                ["Publication:RequireBanner"] = "false",
                ["Publication:RequireRefundPolicy"] = "true"
            })
            .Build();

        var validator = new EventSubmissionValidator(eventRepo, ticketRepo, refundRepo, seatingRepo, Context, config);

        Governance = new GovernanceService(Context, new FakeAssets(), new EmptyHttpFactory(), config);
        Events = new EventService(eventRepo, ticketRepo, seatingRepo, validator,
            new FakeRoleClient(), new FakeCloudinary(), Context, NullLogger<EventService>.Instance, Governance);
        Access = new EventAccessService(eventRepo);
        TicketTypes = new TicketTypeService(ticketRepo, eventRepo, seatingRepo, Access);
        RefundPolicies = new RefundPolicyService(refundRepo, eventRepo, Access);
    }

    // A concert with every field the Info step marks as required.
    public static CreateEventDTO ValidEvent(string title = "Rock Night Can Tho") => new()
    {
        Title = title,
        ShortDescription = "A loud night.",
        LocationName = "Can Tho Convention Center",
        Address = "1 Hoa Binh Ave",
        City = "Can Tho",
        // No seat map is built in these tests, so the concert is general admission.
        SeatingMode = EventAPI.Common.SeatingMode.GeneralAdmission,
        StartsAt = DateTime.UtcNow.AddDays(60),
        EndsAt = DateTime.UtcNow.AddDays(60).AddHours(4)
    };

    public static CreateTicketTypeDTO ValidTicketType(string name = "General") => new()
    {
        TypeName = name,
        Price = 500_000,
        Quantity = 100,
        MinPerOrder = 1,
        MaxPerOrder = 4
    };

    public static CreateRefundPolicyDTO ValidRefundPolicy(string name = "Standard", int hours = 168, decimal percent = 100m) => new()
    {
        PolicyName = name,
        DeadlineBeforeEventHours = hours,
        RefundPercent = percent
    };

    // Adds everything a submission needs (poster, ticket type, refund policy) to a draft.
    public async Task CompleteDraftAsync(int eventId)
    {
        await Events.SetPosterAsync(eventId, "https://img.test/poster.jpg", "poster-1", OrganizerId, false);
        await TicketTypes.CreateAsync(eventId, ValidTicketType(), OrganizerId, false);
        await RefundPolicies.CreateAsync(eventId, ValidRefundPolicy(), OrganizerId, false);
        await SeedComplianceAsync(eventId);
    }

    // Same as CompleteDraftAsync but without the refund policy (the test adds its own).
    public async Task CompleteDraftWithoutRefundPolicyAsync(int eventId)
    {
        await SeedComplianceAsync(eventId);
        await Events.SetPosterAsync(eventId, "https://img.test/poster.jpg", "poster-1", OrganizerId, false);
        await TicketTypes.CreateAsync(eventId, ValidTicketType(), OrganizerId, false);
    }

    public async Task SeedComplianceAsync(int eventId)
    {
        var ev = await Context.Events.FindAsync(eventId); ev!.ComplianceVersion = 1;
        foreach (var type in new[] { "EventPermit", "SafetyPlan" }) Context.Set<ComplianceDocument>().Add(new() { EventId = eventId, Version = 1, DocumentType = type, FileName = type + ".pdf", ContentType = "application/pdf", PublicId = "fake-" + type, SecureUrl = "https://example.invalid/private", Size = 20, SubmittedBy = OrganizerId, SubmittedAt = DateTime.UtcNow });
        await Context.SaveChangesAsync();
    }
    public Task ApproveComplianceAsync(int eventId) => Governance.ReviewComplianceAsync(eventId, AdminId, new(1, "Approved", "Test documents reviewed"), CancellationToken.None);
    public EventDbContext NewContext() => new(new DbContextOptionsBuilder<EventDbContext>().UseNpgsql(ConnectionString).Options);
    public void Dispose()
    {
        Context.Dispose(); NpgsqlConnection.ClearAllPools();
        using var admin = new NpgsqlConnection(_adminConnection); admin.Open();
        using var command = new NpgsqlCommand("DROP DATABASE " + _databaseName + " WITH (FORCE)", admin); command.ExecuteNonQuery();
    }
    private sealed class FakeAssets : IComplianceAssetStore
    {
        public Task<StoredComplianceAsset> UploadAsync(byte[] bytes, string extension, CancellationToken ct) => Task.FromResult(new StoredComplianceAsset(Guid.NewGuid().ToString(), "https://example.invalid/private"));
        public Task<byte[]> DownloadAsync(string id, CancellationToken ct) => Task.FromResult("%PDF-test"u8.ToArray());
        public Task DeleteAsync(string id, CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class EmptyHttpFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private sealed class FakeRoleClient : IIdentityRoleClient
    {
        public Task<GrantRoleResult> GrantRoleAsync(int userId, string roleName, string? bearerToken, CancellationToken ct = default)
            => Task.FromResult(new GrantRoleResult { Success = true, Message = "ok", Roles = new List<string> { roleName } });
    }

    private sealed class FakeCloudinary : ICloudinaryService
    {
        public Task<CloudinaryUploadResult> UploadImageAsync(Stream fileStream, string fileName, string folder)
            => Task.FromResult(new CloudinaryUploadResult { SecureUrl = "https://img.test/" + fileName, PublicId = fileName });

        public Task DeleteImageAsync(string? publicId) => Task.CompletedTask;
    }
}
