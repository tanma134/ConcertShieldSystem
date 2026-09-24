namespace AuthenticationAPI.DTOs
{
    // ---------- Chung ----------
    public class KycPagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    // ---------- 3.4 Consent ----------
    public class ConsentItemDto
    {
        public string Heading { get; set; } = "";
        public string Content { get; set; } = "";
    }

    /// <summary>Trả cho app/web ở GET api/kyc/consent (giữ đúng shape cũ).</summary>
    public class KycConsentPublicDto
    {
        public string Version { get; set; } = "";
        public string Title { get; set; } = "";
        public List<ConsentItemDto> Items { get; set; } = new();
        public string CheckboxText { get; set; } = "";
    }

    public class KycConsentVersionDto
    {
        public int Id { get; set; }
        public string Version { get; set; } = "";
        public string Title { get; set; } = "";
        public List<ConsentItemDto> Items { get; set; } = new();
        public string CheckboxText { get; set; } = "";
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? CreatedBy { get; set; }
        /// <summary>Số hồ sơ eKYC đã đồng ý theo phiên bản này.</summary>
        public int ConsentCount { get; set; }
    }

    public class CreateKycConsentVersionDto
    {
        public string Version { get; set; } = "";
        public string Title { get; set; } = "";
        public List<ConsentItemDto> Items { get; set; } = new();
        public string CheckboxText { get; set; } = "";
        /// <summary>true = kích hoạt ngay sau khi tạo.</summary>
        public bool Activate { get; set; }
    }

    // ---------- 3.5 Retention ----------
    public class KycSettingDto
    {
        /// <summary>0 = không tự động xóa.</summary>
        public int RetentionDays { get; set; }
        public bool KeepDocumentHashAfterDeletion { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? UpdatedBy { get; set; }
    }

    public class UpdateKycSettingDto
    {
        public int RetentionDays { get; set; }
        public bool KeepDocumentHashAfterDeletion { get; set; }
    }

    // ---------- 3.6 Deletion ----------
    public class KycDeletionRequestDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Status { get; set; } = "";
        public DateTime RequestedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public int? ProcessedBy { get; set; }
        public string? Note { get; set; }
    }

    public class RejectKycDeletionDto
    {
        public string Note { get; set; } = "";
    }

    // ---------- 3.7 Access log ----------
    public class KycAccessLogDto
    {
        public long Id { get; set; }
        public int SubjectUserId { get; set; }
        public int? EkycId { get; set; }
        public int? ActorUserId { get; set; }
        public string ActorType { get; set; } = "";
        public string Action { get; set; } = "";
        public string? IpAddress { get; set; }
        public string? Details { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class KycAccessLogQuery
    {
        public int? SubjectUserId { get; set; }
        public int? ActorUserId { get; set; }
        /// <summary>User | Admin | System</summary>
        public string? ActorType { get; set; }
        public string? Action { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}