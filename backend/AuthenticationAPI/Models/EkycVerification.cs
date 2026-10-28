using System;
using System.Collections.Generic;

namespace AuthenticationAPI.Models;

public partial class EkycVerification
{
    public int EkycId { get; set; }

    public int UserId { get; set; }

    public string? CccdNumberEncrypted { get; set; }

    public string? CccdFrontObjectKey { get; set; }

    public string? CccdBackObjectKey { get; set; }

    public string? FaceCaptureObjectKey { get; set; }

    public byte[]? FaceVector { get; set; }

    public string? OcrRawData { get; set; }

    public decimal? FaceMatchScore { get; set; }

    public decimal? LivenessScore { get; set; }

    public bool? LivenessPassed { get; set; }

    public string Status { get; set; } = null!;

    public string? FailReason { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? CccdNumberHash { get; set; }

    public string? ConsentVersion { get; set; }

    public DateTime? ConsentedAt { get; set; }

    public DateTime? DataPurgedAt { get; set; }

    public bool LegalHold { get; set; }

    public string? LegalHoldReason { get; set; }

    public DateTime? LegalHoldAt { get; set; }

    public int? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
