using Microsoft.EntityFrameworkCore;
using ReviewAPI.Models;

namespace ReviewAPI.Data;

public sealed class ReviewDbContext(DbContextOptions<ReviewDbContext> options) : DbContext(options)
{
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<ReviewReply> ReviewReplies => Set<ReviewReply>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Review>(entity =>
        {
            entity.ToTable("reviews");
            entity.HasKey(x => x.ReviewId).HasName("reviews_pkey");
            entity.Property(x => x.ReviewId).HasColumnName("review_id").UseIdentityAlwaysColumn();
            entity.Property(x => x.EventId).HasColumnName("event_id");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.Rating).HasColumnName("rating");
            entity.Property(x => x.Comment).HasColumnName("comment").HasMaxLength(1000);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
            entity.Property(x => x.DeletedAt).HasColumnName("deleted_at");
            entity.Property(x => x.DeletedBy).HasColumnName("deleted_by");
            entity.HasIndex(x => new { x.EventId, x.UserId }).IsUnique().HasDatabaseName("uq_reviews_event_user");
            entity.HasIndex(x => x.EventId).HasDatabaseName("ix_reviews_event_id");
        });

        modelBuilder.Entity<ReviewReply>(entity =>
        {
            entity.ToTable("review_replies");
            entity.HasKey(x => x.ReplyId).HasName("review_replies_pkey");
            entity.Property(x => x.ReplyId).HasColumnName("reply_id").UseIdentityAlwaysColumn();
            entity.Property(x => x.ReviewId).HasColumnName("review_id");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.Role).HasColumnName("role").HasMaxLength(30);
            entity.Property(x => x.Comment).HasColumnName("comment").HasMaxLength(1000);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
            entity.Property(x => x.DeletedAt).HasColumnName("deleted_at");
            entity.Property(x => x.DeletedBy).HasColumnName("deleted_by");
            entity.HasOne(x => x.Review).WithMany(x => x.Replies).HasForeignKey(x => x.ReviewId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
