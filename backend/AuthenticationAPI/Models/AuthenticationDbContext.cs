using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace AuthenticationAPI.Models;

public partial class AuthenticationDbContext : DbContext
{
    public AuthenticationDbContext(DbContextOptions<AuthenticationDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<EkycVerification> EkycVerifications { get; set; }

    public virtual DbSet<KycAccessLog> KycAccessLogs { get; set; }

    public virtual DbSet<KycConsentVersion> KycConsentVersions { get; set; }

    public virtual DbSet<KycDeletionRequest> KycDeletionRequests { get; set; }

    public virtual DbSet<KycSetting> KycSettings { get; set; }

    public virtual DbSet<OrganizerRequest> OrganizerRequests { get; set; }

    public virtual DbSet<RefreshToken> RefreshTokens { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserRole> UserRoles { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EkycVerification>(entity =>
        {
            entity.HasKey(e => e.EkycId).HasName("ekyc_verifications_pkey");

            entity.ToTable("ekyc_verifications");

            entity.HasIndex(e => e.CccdNumberHash, "ix_ekyc_cccd_number_hash");

            entity.HasIndex(e => e.CreatedAt, "ix_ekyc_purge_candidates").HasFilter("(data_purged_at IS NULL)");

            entity.HasIndex(e => new { e.UserId, e.Status }, "ix_ekyc_user_status");

            entity.HasIndex(e => e.CreatedAt, "ix_ekyc_verifications_purge").HasFilter("(data_purged_at IS NULL)");

            entity.HasIndex(e => e.CccdNumberHash, "ux_ekyc_cccd_hash_active")
                .IsUnique()
                .HasFilter("((cccd_number_hash IS NOT NULL) AND ((status)::text = ANY ((ARRAY['Passed'::character varying, 'ManualReview'::character varying])::text[])))");

            entity.HasIndex(e => e.CccdNumberHash, "ux_ekyc_cccd_hash_passed")
                .IsUnique()
                .HasFilter("((status)::text = 'Passed'::text)");

            entity.Property(e => e.EkycId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("ekyc_id");
            entity.Property(e => e.CccdBackObjectKey)
                .HasMaxLength(500)
                .HasColumnName("cccd_back_object_key");
            entity.Property(e => e.CccdFrontObjectKey)
                .HasMaxLength(500)
                .HasColumnName("cccd_front_object_key");
            entity.Property(e => e.CccdNumberEncrypted).HasColumnName("cccd_number_encrypted");
            entity.Property(e => e.CccdNumberHash)
                .HasMaxLength(64)
                .HasColumnName("cccd_number_hash");
            entity.Property(e => e.ConsentVersion)
                .HasMaxLength(20)
                .HasColumnName("consent_version");
            entity.Property(e => e.ConsentedAt).HasColumnName("consented_at");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.DataPurgedAt).HasColumnName("data_purged_at");
            entity.Property(e => e.FaceCaptureObjectKey)
                .HasMaxLength(500)
                .HasColumnName("face_capture_object_key");
            entity.Property(e => e.FaceMatchScore)
                .HasPrecision(5, 2)
                .HasColumnName("face_match_score");
            entity.Property(e => e.FaceVector).HasColumnName("face_vector");
            entity.Property(e => e.FailReason)
                .HasMaxLength(500)
                .HasColumnName("fail_reason");
            entity.Property(e => e.LegalHold)
                .HasDefaultValue(false)
                .HasColumnName("legal_hold");
            entity.Property(e => e.LegalHoldAt).HasColumnName("legal_hold_at");
            entity.Property(e => e.LegalHoldReason)
                .HasMaxLength(500)
                .HasColumnName("legal_hold_reason");
            entity.Property(e => e.LivenessPassed).HasColumnName("liveness_passed");
            entity.Property(e => e.LivenessScore)
                .HasPrecision(5, 2)
                .HasColumnName("liveness_score");
            entity.Property(e => e.OcrRawData)
                .HasColumnType("jsonb")
                .HasColumnName("ocr_raw_data");
            entity.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");
            entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'Pending'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.VerifiedAt).HasColumnName("verified_at");

            entity.HasOne(d => d.User).WithMany(p => p.EkycVerifications)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_ekyc_user");
        });

        modelBuilder.Entity<KycAccessLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("kyc_access_logs_pkey");

            entity.ToTable("kyc_access_logs");

            entity.HasIndex(e => new { e.ActorUserId, e.CreatedAt }, "ix_kyc_access_logs_actor_created").IsDescending(false, true);

            entity.HasIndex(e => e.CreatedAt, "ix_kyc_access_logs_created").IsDescending();

            entity.HasIndex(e => e.EkycId, "ix_kyc_access_logs_ekyc");

            entity.HasIndex(e => new { e.SubjectUserId, e.CreatedAt }, "ix_kyc_access_logs_subject_created").IsDescending(false, true);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Action)
                .HasMaxLength(50)
                .HasColumnName("action");
            entity.Property(e => e.ActorType)
                .HasMaxLength(20)
                .HasDefaultValueSql("'User'::character varying")
                .HasColumnName("actor_type");
            entity.Property(e => e.ActorUserId).HasColumnName("actor_user_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.Details).HasColumnName("details");
            entity.Property(e => e.EkycId).HasColumnName("ekyc_id");
            entity.Property(e => e.IpAddress)
                .HasMaxLength(45)
                .HasColumnName("ip_address");
            entity.Property(e => e.SubjectUserId).HasColumnName("subject_user_id");
        });

        modelBuilder.Entity<KycConsentVersion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("kyc_consent_versions_pkey");

            entity.ToTable("kyc_consent_versions");

            entity.HasIndex(e => e.Version, "uq_kyc_consent_versions_version").IsUnique();

            entity.HasIndex(e => e.IsActive, "ux_kyc_consent_versions_one_active")
                .IsUnique()
                .HasFilter("is_active");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CheckboxText).HasColumnName("checkbox_text");
            entity.Property(e => e.ContentJson).HasColumnName("content_json");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(false)
                .HasColumnName("is_active");
            entity.Property(e => e.Title)
                .HasMaxLength(500)
                .HasColumnName("title");
            entity.Property(e => e.Version)
                .HasMaxLength(20)
                .HasColumnName("version");
        });

        modelBuilder.Entity<KycDeletionRequest>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("kyc_deletion_requests_pkey");

            entity.ToTable("kyc_deletion_requests");

            entity.HasIndex(e => new { e.Status, e.RequestedAt }, "ix_kyc_deletion_requests_status_requested").IsDescending(false, true);

            entity.HasIndex(e => new { e.UserId, e.Status }, "ix_kyc_deletion_requests_user_status");

            entity.HasIndex(e => e.UserId, "ux_kyc_deletion_one_pending_per_user")
                .IsUnique()
                .HasFilter("((status)::text = 'Pending'::text)");

            entity.HasIndex(e => e.UserId, "ux_kyc_deletion_requests_one_pending")
                .IsUnique()
                .HasFilter("((status)::text = 'Pending'::text)");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.ProcessedAt).HasColumnName("processed_at");
            entity.Property(e => e.ProcessedBy).HasColumnName("processed_by");
            entity.Property(e => e.ReasonCode)
                .HasMaxLength(30)
                .HasColumnName("reason_code");
            entity.Property(e => e.RequestedAt).HasColumnName("requested_at");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'Pending'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.UserId).HasColumnName("user_id");
        });

        modelBuilder.Entity<KycSetting>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("kyc_settings_pkey");

            entity.ToTable("kyc_settings");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.KeepDocumentHashAfterDeletion)
                .HasDefaultValue(false)
                .HasColumnName("keep_document_hash_after_deletion");
            entity.Property(e => e.RetentionDays)
                .HasDefaultValue(30)
                .HasColumnName("retention_days");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        });

        modelBuilder.Entity<OrganizerRequest>(entity =>
        {
            entity.HasKey(e => e.RequestId).HasName("organizer_requests_pkey");

            entity.ToTable("organizer_requests");

            entity.Property(e => e.RequestId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("request_id");
            entity.Property(e => e.CompanyName)
                .HasMaxLength(200)
                .HasColumnName("company_name");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
            entity.Property(e => e.DeletedBy).HasColumnName("deleted_by");
            entity.Property(e => e.Experience)
                .HasMaxLength(2000)
                .HasColumnName("experience");
            entity.Property(e => e.IsDeleted)
                .HasDefaultValue(false)
                .HasColumnName("is_deleted");
            entity.Property(e => e.PhoneNumber)
                .HasMaxLength(20)
                .HasColumnName("phone_number");
            entity.Property(e => e.Reason)
                .HasMaxLength(1000)
                .HasColumnName("reason");
            entity.Property(e => e.RejectionReason)
                .HasMaxLength(1000)
                .HasColumnName("rejection_reason");
            entity.Property(e => e.ReviewNote)
                .HasMaxLength(1000)
                .HasColumnName("review_note");
            entity.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");
            entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'Pending'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Website)
                .HasMaxLength(200)
                .HasColumnName("website");

            entity.HasOne(d => d.DeletedByNavigation).WithMany(p => p.OrganizerRequestDeletedByNavigations)
                .HasForeignKey(d => d.DeletedBy)
                .HasConstraintName("fk_organizer_requests_deleted_by");

            entity.HasOne(d => d.ReviewedByNavigation).WithMany(p => p.OrganizerRequestReviewedByNavigations)
                .HasForeignKey(d => d.ReviewedBy)
                .HasConstraintName("fk_organizer_requests_reviewed_by");

            entity.HasOne(d => d.User).WithMany(p => p.OrganizerRequestUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_organizer_requests_user");
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.RefreshTokenId).HasName("refresh_tokens_pkey");

            entity.ToTable("refresh_tokens");

            entity.HasIndex(e => e.TokenHash, "refresh_tokens_token_hash_key").IsUnique();

            entity.Property(e => e.RefreshTokenId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("refresh_token_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.IsRevoked)
                .HasDefaultValue(false)
                .HasColumnName("is_revoked");
            entity.Property(e => e.TokenHash)
                .HasMaxLength(64)
                .HasColumnName("token_hash");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.RefreshTokens)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_refresh_tokens_user");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("roles_pkey");

            entity.ToTable("roles");

            entity.HasIndex(e => e.RoleName, "roles_role_name_key").IsUnique();

            entity.Property(e => e.RoleId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("role_id");
            entity.Property(e => e.RoleName)
                .HasMaxLength(50)
                .HasColumnName("role_name");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("users_pkey");

            entity.ToTable("users");

            entity.HasIndex(e => e.Email, "users_email_key").IsUnique();

            entity.Property(e => e.UserId)
                .UseIdentityAlwaysColumn()
                .HasColumnName("user_id");
            entity.Property(e => e.AuthProvider)
                .HasMaxLength(20)
                .HasDefaultValueSql("'local'::character varying")
                .HasColumnName("auth_provider");
            entity.Property(e => e.AvatarPublicId).HasColumnName("avatar_public_id");
            entity.Property(e => e.AvatarUrl).HasColumnName("avatar_url");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.EkycStatus)
                .HasMaxLength(20)
                .HasDefaultValueSql("'NotStarted'::character varying")
                .HasColumnName("ekyc_status");
            entity.Property(e => e.Email)
                .HasMaxLength(256)
                .HasColumnName("email");
            entity.Property(e => e.FullName)
                .HasMaxLength(100)
                .HasColumnName("full_name");
            entity.Property(e => e.HasPassword)
                .HasDefaultValue(true)
                .HasColumnName("has_password");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.IsVerified)
                .HasDefaultValue(false)
                .HasColumnName("is_verified");
            entity.Property(e => e.OtpExpiredAt).HasColumnName("otp_expired_at");
            entity.Property(e => e.OtpHash)
                .HasMaxLength(256)
                .HasColumnName("otp_hash");
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash");
            entity.Property(e => e.PhoneNumber)
                .HasMaxLength(10)
                .HasColumnName("phone_number");
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.RoleId }).HasName("user_roles_pkey");

            entity.ToTable("user_roles");

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.AssignedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("assigned_at");

            entity.HasOne(d => d.Role).WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.RoleId)
                .HasConstraintName("fk_user_roles_role");

            entity.HasOne(d => d.User).WithMany(p => p.UserRoles)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_user_roles_user");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
