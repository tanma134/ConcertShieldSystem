using Microsoft.EntityFrameworkCore;
using PaymentAPI.Models;

namespace PaymentAPI.Data
{
    public class PaymentDbContext : DbContext
    {
        public PaymentDbContext(DbContextOptions<PaymentDbContext> options)
            : base(options)
        {
        }

        public virtual DbSet<Voucher> Vouchers { get; set; } = null!;
        public virtual DbSet<VoucherUsage> VoucherUsages { get; set; } = null!;
        public virtual DbSet<PaymentTransaction> PaymentTransactions { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Vouchers
            modelBuilder.Entity<Voucher>(entity =>
            {
                entity.ToTable("vouchers");
                entity.HasKey(e => e.VoucherId).HasName("vouchers_pkey");

                entity.Property(e => e.VoucherId).HasColumnName("voucher_id").UseIdentityAlwaysColumn();
                entity.Property(e => e.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
                entity.Property(e => e.Scope).HasColumnName("scope").HasMaxLength(20).IsRequired();
                entity.Property(e => e.OrganizerId).HasColumnName("organizer_id");
                entity.Property(e => e.EventId).HasColumnName("event_id");
                entity.Property(e => e.DiscountType).HasColumnName("discount_type").HasMaxLength(10).IsRequired();
                entity.Property(e => e.DiscountAmount).HasColumnName("discount_amount");
                entity.Property(e => e.DiscountPercent).HasColumnName("discount_percent").HasPrecision(5, 2);
                entity.Property(e => e.MaxDiscountAmount).HasColumnName("max_discount_amount");
                entity.Property(e => e.MinOrderAmount).HasColumnName("min_order_amount").HasDefaultValue(0L);
                entity.Property(e => e.TotalQuantity).HasColumnName("total_quantity").IsRequired();
                entity.Property(e => e.UsedQuantity).HasColumnName("used_quantity").HasDefaultValue(0);
                entity.Property(e => e.MaxUsagePerUser).HasColumnName("max_usage_per_user").HasDefaultValue(1);
                entity.Property(e => e.StartsAt).HasColumnName("starts_at").IsRequired();
                entity.Property(e => e.EndsAt).HasColumnName("ends_at").IsRequired();
                entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
                entity.Property(e => e.CreatedBy).HasColumnName("created_by").IsRequired();
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
                entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
                entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
                entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");

                entity.HasIndex(e => e.Code).IsUnique().HasDatabaseName("vouchers_code_key");
                entity.HasIndex(e => e.EventId).HasDatabaseName("ix_vouchers_event_id");

                entity.HasQueryFilter(e => !e.IsDeleted);
            });

            // Voucher Usages
            modelBuilder.Entity<VoucherUsage>(entity =>
            {
                entity.ToTable("voucher_usages");
                entity.HasKey(e => e.VoucherUsageId).HasName("voucher_usages_pkey");

                entity.Property(e => e.VoucherUsageId).HasColumnName("voucher_usage_id").UseIdentityAlwaysColumn();
                entity.Property(e => e.VoucherId).HasColumnName("voucher_id").IsRequired();
                entity.Property(e => e.OrderId).HasColumnName("order_id").IsRequired();
                entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
                entity.Property(e => e.DiscountAmount).HasColumnName("discount_amount").IsRequired();
                entity.Property(e => e.UsedAt).HasColumnName("used_at").HasDefaultValueSql("now()");

                entity.HasIndex(e => new { e.VoucherId, e.OrderId, e.UserId })
                    .IsUnique()
                    .HasDatabaseName("uq_voucher_usages_voucher_order_user");

                entity.HasOne(d => d.Voucher)
                    .WithMany(p => p.Usages)
                    .HasForeignKey(d => d.VoucherId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("fk_voucher_usages_voucher");
            });

            // Payment Transactions
            modelBuilder.Entity<PaymentTransaction>(entity =>
            {
                entity.ToTable("payment_transactions");
                entity.HasKey(e => e.PaymentTransactionId).HasName("payment_transactions_pkey");

                entity.Property(e => e.PaymentTransactionId).HasColumnName("payment_transaction_id").UseIdentityAlwaysColumn();
                entity.Property(e => e.OrderId).HasColumnName("order_id").IsRequired();
                entity.Property(e => e.Gateway).HasColumnName("gateway").HasMaxLength(30).HasDefaultValue("VNPay");
                entity.Property(e => e.GatewayTransactionRef).HasColumnName("gateway_transaction_ref").HasMaxLength(100);
                entity.Property(e => e.Amount).HasColumnName("amount").IsRequired();
                entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("Initiated");
                entity.Property(e => e.WebhookPayload).HasColumnName("webhook_payload").HasColumnType("jsonb");
                entity.Property(e => e.RequestedAt).HasColumnName("requested_at").HasDefaultValueSql("now()");
                entity.Property(e => e.RespondedAt).HasColumnName("responded_at");

                entity.HasIndex(e => e.OrderId).HasDatabaseName("ix_payment_transactions_order_id");
            });
        }
    }
}
