using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace TicketAPI.Models;

public partial class TicketDbContext : DbContext
{
    public TicketDbContext()
    {
    }

    public TicketDbContext(DbContextOptions<TicketDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderAttendee> OrderAttendees { get; set; }

    public virtual DbSet<OrderDetail> OrderDetails { get; set; }

    public virtual DbSet<Ticket> Tickets { get; set; }

    public virtual DbSet<TicketQrToken> TicketQrTokens { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=05_ticket_db;Username=postgres;Password=123456");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.OrderId).HasName("orders_pkey");

            entity.ToTable("orders");

            entity.HasIndex(e => e.CustomerId, "ix_orders_customer_id");

            entity.HasIndex(e => e.EventId, "ix_orders_event_id");

            entity.HasIndex(e => e.Status, "ix_orders_status");

            entity.Property(e => e.OrderId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("order_id");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.DiscountAmount)
                .HasDefaultValue(0L)
                .HasColumnName("discount_amount");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnName("email");
            entity.Property(e => e.EndsAt).HasColumnName("ends_at");
            entity.Property(e => e.EventId).HasColumnName("event_id");
            entity.Property(e => e.EventName).HasColumnName("event_name");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.FinalAmount).HasColumnName("final_amount");
            entity.Property(e => e.FullName)
                .HasMaxLength(255)
                .HasColumnName("full_name");
            entity.Property(e => e.HoldToken)
                .HasMaxLength(255)
                .HasColumnName("hold_token");
            entity.Property(e => e.IsDeleted)
                .HasDefaultValue(false)
                .HasColumnName("is_deleted");
            entity.Property(e => e.OrderDate)
                .HasDefaultValueSql("now()")
                .HasColumnName("order_date");
            entity.Property(e => e.PaymentGatewayRef)
                .HasMaxLength(255)
                .HasColumnName("payment_gateway_ref");
            entity.Property(e => e.PaymentMethod)
                .HasMaxLength(50)
                .HasColumnName("payment_method");
            entity.Property(e => e.Phone)
                .HasMaxLength(50)
                .HasColumnName("phone");
            entity.Property(e => e.PosterUrl).HasColumnName("poster_url");
            entity.Property(e => e.QueueSessionId).HasColumnName("queue_session_id");
            entity.Property(e => e.StartsAt).HasColumnName("starts_at");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'Pending'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.TotalAmount).HasColumnName("total_amount");
            entity.Property(e => e.VoucherId).HasColumnName("voucher_id");
        });

        modelBuilder.Entity<OrderAttendee>(entity =>
        {
            entity.HasKey(e => e.AttendeeId).HasName("order_attendees_pkey");

            entity.ToTable("order_attendees");

            entity.HasIndex(e => e.OrderId, "ix_order_attendees_order_id");

            entity.Property(e => e.AttendeeId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("attendee_id");
            entity.Property(e => e.CitizenId)
                .HasMaxLength(50)
                .HasColumnName("citizen_id");
            entity.Property(e => e.FullName)
                .HasMaxLength(255)
                .HasColumnName("full_name");
            entity.Property(e => e.IsPrimaryBuyer)
                .HasDefaultValue(false)
                .HasColumnName("is_primary_buyer");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.Phone)
                .HasMaxLength(50)
                .HasColumnName("phone");
            entity.Property(e => e.SeatId).HasColumnName("seat_id");
            entity.Property(e => e.TicketTypeId).HasColumnName("ticket_type_id");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderAttendees)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("fk_order_attendees_order");
        });

        modelBuilder.Entity<OrderDetail>(entity =>
        {
            entity.HasKey(e => e.OrderDetailId).HasName("order_details_pkey");

            entity.ToTable("order_details");

            entity.HasIndex(e => e.OrderId, "ix_order_details_order_id");

            entity.Property(e => e.OrderDetailId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("order_detail_id");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.SeatIds)
                .HasColumnType("jsonb")
                .HasColumnName("seat_ids");
            entity.Property(e => e.TicketTypeId).HasColumnName("ticket_type_id");
            entity.Property(e => e.TicketTypeName)
                .HasMaxLength(100)
                .HasColumnName("ticket_type_name");
            entity.Property(e => e.UnitPrice).HasColumnName("unit_price");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderDetails)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("fk_order_details_order");
        });

        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.HasKey(e => e.TicketId).HasName("tickets_pkey");

            entity.ToTable("tickets");

            entity.HasIndex(e => e.EventId, "ix_tickets_event_id");

            entity.HasIndex(e => e.OrderId, "ix_tickets_order_id");

            entity.HasIndex(e => e.OwnerUserId, "ix_tickets_owner_user_id");

            entity.HasIndex(e => e.TicketTypeId, "ix_tickets_ticket_type_id");

            entity.HasIndex(e => e.TicketCode, "tickets_ticket_code_key").IsUnique();

            entity.HasIndex(e => e.SeatId, "uq_tickets_seat_active")
                .IsUnique()
                .HasFilter("((seat_id IS NOT NULL) AND ((status)::text = 'Active'::text) AND (is_deleted = false))");

            entity.Property(e => e.TicketId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("ticket_id");
            entity.Property(e => e.CheckedInAt).HasColumnName("checked_in_at");
            entity.Property(e => e.CheckedInBy).HasColumnName("checked_in_by");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.EventId).HasColumnName("event_id");
            entity.Property(e => e.IsDeleted)
                .HasDefaultValue(false)
                .HasColumnName("is_deleted");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.OwnerName)
                .HasMaxLength(100)
                .HasColumnName("owner_name");
            entity.Property(e => e.OwnerUserId).HasColumnName("owner_user_id");
            entity.Property(e => e.SeatId).HasColumnName("seat_id");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'Active'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.TicketCode)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("ticket_code");
            entity.Property(e => e.TicketTypeId).HasColumnName("ticket_type_id");
            entity.Property(e => e.TicketTypeName)
                .HasMaxLength(100)
                .HasColumnName("ticket_type_name");

            entity.HasOne(d => d.Order).WithMany(p => p.Tickets)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("fk_tickets_order");
        });

        modelBuilder.Entity<TicketQrToken>(entity =>
        {
            entity.HasKey(e => e.TicketQrTokenId).HasName("ticket_qr_tokens_pkey");

            entity.ToTable("ticket_qr_tokens");

            entity.HasIndex(e => e.QrToken, "ix_ticket_qr_tokens_qr");

            entity.HasIndex(e => e.TicketId, "ix_ticket_qr_tokens_ticket_active").HasFilter("(is_revoked = false)");

            entity.HasIndex(e => e.QrToken, "ticket_qr_tokens_qr_token_key").IsUnique();

            entity.Property(e => e.TicketQrTokenId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("ticket_qr_token_id");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.IsRevoked)
                .HasDefaultValue(false)
                .HasColumnName("is_revoked");
            entity.Property(e => e.IsUsed)
                .HasDefaultValue(false)
                .HasColumnName("is_used");
            entity.Property(e => e.IssuedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("issued_at");
            entity.Property(e => e.QrToken)
                .HasMaxLength(200)
                .HasColumnName("qr_token");
            entity.Property(e => e.RevokedAt).HasColumnName("revoked_at");
            entity.Property(e => e.TicketId).HasColumnName("ticket_id");
            entity.Property(e => e.UsedAt).HasColumnName("used_at");

            entity.HasOne(d => d.Ticket).WithMany(p => p.TicketQrTokens)
                .HasForeignKey(d => d.TicketId)
                .HasConstraintName("fk_ticket_qr_tokens_ticket");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
