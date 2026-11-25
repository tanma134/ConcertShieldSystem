using CheckinAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace CheckinAPI.Data
{
    public class CheckinDbContext : DbContext
    {
        public CheckinDbContext(DbContextOptions<CheckinDbContext> options)
            : base(options)
        {
        }

        public virtual DbSet<Ticket> Tickets { get; set; } = null!;
        public virtual DbSet<TicketQrToken> TicketQrTokens { get; set; } = null!;
        public virtual DbSet<Order> Orders { get; set; } = null!;
        public virtual DbSet<OrderAttendee> OrderAttendees { get; set; } = null!;
        public virtual DbSet<CheckinLog> CheckinLogs { get; set; } = null!;
        public virtual DbSet<OfflineSyncConflict> OfflineSyncConflicts { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Ticket>(entity =>
            {
                entity.ToTable("tickets");
                entity.HasKey(e => e.TicketId).HasName("tickets_pkey");

                entity.Property(e => e.TicketId).HasColumnName("ticket_id").ValueGeneratedOnAdd();
                entity.Property(e => e.TicketCode).HasColumnName("ticket_code");
                entity.Property(e => e.OrderId).HasColumnName("order_id");
                entity.Property(e => e.EventId).HasColumnName("event_id");
                entity.Property(e => e.TicketTypeId).HasColumnName("ticket_type_id");
                entity.Property(e => e.SeatId).HasColumnName("seat_id");
                entity.Property(e => e.OwnerUserId).HasColumnName("owner_user_id");
                entity.Property(e => e.TicketTypeName).HasMaxLength(100).HasColumnName("ticket_type_name");
                entity.Property(e => e.OwnerName).HasMaxLength(100).HasColumnName("owner_name");
                entity.Property(e => e.Status).HasMaxLength(20).HasColumnName("status");
                entity.Property(e => e.CheckedInAt).HasColumnName("checked_in_at");
                entity.Property(e => e.CheckedInBy).HasColumnName("checked_in_by");
                entity.Property(e => e.CheckedInGate).HasMaxLength(100).HasColumnName("checked_in_gate");
                entity.Property(e => e.LastExitAt).HasColumnName("last_exit_at");
                entity.Property(e => e.IsInside).HasColumnName("is_inside").HasDefaultValue(false);
                entity.Property(e => e.ReEntryCount).HasColumnName("re_entry_count").HasDefaultValue(0);
                entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
                entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
                entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
                entity.Property(e => e.IsCheckedIn).HasColumnName("is_checked_in").HasDefaultValue(false);
                entity.Property(e => e.GateId).HasMaxLength(50).HasColumnName("gate_id");

                entity.HasOne(e => e.Order)
                    .WithMany(o => o.Tickets)
                    .HasForeignKey(e => e.OrderId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<TicketQrToken>(entity =>
            {
                entity.ToTable("ticket_qr_tokens");
                entity.HasKey(e => e.TicketQrTokenId).HasName("ticket_qr_tokens_pkey");

                entity.Property(e => e.TicketQrTokenId).HasColumnName("ticket_qr_token_id").ValueGeneratedOnAdd();
                entity.Property(e => e.TicketId).HasColumnName("ticket_id");
                entity.Property(e => e.QrToken).HasMaxLength(200).HasColumnName("qr_token");
                entity.Property(e => e.IssuedAt).HasColumnName("issued_at");
                entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
                entity.Property(e => e.IsRevoked).HasColumnName("is_revoked").HasDefaultValue(false);
                entity.Property(e => e.RevokedAt).HasColumnName("revoked_at");
                entity.Property(e => e.IsUsed).HasColumnName("is_used").HasDefaultValue(false);
                entity.Property(e => e.UsedAt).HasColumnName("used_at");

                entity.HasOne(e => e.Ticket)
                    .WithMany(t => t.TicketQrTokens)
                    .HasForeignKey(e => e.TicketId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Order>(entity =>
            {
                entity.ToTable("orders");
                entity.HasKey(e => e.OrderId).HasName("orders_pkey");

                entity.Property(e => e.OrderId).HasColumnName("order_id").ValueGeneratedOnAdd();
                entity.Property(e => e.CustomerId).HasColumnName("customer_id");
                entity.Property(e => e.EventId).HasColumnName("event_id");
                entity.Property(e => e.FullName).HasMaxLength(255).HasColumnName("full_name");
                entity.Property(e => e.Email).HasMaxLength(255).HasColumnName("email");
                entity.Property(e => e.Phone).HasMaxLength(50).HasColumnName("phone");
                entity.Property(e => e.Status).HasMaxLength(20).HasColumnName("status");
                entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
            });

            modelBuilder.Entity<CheckinLog>(entity =>
            {
                entity.ToTable("checkin_logs");
                entity.HasKey(e => e.LogId).HasName("checkin_logs_pkey");

                entity.Property(e => e.LogId).HasColumnName("log_id").ValueGeneratedOnAdd();
                entity.Property(e => e.TicketCode).HasMaxLength(100).HasColumnName("ticket_code");
                entity.Property(e => e.CustomerName).HasMaxLength(100).HasColumnName("customer_name");
                entity.Property(e => e.ZoneName).HasMaxLength(100).HasColumnName("zone_name");
                entity.Property(e => e.GateId).HasMaxLength(50).HasColumnName("gate_id");
                entity.Property(e => e.GateName).HasMaxLength(100).HasColumnName("gate_name");
                entity.Property(e => e.ScanMethod).HasMaxLength(50).HasColumnName("scan_method");
                entity.Property(e => e.Status).HasMaxLength(50).HasColumnName("status");
                entity.Property(e => e.StaffUserId).HasColumnName("staff_user_id");
                entity.Property(e => e.EventId).HasColumnName("event_id");
                entity.Property(e => e.ScannedAt).HasColumnName("scanned_at");
                entity.Property(e => e.ScanTime).HasColumnName("scan_time");
            });

            modelBuilder.Entity<OfflineSyncConflict>(entity =>
            {
                entity.ToTable("offline_sync_conflicts");
                entity.HasKey(e => e.ConflictId).HasName("offline_sync_conflicts_pkey");

                entity.Property(e => e.ConflictId).HasMaxLength(100).HasColumnName("conflict_id");
                entity.Property(e => e.EventId).HasColumnName("event_id");
                entity.Property(e => e.TicketCode).HasMaxLength(100).HasColumnName("ticket_code");
                entity.Property(e => e.AttendeeName).HasMaxLength(100).HasColumnName("attendee_name");
                entity.Property(e => e.ZoneName).HasMaxLength(100).HasColumnName("zone_name");
                entity.Property(e => e.SeatNumber).HasMaxLength(100).HasColumnName("seat_number");
                entity.Property(e => e.EarlierScannedAt).HasColumnName("earlier_scanned_at");
                entity.Property(e => e.EarlierGateId).HasMaxLength(50).HasColumnName("earlier_gate_id");
                entity.Property(e => e.EarlierGateName).HasMaxLength(100).HasColumnName("earlier_gate_name");
                entity.Property(e => e.EarlierStaffName).HasMaxLength(100).HasColumnName("earlier_staff_name");
                entity.Property(e => e.EarlierMethod).HasMaxLength(50).HasColumnName("earlier_method");
                entity.Property(e => e.DuplicateScannedAt).HasColumnName("duplicate_scanned_at");
                entity.Property(e => e.DuplicateGateId).HasMaxLength(50).HasColumnName("duplicate_gate_id");
                entity.Property(e => e.DuplicateGateName).HasMaxLength(100).HasColumnName("duplicate_gate_name");
                entity.Property(e => e.DuplicateStaffName).HasMaxLength(100).HasColumnName("duplicate_staff_name");
                entity.Property(e => e.DuplicateMethod).HasMaxLength(50).HasColumnName("duplicate_method");
                entity.Property(e => e.Status).HasMaxLength(50).HasColumnName("status");
                entity.Property(e => e.ResolutionAction).HasMaxLength(50).HasColumnName("resolution_action");
                entity.Property(e => e.ResolutionNotes).HasColumnName("resolution_notes");
                entity.Property(e => e.ResolvedByUserId).HasColumnName("resolved_by_user_id");
                entity.Property(e => e.ResolvedByName).HasMaxLength(100).HasColumnName("resolved_by_name");
                entity.Property(e => e.ResolvedAt).HasColumnName("resolved_at");
            });

            modelBuilder.Entity<OrderAttendee>(entity =>
            {
                entity.ToTable("order_attendees");
                entity.HasKey(e => e.AttendeeId).HasName("order_attendees_pkey");
                entity.Property(e => e.AttendeeId).HasColumnName("attendee_id").ValueGeneratedOnAdd();
                entity.Property(e => e.OrderId).HasColumnName("order_id");
                entity.Property(e => e.TicketTypeId).HasColumnName("ticket_type_id");
                entity.Property(e => e.FullName).HasColumnName("full_name").HasMaxLength(150);
                entity.Property(e => e.CitizenId).HasColumnName("citizen_id").HasMaxLength(50);
                entity.Property(e => e.Phone).HasColumnName("phone").HasMaxLength(20);
                entity.Property(e => e.IsPrimaryBuyer).HasColumnName("is_primary_buyer").HasDefaultValue(false);
                entity.Property(e => e.SeatId).HasColumnName("seat_id");

                entity.HasOne(e => e.Order)
                    .WithMany(o => o.OrderAttendees)
                    .HasForeignKey(e => e.OrderId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
