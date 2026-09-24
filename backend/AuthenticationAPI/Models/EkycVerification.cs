using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthenticationAPI.Models;

public partial class EkycVerification
{
    public int EkycId { get; set; }

    public int UserId { get; set; }

    public string? CccdNumberEncrypted { get; set; }

    /// <summary>HMAC-SHA256 (hex, 64 ký tự) của số giấy tờ - dùng để phát hiện 1 CCCD bị dùng cho nhiều tài khoản.</summary>
    [Column("cccd_number_hash")]
    [StringLength(64)]
    public string? CccdNumberHash { get; set; }

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

    /// <summary>Phiên bản nội dung đồng ý mà người dùng đã xác nhận.</summary>
    [Column("consent_version")]
    [StringLength(20)]
    public string? ConsentVersion { get; set; }

    [Column("consented_at")]
    public DateTime? ConsentedAt { get; set; }

    /// <summary>Thời điểm đã xóa ảnh gốc + dữ liệu OCR (job retention). Null = chưa xóa.</summary>
    [Column("data_purged_at")]
    public DateTime? DataPurgedAt { get; set; }

    public virtual User User { get; set; } = null!;
}