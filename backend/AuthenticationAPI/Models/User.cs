using System;
using System.Collections.Generic;

namespace AuthenticationAPI.Models;

public partial class User
{
    public int UserId { get; set; }

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? PhoneNumber { get; set; }

    public string? AvatarUrl { get; set; }

    public bool IsVerified { get; set; }

    public string? OtpHash { get; set; }

    public DateTime? OtpExpiredAt { get; set; }

    public string EkycStatus { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<EkycVerification> EkycVerifications { get; set; } = new List<EkycVerification>();

    public virtual ICollection<OrganizerRequest> OrganizerRequestDeletedByNavigations { get; set; } = new List<OrganizerRequest>();

    public virtual ICollection<OrganizerRequest> OrganizerRequestReviewedByNavigations { get; set; } = new List<OrganizerRequest>();

    public virtual ICollection<OrganizerRequest> OrganizerRequestUsers { get; set; } = new List<OrganizerRequest>();

    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
