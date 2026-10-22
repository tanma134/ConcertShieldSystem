using System.ComponentModel.DataAnnotations;

namespace AdminAPI.DTOs;

// Request DTO for creating a customer support case
public class CreateSupportCaseRequest
{
 public int? CustomerId { get; set; }

 [Required(ErrorMessage = "Customer name is required")]
 [MaxLength(150)]
 public string CustomerName { get; set; } = string.Empty;

 [Required(ErrorMessage = "Customer email is required")]
 [EmailAddress(ErrorMessage = "Invalid email format")]
 [MaxLength(150)]
 public string CustomerEmail { get; set; } = string.Empty;

 [MaxLength(50)]
 public string? CustomerPhone { get; set; }

 [Required(ErrorMessage = "Case title is required")]
 [MaxLength(255)]
 public string Title { get; set; } = string.Empty;

 [Required(ErrorMessage = "Description is required")]
 public string Description { get; set; } = string.Empty;

 public string Category { get; set; } = "General Inquiry";

 public string Priority { get; set; } = "Medium";

 // Transaction context references 
 public int? OrderId { get; set; }
 public int? TicketId { get; set; }
 public string? PaymentReference { get; set; }
}

// DTO for listing support cases
public class SupportCaseListDto
{
 public int CaseId { get; set; }
 public string CaseNumber { get; set; } = string.Empty;
 public string Title { get; set; } = string.Empty;
 public string CustomerName { get; set; } = string.Empty;
 public string CustomerEmail { get; set; } = string.Empty;
 public string Category { get; set; } = string.Empty;
 public string Priority { get; set; } = string.Empty;
 public string Status { get; set; } = string.Empty;
 public int? AssignedStaffId { get; set; }
 public string? AssignedStaffName { get; set; }
 public DateTime CreatedAt { get; set; }
 public DateTime UpdatedAt { get; set; }
}

// DTO for detailed support case view with notes history
public class SupportCaseDetailDto
{
 public int CaseId { get; set; }
 public string CaseNumber { get; set; } = string.Empty;
 public int? CustomerId { get; set; }
 public string CustomerName { get; set; } = string.Empty;
 public string CustomerEmail { get; set; } = string.Empty;
 public string? CustomerPhone { get; set; }
 public string Title { get; set; } = string.Empty;
 public string Description { get; set; } = string.Empty;
 public string Category { get; set; } = string.Empty;
 public string Priority { get; set; } = string.Empty;
 public string Status { get; set; } = string.Empty;

 public int? OrderId { get; set; }
 public int? TicketId { get; set; }
 public string? PaymentReference { get; set; }

 public int? AssignedStaffId { get; set; }
 public string? AssignedStaffName { get; set; }
 public DateTime? AssignedAt { get; set; }
 public int? AssignedBy { get; set; }

 public string? Resolution { get; set; }
 public int? ResolvedBy { get; set; }
 public string? ResolvedByName { get; set; }
 public DateTime? ResolvedAt { get; set; }

 public int CreatedBy { get; set; }
 public string CreatedByName { get; set; } = string.Empty;
 public DateTime CreatedAt { get; set; }
 public DateTime UpdatedAt { get; set; }

 public List<SupportCaseNoteDto> Notes { get; set; } = new();
}

// DTO for timeline notes and activity entries
public class SupportCaseNoteDto
{
 public int NoteId { get; set; }
 public int CaseId { get; set; }
 public int AuthorId { get; set; }
 public string AuthorName { get; set; } = string.Empty;
 public string AuthorRole { get; set; } = string.Empty;
 public string NoteType { get; set; } = string.Empty;
 public string Content { get; set; } = string.Empty;
 public DateTime CreatedAt { get; set; }
}

// Request DTO for appending an internal note
public class AddSupportCaseNoteRequest
{
 [Required(ErrorMessage = "Note content is required")]
 public string Content { get; set; } = string.Empty;

 public string NoteType { get; set; } = "InternalNote";
}

// Request DTO for assigning a support case to staff
public class AssignStaffRequest
{
 [Required(ErrorMessage = "Staff user ID is required")]
 public int StaffUserId { get; set; }

 [Required(ErrorMessage = "Staff name is required")]
 public string StaffName { get; set; } = string.Empty;

 public string? StaffEmail { get; set; }
}

// Request DTO for resolving and closing a support case
public class ResolveCaseRequest
{
 [Required(ErrorMessage = "Resolution details are required when closing a support case ")]
 public string Resolution { get; set; } = string.Empty;
}

// Query parameters DTO for filtering support cases
public class SupportCaseQuery
{
 public string? Status { get; set; }
 public string? Priority { get; set; }
 public int? AssignedStaffId { get; set; }
 public string? SearchTerm { get; set; }
 public int Page { get; set; } = 1;
 public int PageSize { get; set; } = 20;
}
