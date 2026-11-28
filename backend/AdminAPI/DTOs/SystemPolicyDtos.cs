namespace AdminAPI.DTOs
{
    public class PolicyVersionDTO
    {
        public int PolicyId { get; set; }
        public int VersionId => PolicyId;
        public string Category { get; set; } = string.Empty;
        public string VersionCode { get; set; } = string.Empty;
        public int VersionNumber { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public string ChangeSummary { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public decimal PlatformFeePercent { get; set; } = 5.0m;
        public int PayoutDays { get; set; } = 7;
        public string Status { get; set; } = "Active"; // Active, Archived
        public DateTime EffectiveDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public int CreatedByAdminId { get; set; }
        public string CreatedByAdminName { get; set; } = "Platform Administrator";
    }

    public class CreatePolicyVersionRequestDTO
    {
        public string Category { get; set; } = "PRICE_TICKETING";
        public string? VersionNumber { get; set; }
        public string? RulesJson { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Scope { get; set; } = "Toàn bộ người dùng & Ban tổ chức";
        public string ChangeSummary { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public decimal? PlatformFeePercent { get; set; } = 5.0m;
        public int? PayoutDays { get; set; } = 7;
        public DateTime? EffectiveDate { get; set; }
    }
}
