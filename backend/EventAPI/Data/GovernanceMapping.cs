using System.Text.RegularExpressions;
using EventAPI.Models;
using Microsoft.EntityFrameworkCore;
namespace EventAPI.Data;
public static class GovernanceMapping
{
    // Match migration snake_case column names without altering legacy table mappings.
    public static void ConfigureGovernance(this ModelBuilder model)
    {
        Map<ComplianceDocument>(model, "event_compliance_documents");
        Map<ComplianceReview>(model, "event_compliance_reviews");
        Map<EventChangeRequest>(model, "event_change_requests");
        Map<GovernanceOutbox>(model, "governance_outbox");
        model.Entity<ComplianceDocument>().HasIndex(x => new { x.EventId, x.Version, x.DocumentType }).IsUnique();
        model.Entity<ComplianceReview>().HasIndex(x => new { x.EventId, x.Version }).IsUnique();
        model.Entity<EventChangeRequest>().HasIndex(x => x.EventId).IsUnique().HasFilter("status = 'Pending'");
        foreach (var name in new[] { nameof(Event.ComplianceStatus), nameof(Event.ComplianceVersion), nameof(Event.ComplianceReviewedVersion), nameof(Event.ScheduleVersion), nameof(Event.SalesFrozen) })
            model.Entity<Event>().Property(name).HasColumnName(Snake(name));
        // Optimistic concurrency token (xmin) prevents stale writes from overwriting governance updates.
        model.Entity<Event>().Property(x => x.RowVersion).IsRowVersion();
    }
    private static string Snake(string name) => Regex.Replace(name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant();
    private static void Map<T>(ModelBuilder model, string table) where T : class
    {
        var b = model.Entity<T>(); b.ToTable(table); b.HasKey("Id");
        foreach (var p in typeof(T).GetProperties()) b.Property(p.Name).HasColumnName(Snake(p.Name));
        b.Property<long>("Id").UseIdentityAlwaysColumn();
        if (typeof(T) != typeof(GovernanceOutbox)) b.HasOne<Event>().WithMany().HasForeignKey("EventId").OnDelete(DeleteBehavior.Restrict);
    }
}
