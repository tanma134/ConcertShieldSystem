using System;
using System.Collections.Generic;
using AdminAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace AdminAPI.Data;

public partial class AdminDbContext : DbContext
{
    public AdminDbContext(DbContextOptions<AdminDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<FraudAlert> FraudAlerts { get; set; }

    public virtual DbSet<RiskAppeal> RiskAppeals { get; set; }

    public virtual DbSet<RiskDecision> RiskDecisions { get; set; }

    public virtual DbSet<RiskList> RiskLists { get; set; }

    public virtual DbSet<RiskNotification> RiskNotifications { get; set; }

    public virtual DbSet<SystemParameter> SystemParameters { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.AuditLogId).HasName("audit_logs_pkey");

            entity.Property(e => e.AuditLogId).UseIdentityAlwaysColumn();
            entity.Property(e => e.ActorType).HasDefaultValueSql("'user'::character varying");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<FraudAlert>(entity =>
        {
            entity.HasKey(e => e.FraudAlertId).HasName("fraud_alerts_pkey");

            entity.Property(e => e.FraudAlertId).UseIdentityAlwaysColumn();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.RiskLevel).HasDefaultValueSql("'Low'::character varying");
            entity.Property(e => e.Status).HasDefaultValueSql("'Open'::character varying");
        });

        modelBuilder.Entity<RiskAppeal>(entity =>
        {
            entity.HasKey(e => e.RiskAppealId).HasName("risk_appeal_pkey");

            entity.HasIndex(e => e.SlaDueAt, "ix_risk_appeal_pending").HasFilter("((status)::text = 'Pending'::text)");

            entity.Property(e => e.RiskAppealId).UseIdentityAlwaysColumn();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.Status).HasDefaultValueSql("'Pending'::character varying");

            entity.HasOne(d => d.RiskDecision).WithOne(p => p.RiskAppeal).HasConstraintName("fk_risk_appeal_decision");
        });

        modelBuilder.Entity<RiskDecision>(entity =>
        {
            entity.HasKey(e => e.RiskDecisionId).HasName("risk_decision_pkey");

            entity.HasIndex(e => new { e.Status, e.ExpiresAt }, "ix_risk_decision_active").HasFilter("((status)::text = 'Active'::text)");

            entity.Property(e => e.RiskDecisionId).UseIdentityAlwaysColumn();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.DecidedByType).HasDefaultValueSql("'SYSTEM'::character varying");
            entity.Property(e => e.EscalationLevel).HasDefaultValue((short)1);
            entity.Property(e => e.IsShadow).HasDefaultValue(false);
            entity.Property(e => e.Status).HasDefaultValueSql("'Active'::character varying");

            entity.HasOne(d => d.FraudAlert).WithMany(p => p.RiskDecisions)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_risk_decision_alert");
        });

        modelBuilder.Entity<RiskList>(entity =>
        {
            entity.HasKey(e => e.RiskListId).HasName("risk_list_pkey");

            entity.Property(e => e.RiskListId).UseIdentityAlwaysColumn();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<RiskNotification>(entity =>
        {
            entity.HasKey(e => e.RiskNotificationId).HasName("risk_notification_pkey");

            entity.HasIndex(e => e.CreatedAt, "ix_risk_notification_pending").HasFilter("(delivered_at IS NULL)");

            entity.Property(e => e.RiskNotificationId).UseIdentityAlwaysColumn();
            entity.Property(e => e.CanAppeal).HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.RiskDecision).WithMany(p => p.RiskNotifications)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_risk_notification_decision");
        });

        modelBuilder.Entity<SystemParameter>(entity =>
        {
            entity.HasKey(e => e.SystemParameterId).HasName("system_parameters_pkey");

            entity.Property(e => e.SystemParameterId).UseIdentityAlwaysColumn();
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
