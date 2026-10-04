using Microsoft.EntityFrameworkCore;
using NotificationAPI.Models;

namespace NotificationAPI.Data
{
    public class NotificationDbContext : DbContext
    {
        public NotificationDbContext(DbContextOptions<NotificationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Notification> Notifications { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Notification>(entity =>
            {
                entity.ToTable("notifications");

                entity.HasKey(e => e.NotificationId);

                entity.Property(e => e.NotificationId).HasColumnName("notification_id");
                entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
                entity.Property(e => e.Title).HasColumnName("title").HasMaxLength(255).IsRequired();
                entity.Property(e => e.Message).HasColumnName("message").IsRequired();
                entity.Property(e => e.Category).HasColumnName("category").HasMaxLength(50).HasDefaultValue("event_new");
                entity.Property(e => e.TargetUrl).HasColumnName("target_url").HasMaxLength(500);
                entity.Property(e => e.IsRead).HasColumnName("is_read").HasDefaultValue(false);
                entity.Property(e => e.ReadAt).HasColumnName("read_at");
                entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.HasIndex(e => new { e.UserId, e.IsDeleted, e.CreatedAt });
            });
        }
    }
}
