using EventAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace EventAPI.Data
{
    public class EventDbContext : DbContext
    {
        public EventDbContext(DbContextOptions<EventDbContext> options)
            : base(options)
        {
        }

        public virtual DbSet<Event> Events { get; set; } = null!;
        public virtual DbSet<EventImage> EventImages { get; set; } = null!;
        public virtual DbSet<TicketType> TicketTypes { get; set; } = null!;
        public virtual DbSet<PricingRule> PricingRules { get; set; } = null!;
        public virtual DbSet<RefundPolicy> RefundPolicies { get; set; } = null!;
        public virtual DbSet<SeatMap> SeatMaps { get; set; } = null!;
        public virtual DbSet<SeatZone> SeatZones { get; set; } = null!;
        public virtual DbSet<Seat> Seats { get; set; } = null!;
        public virtual DbSet<Wishlist> Wishlists { get; set; } = null!;
        public virtual DbSet<Review> Reviews { get; set; } = null!;
        public virtual DbSet<SeatingTemplate> SeatingTemplates { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Events
            modelBuilder.Entity<Event>(entity =>
            {
                entity.ToTable("events");
                entity.HasKey(e => e.EventId).HasName("events_pkey");

                entity.Property(e => e.EventId).HasColumnName("event_id").UseIdentityAlwaysColumn();
                entity.Property(e => e.OrganizerId).HasColumnName("organizer_id");
                entity.Property(e => e.CategoryId).HasColumnName("category_id").HasDefaultValue(1);
                entity.Property(e => e.Title).HasColumnName("title").HasMaxLength(200);
                entity.Property(e => e.Slug).HasColumnName("slug").HasMaxLength(250);
                entity.HasIndex(e => e.Slug).IsUnique().HasDatabaseName("uq_events_slug");
                entity.HasIndex(e => e.OrganizerId).HasDatabaseName("ix_events_organizer_id");

                entity.Property(e => e.ShortDescription).HasColumnName("short_description").HasMaxLength(500);
                entity.Property(e => e.Description).HasColumnName("description");
                entity.Property(e => e.PosterUrl)
                    .HasColumnName("poster_url")
                    .HasMaxLength(500);

                entity.Property(e => e.PosterPublicId)
                    .HasColumnName("poster_public_id")
                    .HasMaxLength(500);

                entity.Property(e => e.BannerUrl)
                    .HasColumnName("banner_url")
                    .HasMaxLength(500);

                entity.Property(e => e.BannerPublicId)
                    .HasColumnName("banner_public_id")
                    .HasMaxLength(500);
                entity.Property(e => e.LocationName).HasColumnName("location_name").HasMaxLength(200);
                entity.Property(e => e.Address).HasColumnName("address").HasMaxLength(300);
                entity.Property(e => e.City).HasColumnName("city").HasMaxLength(100);
                entity.Property(e => e.Longitude).HasColumnName("longitude").HasPrecision(11, 8);
                entity.Property(e => e.Latitude).HasColumnName("latitude").HasPrecision(10, 8);
                entity.Property(e => e.StartsAt).HasColumnName("starts_at");
                entity.Property(e => e.EndsAt).HasColumnName("ends_at");
                entity.Property(e => e.Timezone).HasColumnName("timezone").HasMaxLength(50).HasDefaultValue("SE Asia Standard Time");
                entity.Property(e => e.HasSeatingChart).HasColumnName("has_seating_chart").HasDefaultValue(false);
                entity.Property(e => e.SeatingMode).HasColumnName("seating_mode").HasMaxLength(30).HasDefaultValue("ReservedSeating");
                entity.Property(e => e.RequiresVirtualQueue).HasColumnName("requires_virtual_queue").HasDefaultValue(false);
                entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("Draft");
                entity.Property(e => e.RejectedReason).HasColumnName("rejected_reason").HasMaxLength(500);
                entity.Property(e => e.SubmittedAt)
                    .HasColumnName("submitted_at")
                    .HasColumnType("timestamp with time zone");

                entity.Property(e => e.ApprovedAt)
                    .HasColumnName("approved_at")
                    .HasColumnType("timestamp with time zone");

                entity.Property(e => e.RejectedAt)
                    .HasColumnName("rejected_at")
                    .HasColumnType("timestamp with time zone");
                entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");
                entity.HasIndex(e => e.Status).HasDatabaseName("ix_events_status");
                entity.Property(e => e.IsFeatured).HasColumnName("is_featured").HasDefaultValue(false);
                entity.Property(e => e.ViewCount).HasColumnName("view_count").HasDefaultValue(0);
                entity.Property(e => e.TotalTickets).HasColumnName("total_tickets").HasDefaultValue(0);
                entity.Property(e => e.SoldTickets).HasColumnName("sold_tickets").HasDefaultValue(0);
                entity.Property(e => e.MinTicketsPerAccount).HasColumnName("min_tickets_per_account");
                entity.Property(e => e.MaxTicketsPerAccount).HasColumnName("max_tickets_per_account");
                entity.Property(e => e.MetaTitle).HasColumnName("meta_title").HasMaxLength(200);
                entity.Property(e => e.MetaDescription).HasColumnName("meta_description").HasMaxLength(500);
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
                entity.Property(e => e.PublishedAt).HasColumnName("published_at");
                entity.Property(e => e.CreatedBy).HasColumnName("created_by");
                entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
                entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
                entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
                entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            });

            // EventImages
            modelBuilder.Entity<EventImage>(entity =>
            {
                entity.ToTable("event_images");
                entity.HasKey(e => e.ImageId).HasName("event_images_pkey");

                entity.Property(e => e.ImageId).HasColumnName("image_id").UseIdentityAlwaysColumn();
                entity.Property(e => e.EventId).HasColumnName("event_id");
                entity.Property(e => e.ImageUrl).HasColumnName("image_url").HasMaxLength(500);
                entity.Property(e => e.PublicId)
                    .HasColumnName("public_id")
                    .HasMaxLength(500);
                entity.Property(e => e.SortOrder).HasColumnName("sort_order").HasDefaultValue(0);
                entity.Property(e => e.IsMain).HasColumnName("is_main").HasDefaultValue(false);
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
                entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
                entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
                entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");

                entity.HasOne(d => d.Event)
                    .WithMany(p => p.EventImages)
                    .HasForeignKey(d => d.EventId)
                    .HasConstraintName("fk_event_images_event");
            });

            // TicketTypes
            modelBuilder.Entity<TicketType>(entity =>
            {
                entity.ToTable("ticket_types");
                entity.HasKey(e => e.TicketTypeId).HasName("ticket_types_pkey");

                entity.Property(e => e.TicketTypeId).HasColumnName("ticket_type_id").UseIdentityAlwaysColumn();
                entity.Property(e => e.EventId).HasColumnName("event_id");
                entity.Property(e => e.TypeName).HasColumnName("type_name").HasMaxLength(100);
                entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(500);
                entity.Property(e => e.Price).HasColumnName("price");
                entity.Property(e => e.OriginalPrice).HasColumnName("original_price");
                entity.Property(e => e.Quantity).HasColumnName("quantity");
                entity.Property(e => e.SoldQuantity).HasColumnName("sold_quantity").HasDefaultValue(0);
                entity.Property(e => e.MinPerOrder).HasColumnName("min_per_order").HasDefaultValue(1);
                entity.Property(e => e.MaxPerOrder).HasColumnName("max_per_order").HasDefaultValue(10);
                entity.Property(e => e.ColorCode).HasColumnName("color_code").HasMaxLength(20);
                entity.Property(e => e.SortOrder).HasColumnName("sort_order").HasDefaultValue(0);
                entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("Active");
                entity.Property(e => e.SalesStartsAt).HasColumnName("sales_starts_at");
                entity.Property(e => e.SalesEndsAt).HasColumnName("sales_ends_at");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
                entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
                entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
                entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");

                entity.HasOne(d => d.Event)
                    .WithMany(p => p.TicketTypes)
                    .HasForeignKey(d => d.EventId)
                    .HasConstraintName("fk_ticket_types_event");

                entity.HasIndex(e => new { e.EventId, e.TypeName })
                    .IsUnique()
                    .HasFilter("is_deleted = false")
                    .HasDatabaseName("uq_ticket_types_event_name_active");

                // DB-level inventory invariant (council feedback: "một ghế/一 vé không thể
                // bán vượt quota; DB phải có transaction/invariant/constraint, không chỉ
                // dựa vào Redis lock"). No app code can ever persist an oversold row, even
                // if a future service writes SoldQuantity directly instead of going through
                // TryReserveAsync below.
                entity.ToTable(t => t.HasCheckConstraint(
                    "ck_ticket_types_sold_within_quantity",
                    "sold_quantity >= 0 AND sold_quantity <= quantity"));
            });

            // PricingRules
            modelBuilder.Entity<PricingRule>(entity =>
            {
                entity.ToTable("pricing_rules");
                entity.HasKey(e => e.PricingRuleId).HasName("pricing_rules_pkey");

                entity.Property(e => e.PricingRuleId).HasColumnName("pricing_rule_id").UseIdentityAlwaysColumn();
                entity.Property(e => e.TicketTypeId).HasColumnName("ticket_type_id");
                entity.Property(e => e.RuleName).HasColumnName("rule_name").HasMaxLength(100);
                entity.Property(e => e.RuleType).HasColumnName("rule_type").HasMaxLength(30);
                entity.Property(e => e.AdjustedPrice).HasColumnName("adjusted_price");
                entity.Property(e => e.DiscountPercent).HasColumnName("discount_percent").HasPrecision(5, 2);
                entity.Property(e => e.TriggerFrom).HasColumnName("trigger_from");
                entity.Property(e => e.TriggerTo).HasColumnName("trigger_to");
                entity.Property(e => e.QuantityThreshold).HasColumnName("quantity_threshold");
                entity.Property(e => e.Priority).HasColumnName("priority").HasDefaultValue(0);
                entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

                entity.HasOne(d => d.TicketType)
                    .WithMany(p => p.PricingRules)
                    .HasForeignKey(d => d.TicketTypeId)
                    .HasConstraintName("fk_pricing_rules_ticket_type");
            });

            // RefundPolicies
            modelBuilder.Entity<RefundPolicy>(entity =>
            {
                entity.ToTable("refund_policies");
                entity.HasKey(e => e.RefundPolicyId).HasName("refund_policies_pkey");

                entity.Property(e => e.RefundPolicyId).HasColumnName("refund_policy_id").UseIdentityAlwaysColumn();
                entity.Property(e => e.EventId).HasColumnName("event_id");
                entity.Property(e => e.PolicyName).HasColumnName("policy_name").HasMaxLength(150);
                entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(1000);
                entity.Property(e => e.DeadlineBeforeEventHours).HasColumnName("deadline_before_event_hours");
                entity.Property(e => e.RefundPercent).HasColumnName("refund_percent").HasPrecision(5, 2);
                entity.Property(e => e.RequiresOrganizerApproval).HasColumnName("requires_organizer_approval").HasDefaultValue(true);
                entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

                entity.HasOne(d => d.Event)
                    .WithMany(p => p.RefundPolicies)
                    .HasForeignKey(d => d.EventId)
                    .HasConstraintName("fk_refund_policies_event");
            });

            // SeatMaps
            modelBuilder.Entity<SeatMap>(entity =>
            {
                entity.ToTable("seat_maps");
                entity.HasKey(e => e.SeatMapId).HasName("seat_maps_pkey");

                entity.Property(e => e.SeatMapId).HasColumnName("seat_map_id").UseIdentityAlwaysColumn();
                entity.Property(e => e.EventId).HasColumnName("event_id");
                entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(150);
                entity.Property(e => e.LayoutJson).HasColumnName("layout_json").HasColumnType("jsonb");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
                entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
                entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
                entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");

                entity.HasOne(d => d.Event)
                    .WithMany(p => p.SeatMaps)
                    .HasForeignKey(d => d.EventId)
                    .HasConstraintName("fk_seat_maps_event");
            });

            // SeatZones
            modelBuilder.Entity<SeatZone>(entity =>
            {
                entity.ToTable("seat_zones");
                entity.HasKey(e => e.SeatZoneId).HasName("seat_zones_pkey");

                entity.Property(e => e.SeatZoneId).HasColumnName("seat_zone_id").UseIdentityAlwaysColumn();
                entity.Property(e => e.SeatMapId).HasColumnName("seat_map_id");
                entity.Property(e => e.TicketTypeId).HasColumnName("ticket_type_id");
                entity.Property(e => e.ZoneName).HasColumnName("zone_name").HasMaxLength(100);
                entity.Property(e => e.ShapeJson).HasColumnName("shape_json").HasColumnType("jsonb");
                entity.Property(e => e.ZoneType).HasColumnName("zone_type").HasMaxLength(20).HasDefaultValue("Seated");
                entity.Property(e => e.Capacity).HasColumnName("capacity").HasDefaultValue(0);

                entity.HasOne(d => d.SeatMap)
                    .WithMany(p => p.SeatZones)
                    .HasForeignKey(d => d.SeatMapId)
                    .HasConstraintName("fk_seat_zones_seat_map");

                entity.HasOne(d => d.TicketType)
                    .WithMany(p => p.SeatZones)
                    .HasForeignKey(d => d.TicketTypeId)
                    .HasConstraintName("fk_seat_zones_ticket_type");
            });

            // Seats
            modelBuilder.Entity<Seat>(entity =>
            {
                entity.ToTable("seats");
                entity.HasKey(e => e.SeatId).HasName("seats_pkey");

                entity.Property(e => e.SeatId).HasColumnName("seat_id").UseIdentityAlwaysColumn();
                entity.Property(e => e.SeatZoneId).HasColumnName("seat_zone_id");
                entity.Property(e => e.RowLabel).HasColumnName("row_label").HasMaxLength(10);
                entity.Property(e => e.SeatNumber).HasColumnName("seat_number").HasMaxLength(10);
                entity.Property(e => e.XCoordinate).HasColumnName("x_coordinate");
                entity.Property(e => e.YCoordinate).HasColumnName("y_coordinate");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

                entity.HasOne(d => d.SeatZone)
                    .WithMany(p => p.Seats)
                    .HasForeignKey(d => d.SeatZoneId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Wishlists
            modelBuilder.Entity<Wishlist>(entity =>
            {
                entity.ToTable("wishlists");
                entity.HasKey(e => e.WishlistId).HasName("wishlists_pkey");

                entity.Property(e => e.WishlistId).HasColumnName("wishlist_id").UseIdentityAlwaysColumn();
                entity.Property(e => e.EventId).HasColumnName("event_id");
                entity.Property(e => e.UserId).HasColumnName("user_id");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
                entity.HasIndex(e => new { e.UserId, e.EventId }).IsUnique().HasDatabaseName("uq_wishlists_user_event");

                entity.HasOne(d => d.Event)
                    .WithMany(p => p.Wishlists)
                    .HasForeignKey(d => d.EventId)
                    .HasConstraintName("fk_wishlists_event");
            });

            // Reviews
            modelBuilder.Entity<Review>(entity =>
            {
                entity.ToTable("reviews");
                entity.HasKey(e => e.ReviewId).HasName("reviews_pkey");

                entity.Property(e => e.ReviewId).HasColumnName("review_id").UseIdentityAlwaysColumn();
                entity.Property(e => e.EventId).HasColumnName("event_id");
                entity.Property(e => e.UserId).HasColumnName("user_id");
                entity.Property(e => e.Rating).HasColumnName("rating");
                entity.Property(e => e.Comment).HasColumnName("comment").HasMaxLength(1000);
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
                entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

                entity.HasOne(d => d.Event)
                    .WithMany(p => p.Reviews)
                    .HasForeignKey(d => d.EventId);
            });

            // SeatingTemplates
            modelBuilder.Entity<SeatingTemplate>(entity =>
            {
                entity.ToTable("seating_templates");
                entity.HasKey(e => e.SeatingTemplateId).HasName("seating_templates_pkey");

                entity.Property(e => e.SeatingTemplateId).HasColumnName("seating_template_id").UseIdentityAlwaysColumn();
                entity.Property(e => e.OrganizerId).HasColumnName("organizer_id");
                entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(150);
                entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(500);
                entity.Property(e => e.SeatingMode).HasColumnName("seating_mode").HasMaxLength(30).HasDefaultValue("ReservedSeating");
                entity.Property(e => e.IsPublic).HasColumnName("is_public").HasDefaultValue(false);
                entity.Property(e => e.LayoutJson).HasColumnName("layout_json").HasColumnType("jsonb");
                entity.Property(e => e.ZonesJson).HasColumnName("zones_json").HasColumnType("jsonb");
                entity.Property(e => e.CreatedBy).HasColumnName("created_by");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
                entity.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
                entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");

                entity.HasIndex(e => e.OrganizerId).HasDatabaseName("ix_seating_templates_organizer_id");
                entity.HasIndex(e => e.IsPublic).HasDatabaseName("ix_seating_templates_is_public");
            });
        }
    }
}
