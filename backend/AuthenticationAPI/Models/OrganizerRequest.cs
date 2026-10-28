using System;
using System.Collections.Generic;

namespace AuthenticationAPI.Models;

public partial class OrganizerRequest
{
    public int RequestId { get; set; }

    public int UserId { get; set; }

    public string Reason { get; set; } = null!;

    public string? CompanyName { get; set; }

    public string? Website { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Experience { get; set; }

    public string Status { get; set; } = null!;

    public int? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewNote { get; set; }

    public string? RejectionReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public int? DeletedBy { get; set; }

    public virtual User? DeletedByNavigation { get; set; }

    public virtual User? ReviewedByNavigation { get; set; }

    public virtual User User { get; set; } = null!;
}
