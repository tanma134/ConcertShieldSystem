using IncidentAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace IncidentAPI.Data;

// DbContext cho IncidentAPI - quản lý database incident_db (PostgreSQL)
public class IncidentDbContext : DbContext
{
    public IncidentDbContext(DbContextOptions<IncidentDbContext> options) : base(options) { }

    public virtual DbSet<Incident> Incidents { get; set; }
    public virtual DbSet<IncidentEvidence> IncidentEvidences { get; set; }
    public virtual DbSet<IncidentUpdate> IncidentUpdates { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Incident>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("incidents_pkey");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.Status).HasDefaultValueSql("'Reported'::character varying");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsEscalated).HasDefaultValue(false);

            entity.HasMany(e => e.Evidences)
                  .WithOne(ev => ev.Incident)
                  .HasForeignKey(ev => ev.IncidentId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("fk_incident_evidences_incident");

            entity.HasMany(e => e.Updates)
                  .WithOne(u => u.Incident)
                  .HasForeignKey(u => u.IncidentId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .HasConstraintName("fk_incident_updates_incident");
        });

        modelBuilder.Entity<IncidentEvidence>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("incident_evidences_pkey");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.UploadedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<IncidentUpdate>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("incident_updates_pkey");
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        });
    }
}
