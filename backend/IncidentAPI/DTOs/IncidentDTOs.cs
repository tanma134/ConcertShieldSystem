using System.ComponentModel.DataAnnotations;

namespace IncidentAPI.DTOs;

// Request DTO cho UC_16.1 Report Incident
public class CreateIncidentRequest
{
    [Required]
    public int EventId { get; set; }

    public string? EventName { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 3)]
    public string Title { get; set; } = null!;

    [Required]
    public string Category { get; set; } = "Security";

    [Required]
    public string Severity { get; set; } = "Medium";

    [Required]
    [StringLength(255)]
    public string Location { get; set; } = null!;

    [Required]
    public string Description { get; set; } = null!;
}

// Request DTO cho UC_16.3 Update Incident
public class UpdateIncidentRequest
{
    public string? Title { get; set; }
    public string? Category { get; set; }
    public string? Severity { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
    public string? UpdateNote { get; set; }
}

// Request DTO cho quick status patch
public class UpdateStatusRequest
{
    [Required]
    public string Status { get; set; } = null!;
    public string? Note { get; set; }
}

// Request DTO cho UC_16.4 Escalate Incident
public class EscalateIncidentRequest
{
    [Required]
    public string Reason { get; set; } = null!;
    public int? AdminId { get; set; }
}

// Request DTO cho UC_16.5 Resolve Incident
public class ResolveIncidentRequest
{
    [Required]
    public string ResolutionSummary { get; set; } = null!;

    [Required]
    public string ActionTaken { get; set; } = null!;
}

// DTO Response tóm tắt trong danh sách
public class IncidentListItemDto
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public string? EventName { get; set; }
    public int ReporterId { get; set; }
    public string ReporterName { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Category { get; set; } = null!;
    public string Severity { get; set; } = null!;
    public string Location { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Status { get; set; } = null!;
    public bool IsEscalated { get; set; }
    public DateTime? EscalatedAt { get; set; }
    public int EvidenceCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

// DTO Response chi tiết sự cố
public class IncidentDetailDto
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public string? EventName { get; set; }
    public int ReporterId { get; set; }
    public string ReporterName { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Category { get; set; } = null!;
    public string Severity { get; set; } = null!;
    public string Location { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Status { get; set; } = null!;
    public bool IsEscalated { get; set; }
    public int? EscalatedToAdminId { get; set; }
    public DateTime? EscalatedAt { get; set; }
    public string? EscalationReason { get; set; }
    public string? ResolutionSummary { get; set; }
    public string? ActionTaken { get; set; }
    public int? ResolvedById { get; set; }
    public string? ResolvedByName { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<IncidentEvidenceDto> Evidences { get; set; } = new();
    public List<IncidentUpdateDto> Updates { get; set; } = new();
}

// DTO cho UC_16.2 Bằng chứng
public class IncidentEvidenceDto
{
    public int Id { get; set; }
    public int IncidentId { get; set; }
    public string FileUrl { get; set; } = null!;
    public string PublicId { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public string FileType { get; set; } = null!;
    public long FileSize { get; set; }
    public string MimeType { get; set; } = null!;
    public string? Caption { get; set; }
    public int UploadedById { get; set; }
    public string UploadedByName { get; set; } = null!;
    public DateTime UploadedAt { get; set; }
}

// DTO cho UC_16.3 Lịch sử cập nhật / Timeline
public class IncidentUpdateDto
{
    public int Id { get; set; }
    public int IncidentId { get; set; }
    public int UpdatedById { get; set; }
    public string UpdatedByName { get; set; } = null!;
    public string ActionType { get; set; } = null!;
    public string Note { get; set; } = null!;
    public string? PreviousStatus { get; set; }
    public string? NewStatus { get; set; }
    public DateTime CreatedAt { get; set; }
}

// DTO cho UC_16.5 Kết quả giải quyết
public class ResolutionDto
{
    public int IncidentId { get; set; }
    public string Title { get; set; } = null!;
    public string ResolutionSummary { get; set; } = null!;
    public string ActionTaken { get; set; } = null!;
    public int? ResolvedById { get; set; }
    public string? ResolvedByName { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

// Query parameters cho bộ lọc tìm kiếm
public class IncidentQuery
{
    public int? EventId { get; set; }
    public string? Status { get; set; }
    public string? Severity { get; set; }
    public string? Category { get; set; }
    public bool? IsEscalated { get; set; }
    public string? SearchTerm { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
