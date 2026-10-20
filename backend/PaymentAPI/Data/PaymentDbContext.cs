using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using PaymentAPI.Models;

namespace PaymentAPI.Data;

public partial class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<PaymentReconciliationResolution> PaymentReconciliationResolutions { get; set; } = null!;

    public virtual DbSet<PaymentRefund> PaymentRefunds { get; set; } = null!;

    public virtual DbSet<PaymentTransaction> PaymentTransactions { get; set; } = null!;

    public virtual DbSet<Voucher> Vouchers { get; set; } = null!;

    public virtual DbSet<VoucherUsage> VoucherUsages { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PaymentReconciliationResolution>(entity =>
        {
            entity.HasKey(e => e.PaymentReconciliationResolutionId).HasName("payment_reconciliation_resolutions_pkey");

            entity.ToTable("payment_reconciliation_resolutions", table =>
                table.HasCheckConstraint("ck_reconciliation_resolution_status", "resolution_status IN ('Resolved', 'Dismissed')"));

            entity.HasIndex(e => e.PaymentTransactionId, "uq_reconciliation_resolution_transaction").IsUnique();

            entity.Property(e => e.PaymentReconciliationResolutionId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("payment_reconciliation_resolution_id");
            entity.Property(e => e.PaymentTransactionId).HasColumnName("payment_transaction_id");
            entity.Property(e => e.ResolutionAction)
                .HasMaxLength(50)
                .HasColumnName("resolution_action");
            entity.Property(e => e.ResolutionNote).HasColumnName("resolution_note");
            entity.Property(e => e.ResolutionStatus)
                .HasMaxLength(20)
                .HasColumnName("resolution_status");
            entity.Property(e => e.ResolvedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("resolved_at");
            entity.Property(e => e.ResolvedByUserId).HasColumnName("resolved_by_user_id");

            entity.HasOne(d => d.PaymentTransaction).WithOne(p => p.PaymentReconciliationResolution)
                .HasForeignKey<PaymentReconciliationResolution>(d => d.PaymentTransactionId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_reconciliation_resolution_payment_transaction");
        });

        modelBuilder.Entity<PaymentRefund>(entity =>
        {
            entity.HasKey(e => e.PaymentRefundId).HasName("payment_refunds_pkey");

            entity.ToTable("payment_refunds");

            entity.HasIndex(e => e.ReturnRequestId, "uq_payment_refunds_return_request").IsUnique();
            entity.HasIndex(e => e.OrderId, "ix_payment_refunds_order_id");

            entity.Property(e => e.PaymentRefundId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("payment_refund_id");
            entity.Property(e => e.ReturnRequestId).HasColumnName("return_request_id");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.Amount).HasColumnName("amount");
            entity.Property(e => e.Gateway)
                .HasMaxLength(30)
                .HasColumnName("gateway");
            entity.Property(e => e.GatewayRefundRef)
                .HasMaxLength(100)
                .HasColumnName("gateway_refund_ref");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasColumnName("status");
            entity.Property(e => e.ErrorMessage)
                .HasMaxLength(500)
                .HasColumnName("error_message");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.RefundType).HasMaxLength(20).HasColumnName("refund_type");
            entity.Property(e => e.Reason).HasMaxLength(255).HasColumnName("reason");
            entity.Property(e => e.ProcessedByUserId).HasColumnName("processed_by_user_id");
            entity.Property(e => e.ProcessedByUserName).HasMaxLength(100).HasColumnName("processed_by_user_name");
            entity.Property(e => e.CustomerName).HasMaxLength(150).HasColumnName("customer_name");
            entity.Property(e => e.GatewayErrorCode).HasMaxLength(50).HasColumnName("gateway_error_code");
            entity.Property(e => e.RetryCount).HasColumnName("retry_count");
            entity.Property(e => e.LastRetryAt).HasColumnName("last_retry_at");
            entity.Property(e => e.GatewayAmount).HasColumnName("gateway_amount");
            entity.Property(e => e.ReconciliationStatus).HasMaxLength(30).HasColumnName("reconciliation_status");
            entity.Property(e => e.EventId).HasColumnName("event_id");
            entity.Property(e => e.EventName).HasMaxLength(255).HasColumnName("event_name");
        });

        modelBuilder.Entity<PaymentTransaction>(entity =>
        {
            entity.HasKey(e => e.PaymentTransactionId).HasName("payment_transactions_pkey");

            entity.ToTable("payment_transactions");

            entity.HasIndex(e => e.OrderId, "ix_payment_transactions_order_id");
            entity.HasIndex(e => new { e.UserId, e.Status }, "ix_payment_transactions_user_status");

            entity.Property(e => e.PaymentTransactionId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("payment_transaction_id");
            entity.Property(e => e.Amount).HasColumnName("amount");
            entity.Property(e => e.ClientIp)
                .HasMaxLength(64)
                .HasColumnName("client_ip");
            entity.Property(e => e.DeviceFp)
                .HasMaxLength(128)
                .HasColumnName("device_fp");
            entity.Property(e => e.Gateway)
                .HasMaxLength(30)
                .HasDefaultValueSql("'VNPay'::character varying")
                .HasColumnName("gateway");
            entity.Property(e => e.GatewayTransactionRef)
                .HasMaxLength(100)
                .HasColumnName("gateway_transaction_ref");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.RequestedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("requested_at");
            entity.Property(e => e.RespondedAt).HasColumnName("responded_at");
            entity.Property(e => e.SessionId)
                .HasMaxLength(128)
                .HasColumnName("session_id");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'Initiated'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.WebhookPayload)
                .HasColumnType("jsonb")
                .HasColumnName("webhook_payload");
        });

        modelBuilder.Entity<Voucher>(entity =>
        {
            entity.HasKey(e => e.VoucherId).HasName("vouchers_pkey");

            entity.ToTable("vouchers");

            entity.HasIndex(e => e.EventId, "ix_vouchers_event_id");
            entity.HasIndex(e => e.Code, "vouchers_code_key").IsUnique();

            entity.Property(e => e.VoucherId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("voucher_id");
            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .HasColumnName("code")
                .IsRequired();
            entity.Property(e => e.Scope)
                .HasMaxLength(20)
                .HasColumnName("scope")
                .IsRequired();
            entity.Property(e => e.OrganizerId).HasColumnName("organizer_id");
            entity.Property(e => e.EventId).HasColumnName("event_id");
            entity.Property(e => e.DiscountType)
                .HasMaxLength(10)
                .HasColumnName("discount_type")
                .IsRequired();
            entity.Property(e => e.DiscountAmount).HasColumnName("discount_amount");
            entity.Property(e => e.DiscountPercent)
                .HasPrecision(5, 2)
                .HasColumnName("discount_percent");
            entity.Property(e => e.MaxDiscountAmount).HasColumnName("max_discount_amount");
            entity.Property(e => e.MinOrderAmount)
                .HasDefaultValue(0L)
                .HasColumnName("min_order_amount");
            entity.Property(e => e.TotalQuantity).HasColumnName("total_quantity").IsRequired();
            entity.Property(e => e.UsedQuantity)
                .HasDefaultValue(0)
                .HasColumnName("used_quantity");
            entity.Property(e => e.MaxUsagePerUser)
                .HasDefaultValue(1)
                .HasColumnName("max_usage_per_user");
            entity.Property(e => e.StartsAt).HasColumnName("starts_at").IsRequired();
            entity.Property(e => e.EndsAt).HasColumnName("ends_at").IsRequired();
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by").IsRequired();
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.IsDeleted)
                .HasDefaultValue(false)
                .HasColumnName("is_deleted");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<VoucherUsage>(entity =>
        {
            entity.HasKey(e => e.VoucherUsageId).HasName("voucher_usages_pkey");

            entity.ToTable("voucher_usages");

            entity.HasIndex(e => new { e.VoucherId, e.OrderId, e.UserId }, "uq_voucher_usages_voucher_order_user").IsUnique();
            entity.HasIndex(e => e.OrderId, "uq_voucher_usages_order_id").IsUnique();

            entity.Property(e => e.VoucherUsageId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("voucher_usage_id");
            entity.Property(e => e.VoucherId).HasColumnName("voucher_id").IsRequired();
            entity.Property(e => e.OrderId).HasColumnName("order_id").IsRequired();
            entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(e => e.DiscountAmount).HasColumnName("discount_amount").IsRequired();
            entity.Property(e => e.UsedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("used_at");

            // Cùng điều kiện lọc với Voucher để EF không cảnh báo (10622).
            entity.HasQueryFilter(e => !e.Voucher.IsDeleted);

            entity.HasOne(d => d.Voucher).WithMany(p => p.VoucherUsages)
                .HasForeignKey(d => d.VoucherId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_voucher_usages_voucher");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
