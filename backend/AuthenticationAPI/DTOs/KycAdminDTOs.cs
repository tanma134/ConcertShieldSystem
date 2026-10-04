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
        /// <summary>Phải nằm trong [MinRetentionDays, MaxRetentionDays]; không còn giá trị 0.</summary>
        public int RetentionDays { get; set; }
        public bool KeepDocumentHashAfterDeletion { get; set; }
        /// <summary>BR-217: shortest retention allowed (legal minimum).</summary>
        public int MinRetentionDays { get; set; }
        /// <summary>BR-217: longest retention allowed (not longer than necessary).</summary>
        public int MaxRetentionDays { get; set; }
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
        public string? ReasonCode { get; set; }
        public string? Note { get; set; }
        /// <summary>BR-56: deadline = RequestedAt + 72h.</summary>
        public DateTime DueAt { get; set; }
        public bool IsOverdue { get; set; }
    }

    public class RejectKycDeletionDto
    {
        /// <summary>BR-228: LegalRetention | LegalHold | AuthorityRequest.</summary>
        public string ReasonCode { get; set; } = "";
        public string Note { get; set; } = "";
    }

    // ---------- eKYC manual review (BR-50, BR-55, BR-109) ----------
    public class KycReviewItemDto
    {
        public int EkycId { get; set; }
        public int UserId { get; set; }
        public string Status { get; set; } = "";
        public decimal? FaceMatchScore { get; set; }
        public bool? LivenessPassed { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class RejectKycReviewDto
    {
        public string Reason { get; set; } = "";
    }

    public class SetKycLegalHoldDto
    {
        public bool Hold { get; set; }
        public string? Reason { get; set; }
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

    /// <summary>BR-235: what a data subject sees about access to their own eKYC data (no admin id, no IP).</summary>
    public class KycMyAccessLogDto
    {
        public long Id { get; set; }
        /// <summary>User | Admin | System</summary>
        public string ActorType { get; set; } = "";
        public string Action { get; set; } = "";
        public string? Purpose { get; set; }
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