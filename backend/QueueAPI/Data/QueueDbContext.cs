using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using QueueAPI.Models;

namespace QueueAPI.Data;

public sealed class QueueDbContext(DbContextOptions<QueueDbContext> options) : DbContext(options)
{
    public DbSet<QueueConfiguration> QueueConfigurations => Set<QueueConfiguration>();
    public DbSet<QueueSession> QueueSessions => Set<QueueSession>();
    public DbSet<AbuseAlert> AbuseAlerts => Set<AbuseAlert>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Existing PostgreSQL columns are timestamp without time zone. Their
        // values are Vietnam local wall-clock timestamps by application contract.
        var vietnamTimestampConverter = new ValueConverter<DateTime, DateTime>(
            value => DateTime.SpecifyKind(value, DateTimeKind.Unspecified),
            value => DateTime.SpecifyKind(value, DateTimeKind.Unspecified));

        // =========================================================
        // Queue Configuration
        // =========================================================
        modelBuilder.Entity<QueueConfiguration>(entity =>
        {
            entity.ToTable("queue_configurations");

            entity.HasKey(x => x.QueueConfigurationId);

            entity.Property(x => x.QueueConfigurationId)
                .HasColumnName("queue_configuration_id");

            entity.Property(x => x.EventId)
                .HasColumnName("event_id");

            entity.Property(x => x.PreQueueStartTime)
                .HasColumnName("pre_queue_start_time")
                .HasColumnType("timestamp without time zone")
                .HasConversion(vietnamTimestampConverter);

            entity.Property(x => x.PreQueueEndTime)
                .HasColumnName("pre_queue_end_time")
                .HasColumnType("timestamp without time zone")
                .HasConversion(vietnamTimestampConverter);

            entity.Property(x => x.AdmissionRate)
                .HasColumnName("admission_rate");

            entity.Property(x => x.QueueEnabled)
                .HasColumnName("queue_enabled");

            entity.Property(x => x.FairnessPolicy)
                .HasColumnName("fairness_policy")
                .HasMaxLength(40)
                .IsRequired();

            entity.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp without time zone")
                .HasConversion(vietnamTimestampConverter);

            entity.Property(x => x.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("timestamp without time zone")
                .HasConversion(vietnamTimestampConverter);

            entity.HasIndex(x => x.EventId)
                .IsUnique();
        });

        // =========================================================
        // Queue Session
        // =========================================================
        modelBuilder.Entity<QueueSession>(entity =>
        {
            entity.ToTable("queue_sessions");

            entity.HasKey(x => x.QueueSessionId);

            entity.Property(x => x.QueueSessionId)
                .HasColumnName("queue_session_id");

            entity.Property(x => x.EventId)
                .HasColumnName("event_id");

            entity.Property(x => x.UserId)
                .HasColumnName("user_id");

            entity.Property(x => x.SessionId)
                .HasColumnName("session_id");

            entity.Property(x => x.QueueToken)
                .HasColumnName("queue_token");

            entity.Property(x => x.Status)
                .HasColumnName("status");

            entity.Property(x => x.Position)
                .HasColumnName("position");

            entity.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp without time zone")
                .HasConversion(vietnamTimestampConverter);

            entity.Property(x => x.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("timestamp without time zone")
                .HasConversion(vietnamTimestampConverter);

            entity.HasIndex(x => new { x.EventId, x.UserId })
                .IsUnique();

            entity.HasIndex(x => x.QueueToken)
                .IsUnique();
        });

        // =========================================================
        // Abuse Alert
        // =========================================================
        modelBuilder.Entity<AbuseAlert>(entity =>
        {
            entity.ToTable("queue_abuse_alerts");

            entity.HasKey(x => x.AbuseAlertId);

            entity.Property(x => x.AbuseAlertId)
                .HasColumnName("abuse_alert_id");

            entity.Property(x => x.EventId)
                .HasColumnName("event_id");

            entity.Property(x => x.UserId)
                .HasColumnName("user_id");

            entity.Property(x => x.SessionId)
                .HasColumnName("session_id");

            entity.Property(x => x.QueueToken)
                .HasColumnName("queue_token");

            entity.Property(x => x.AlertType)
                .HasColumnName("alert_type");

            entity.Property(x => x.Severity)
                .HasColumnName("severity");

            entity.Property(x => x.Status)
                .HasColumnName("status");

            entity.Property(x => x.DetectedBehavior)
                .HasColumnName("detected_behavior");

            entity.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamp without time zone")
                .HasConversion(vietnamTimestampConverter);

            entity.HasIndex(x => new { x.EventId, x.CreatedAt });
        });

    }
}