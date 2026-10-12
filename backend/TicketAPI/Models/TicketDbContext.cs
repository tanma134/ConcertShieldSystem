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

    public virtual DbSet<TicketReturnRequest> TicketReturnRequests { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Khi chạy qua DI, Program.cs là nguồn connection string duy nhất.
        // Fallback này chỉ phục vụ tooling/scaffolding chạy trực tiếp DbContext.
        if (!optionsBuilder.IsConfigured)
            optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=05_ticket_db;Username=postgres;Password=123456");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

        modelBuilder.Entity<AppliedEventChange>(b => {
            b.ToTable("applied_event_changes"); b.HasKey(x => x.ChangeId);
            b.HasIndex(x => new { x.EventId, x.ScheduleVersion }).IsUnique();
            foreach (var p in typeof(AppliedEventChange).GetProperties()) b.Property(p.Name).HasColumnName(System.Text.RegularExpressions.Regex.Replace(p.Name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant());
            b.Property(x => x.ChangeId).ValueGeneratedNever();
        });
        modelBuilder.Entity<AffectedTicket>(b => {
            b.ToTable("affected_tickets"); b.HasKey(x => x.Id);
            foreach (var p in typeof(AffectedTicket).GetProperties()) b.Property(p.Name).HasColumnName(System.Text.RegularExpressions.Regex.Replace(p.Name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant());
            b.Property(x => x.Id).UseIdentityAlwaysColumn();
            b.HasIndex(x => new { x.ChangeId, x.TicketId }).IsUnique();
            b.HasOne<AppliedEventChange>().WithMany().HasForeignKey(x => x.ChangeId);
            b.HasOne<Ticket>().WithMany().HasForeignKey(x => x.TicketId);
        });

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

        modelBuilder.Entity<TicketReturnRequest>(entity =>
        {
            entity.HasKey(e => e.TicketReturnRequestId).HasName("ticket_return_requests_pkey");

            entity.ToTable("ticket_return_requests");

            entity.HasIndex(e => new { e.RequesterUserId, e.CreatedAt }, "ix_ticket_return_requests_requester");

            entity.HasIndex(e => e.TicketId, "uq_ticket_return_requests_open")
                .IsUnique()
                .HasFilter("((status)::text = 'Pending'::text)");

            entity.Property(e => e.TicketReturnRequestId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("ticket_return_request_id");
            entity.Property(e => e.TicketId).HasColumnName("ticket_id");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.EventId).HasColumnName("event_id");
            entity.Property(e => e.RequesterUserId).HasColumnName("requester_user_id");
            entity.Property(e => e.Reason).HasMaxLength(500).HasColumnName("reason");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'Pending'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.RefundAmount).HasColumnName("refund_amount");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.CancelledAt).HasColumnName("cancelled_at");
            entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");
            entity.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");
            entity.Property(e => e.ReviewNote).HasMaxLength(500).HasColumnName("review_note");
            entity.Property(e => e.RefundedAt).HasColumnName("refunded_at");
            entity.Property(e => e.RefundReference).HasMaxLength(100).HasColumnName("refund_reference");
            entity.Property(e => e.RefundError).HasMaxLength(500).HasColumnName("refund_error");

            entity.HasOne(d => d.Ticket).WithMany()
                .HasForeignKey(d => d.TicketId)
                .HasConstraintName("fk_ticket_return_requests_ticket");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
