using AdminAPI.Data;
using AdminAPI.DTOs;
using AdminAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace AdminAPI.Services;

// Implementation of Support Case Management and Dispute Resolution
public class SupportCaseService : ISupportCaseService
{
 private readonly AdminDbContext _db;
 private readonly ILogger<SupportCaseService> _logger;
 private readonly ISupportEmailService _emailService;

 public SupportCaseService(AdminDbContext db, ILogger<SupportCaseService> logger, ISupportEmailService emailService)
 {
 _db = db;
 _logger = logger;
 _emailService = emailService;
 }

 // Testing constructor overload with default logger and no-op email service
 public SupportCaseService(AdminDbContext db, ILogger<SupportCaseService> logger)
 : this(db, logger, new NullSupportEmailService())
 {
 }

 public SupportCaseService(AdminDbContext db, ISupportEmailService emailService)
 : this(db, Microsoft.Extensions.Logging.Abstractions.NullLogger<SupportCaseService>.Instance, emailService)
 {
 }

 public SupportCaseService(AdminDbContext db)
 : this(db, Microsoft.Extensions.Logging.Abstractions.NullLogger<SupportCaseService>.Instance, new NullSupportEmailService())
 {
 }

 // Create a new customer support case with transaction context
 public async Task<SupportCaseDetailDto> CreateCaseAsync(CreateSupportCaseRequest req, int creatorId, string creatorName)
 {
 // Anti-spam Rule 1: Prevent duplicate open cases for the same Order ID
 if (req.OrderId.HasValue && req.OrderId.Value > 0)
 {
 var existingOrderCase = await _db.SupportCases
 .Where(c => c.OrderId == req.OrderId.Value && c.Status != "Closed" && c.Status != "Resolved")
 .OrderByDescending(c => c.CreatedAt)
 .FirstOrDefaultAsync();

 if (existingOrderCase != null)
 {
 throw new InvalidOperationException($"An active support case (#{existingOrderCase.CaseNumber}) already exists for Order #{req.OrderId.Value}. Our team is currently reviewing your request.");
 }
 }

 // Anti-spam Rule 2: Rate limit rapid repeated submissions (cooldown 30 seconds)
 var cooldownTime = DateTime.UtcNow.AddSeconds(-30);
 var recentCase = await _db.SupportCases
 .Where(c => (req.CustomerId.HasValue && c.CustomerId == req.CustomerId.Value) ||
 c.CustomerEmail.ToLower() == req.CustomerEmail.Trim().ToLower())
 .Where(c => c.CreatedAt >= cooldownTime)
 .FirstOrDefaultAsync();

 if (recentCase != null)
 {
 throw new InvalidOperationException("You have submitted a request recently. Please wait a moment before sending another request.");
 }

 // Generate unique case number: SC-YYYYMMDD-XXXX
 var datePrefix = $"SC-{DateTime.UtcNow:yyyyMMdd}-";
 var countToday = await _db.SupportCases.CountAsync(c => c.CaseNumber.StartsWith(datePrefix));
 var caseNumber = $"{datePrefix}{(countToday + 1):D4}";

 var supportCase = new SupportCase
 {
 CaseNumber = caseNumber,
 CustomerId = req.CustomerId,
 CustomerName = req.CustomerName.Trim(),
 CustomerEmail = req.CustomerEmail.Trim(),
 CustomerPhone = req.CustomerPhone?.Trim(),
 Title = req.Title.Trim(),
 Description = req.Description.Trim(),
 Category = string.IsNullOrWhiteSpace(req.Category) ? "General Inquiry" : req.Category.Trim(),
 Priority = string.IsNullOrWhiteSpace(req.Priority) ? "Medium" : req.Priority.Trim(),
 Status = "Open",
 // Preserve transaction links 
 OrderId = req.OrderId,
 TicketId = req.TicketId,
 PaymentReference = req.PaymentReference?.Trim(),
 CreatedBy = creatorId,
 CreatedByName = creatorName,
 CreatedAt = DateTime.UtcNow,
 UpdatedAt = DateTime.UtcNow
 };

 _db.SupportCases.Add(supportCase);
 await _db.SaveChangesAsync();

 // Create initial creation audit note
 var initialNote = new SupportCaseNote
 {
 CaseId = supportCase.CaseId,
 AuthorId = creatorId,
 AuthorName = creatorName,
 AuthorRole = "Creator",
 NoteType = "SystemAudit",
 Content = $"Support case #{caseNumber} created with priority '{supportCase.Priority}' and category '{supportCase.Category}'.",
 CreatedAt = DateTime.UtcNow
 };
 _db.SupportCaseNotes.Add(initialNote);
 await _db.SaveChangesAsync();

 _logger.LogInformation("Support case #{CaseNumber} created by user {UserId}", caseNumber, creatorId);
 return MapToDetailDto(supportCase, new List<SupportCaseNote> { initialNote });
 }

 // List support cases with staff visibility restriction (Staff only sees assigned or unassigned cases)
 public async Task<List<SupportCaseListDto>> GetCasesAsync(SupportCaseQuery query, int currentUserId, bool isAdmin)
 {
 var q = _db.SupportCases.AsNoTracking().AsQueryable();

 // Role-based visibility scope
 if (!isAdmin)
 {
 q = q.Where(c => c.AssignedStaffId == currentUserId || c.AssignedStaffId == null);
 }

 // Filter by status
 if (!string.IsNullOrWhiteSpace(query.Status))
 {
 q = q.Where(c => c.Status == query.Status.Trim());
 }

 // Filter by priority
 if (!string.IsNullOrWhiteSpace(query.Priority))
 {
 q = q.Where(c => c.Priority == query.Priority.Trim());
 }

 // Filter by assigned staff ID
 if (query.AssignedStaffId.HasValue)
 {
 q = q.Where(c => c.AssignedStaffId == query.AssignedStaffId.Value);
 }

 // Search term matching case number, title, or customer name
 if (!string.IsNullOrWhiteSpace(query.SearchTerm))
 {
 var term = query.SearchTerm.Trim().ToLower();
 q = q.Where(c => c.CaseNumber.ToLower().Contains(term) ||
 c.Title.ToLower().Contains(term) ||
 c.CustomerName.ToLower().Contains(term) ||
 c.CustomerEmail.ToLower().Contains(term));
 }

 // Sort latest first and paginate
 var items = await q.OrderByDescending(c => c.CreatedAt)
 .Skip((query.Page - 1) * query.PageSize)
 .Take(query.PageSize)
 .Select(c => new SupportCaseListDto
 {
 CaseId = c.CaseId,
 CaseNumber = c.CaseNumber,
 Title = c.Title,
 CustomerName = c.CustomerName,
 CustomerEmail = c.CustomerEmail,
 Category = c.Category,
 Priority = c.Priority,
 Status = c.Status,
 AssignedStaffId = c.AssignedStaffId,
 AssignedStaffName = c.AssignedStaffName,
 CreatedAt = c.CreatedAt,
 UpdatedAt = c.UpdatedAt
 })
 .ToListAsync();

 return items;
 }

 // View support case detail along with notes activity timeline
 public async Task<SupportCaseDetailDto> GetCaseDetailAsync(int caseId, int currentUserId, bool isAdmin)
 {
 var supportCase = await _db.SupportCases.AsNoTracking().FirstOrDefaultAsync(c => c.CaseId == caseId);
 if (supportCase == null)
 {
 throw new KeyNotFoundException($"Support case #{caseId} not found.");
 }

 // Enforce staff visibility scope
 if (!isAdmin && supportCase.AssignedStaffId != null && supportCase.AssignedStaffId != currentUserId)
 {
 throw new UnauthorizedAccessException("You do not have permission to view support cases assigned to another staff member.");
 }

 var notes = await _db.SupportCaseNotes.AsNoTracking()
 .Where(n => n.CaseId == caseId)
 .OrderBy(n => n.CreatedAt)
 .ToListAsync();

 return MapToDetailDto(supportCase, notes);
 }

 // Assign support case to eligible staff (Admin only)
 public async Task<SupportCaseDetailDto> AssignStaffAsync(int caseId, AssignStaffRequest req, int adminId, string adminName)
 {
 var supportCase = await _db.SupportCases.FirstOrDefaultAsync(c => c.CaseId == caseId);
 if (supportCase == null)
 {
 throw new KeyNotFoundException($"Support case #{caseId} not found.");
 }

 // & Closed support cases cannot be reassigned
 if (supportCase.Status == "Closed" || supportCase.Status == "Resolved")
 {
 throw new InvalidOperationException("Cannot reassign a support case that is already closed or resolved .");
 }

 var oldStaff = supportCase.AssignedStaffName;
 supportCase.AssignedStaffId = req.StaffUserId;
 supportCase.AssignedStaffName = req.StaffName.Trim();
 supportCase.AssignedAt = DateTime.UtcNow;
 supportCase.AssignedBy = adminId;
 supportCase.Status = "Assigned";
 supportCase.UpdatedAt = DateTime.UtcNow;

 // Log assignment history note
 var note = new SupportCaseNote
 {
 CaseId = caseId,
 AuthorId = adminId,
 AuthorName = adminName,
 AuthorRole = "Admin",
 NoteType = "AssignmentChange",
 Content = string.IsNullOrEmpty(oldStaff)
 ? $"Case assigned to staff '{req.StaffName.Trim()}'."
 : $"Case reassigned from '{oldStaff}' to '{req.StaffName.Trim()}'.",
 CreatedAt = DateTime.UtcNow
 };
 _db.SupportCaseNotes.Add(note);

 await _db.SaveChangesAsync();
 _logger.LogInformation("Support case #{CaseId} assigned to {StaffName} by Admin {AdminId}", caseId, req.StaffName, adminId);

 // 1. In-App Notification (Persist to notification_db for staff bell tray)
 try
 {
 using var notifConn = new Npgsql.NpgsqlConnection("Host=localhost;Port=5432;Database=notification_db;Username=postgres;Password=123456");
 await notifConn.OpenAsync();
 using var notifCmd = new Npgsql.NpgsqlCommand(
 "INSERT INTO notifications (user_id, title, message, category, target_url, is_read, is_deleted, created_at) " +
 "VALUES (@uid, @title, @msg, @cat, @url, false, false, NOW());", notifConn);
 notifCmd.Parameters.AddWithValue("uid", req.StaffUserId);
 notifCmd.Parameters.AddWithValue("title", $"Assigned to Case #{supportCase.CaseNumber}");
 notifCmd.Parameters.AddWithValue("msg", $"You have been assigned to handle support case #{supportCase.CaseNumber} ('{supportCase.Title}').");
 notifCmd.Parameters.AddWithValue("cat", "support_assignment");
 notifCmd.Parameters.AddWithValue("url", $"/staff/support-cases/{caseId}");
 await notifCmd.ExecuteNonQueryAsync();
 _logger.LogInformation("In-app assignment notification created for staff user {StaffId}", req.StaffUserId);
 }
 catch (Exception ex)
 {
 _logger.LogWarning(ex, "Could not persist in-app notification for staff user {StaffId}", req.StaffUserId);
 }

 // 2. Email Notification to Staff
 var staffEmail = req.StaffEmail?.Trim();
 if (string.IsNullOrWhiteSpace(staffEmail))
 {
 try
 {
 using var authConn = new Npgsql.NpgsqlConnection("Host=localhost;Port=5432;Database=authentication_db;Username=postgres;Password=123456");
 await authConn.OpenAsync();
 using var authCmd = new Npgsql.NpgsqlCommand("SELECT email FROM users WHERE user_id = @uid LIMIT 1;", authConn);
 authCmd.Parameters.AddWithValue("uid", req.StaffUserId);
 var lookup = await authCmd.ExecuteScalarAsync() as string;
 if (!string.IsNullOrWhiteSpace(lookup))
 {
 staffEmail = lookup.Trim();
 }
 }
 catch (Exception ex)
 {
 _logger.LogWarning(ex, "Could not query staff email from authentication_db for user {UserId}", req.StaffUserId);
 }
 }

 if (!string.IsNullOrWhiteSpace(staffEmail))
 {
 try
 {
 await _emailService.SendCaseAssignedEmailAsync(
 staffEmail,
 req.StaffName,
 supportCase.CaseNumber,
 supportCase.Title,
 supportCase.Priority,
 supportCase.Category,
 supportCase.CustomerName,
 supportCase.OrderId,
 caseId);
 }
 catch (Exception ex)
 {
 _logger.LogWarning(ex, "Failed to dispatch assignment email to staff {StaffEmail}", staffEmail);
 }
 }

 var notes = await _db.SupportCaseNotes.AsNoTracking().Where(n => n.CaseId == caseId).OrderBy(n => n.CreatedAt).ToListAsync();
 return MapToDetailDto(supportCase, notes);
 }

 // Resolve and close support case with mandatory resolution
 public async Task<SupportCaseDetailDto> ResolveCaseAsync(int caseId, ResolveCaseRequest req, int resolverId, string resolverName, string role)
 {
 if (string.IsNullOrWhiteSpace(req.Resolution))
 {
 // Closing a support case strictly requires a recorded resolution
 throw new ArgumentException("Resolution details are strictly required when closing a support case .");
 }

 var supportCase = await _db.SupportCases.FirstOrDefaultAsync(c => c.CaseId == caseId);
 if (supportCase == null)
 {
 throw new KeyNotFoundException($"Support case #{caseId} not found.");
 }

 // Prevent closing an already closed case
 if (supportCase.Status == "Closed")
 {
 throw new InvalidOperationException("Support case is already closed.");
 }

 supportCase.Status = "Closed";
 supportCase.Resolution = req.Resolution.Trim();
 supportCase.ResolvedBy = resolverId;
 supportCase.ResolvedByName = resolverName;
 supportCase.ResolvedAt = DateTime.UtcNow;
 supportCase.UpdatedAt = DateTime.UtcNow;

 // Add closure record note
 var resolutionNote = new SupportCaseNote
 {
 CaseId = caseId,
 AuthorId = resolverId,
 AuthorName = resolverName,
 AuthorRole = role,
 NoteType = "Resolution",
 Content = $"Case closed with resolution: {req.Resolution.Trim()}",
 CreatedAt = DateTime.UtcNow
 };
 _db.SupportCaseNotes.Add(resolutionNote);

 await _db.SaveChangesAsync();
 _logger.LogInformation("Support case #{CaseId} resolved and closed by {ResolverName}", caseId, resolverName);

 // Initiate customer outcome notification via email
 try
 {
 await _emailService.SendCaseResolvedEmailAsync(
 supportCase.CustomerEmail,
 supportCase.CustomerName,
 supportCase.CaseNumber,
 supportCase.Title,
 supportCase.Resolution,
 resolverName,
 supportCase.OrderId
 );
 }
 catch (Exception ex)
 {
 _logger.LogWarning(ex, "Failed to send resolution email for case #{CaseNumber}", supportCase.CaseNumber);
 }

 var notes = await _db.SupportCaseNotes.AsNoTracking().Where(n => n.CaseId == caseId).OrderBy(n => n.CreatedAt).ToListAsync();
 return MapToDetailDto(supportCase, notes);
 }

 // Get latest support case linked to an order
 public async Task<SupportCaseDetailDto?> GetCaseByOrderIdAsync(int orderId)
 {
 var supportCase = await _db.SupportCases.AsNoTracking()
 .Where(c => c.OrderId == orderId)
 .OrderByDescending(c => c.CreatedAt)
 .FirstOrDefaultAsync();

 if (supportCase == null) return null;

 var notes = await _db.SupportCaseNotes.AsNoTracking()
 .Where(n => n.CaseId == supportCase.CaseId)
 .OrderBy(n => n.CreatedAt)
 .ToListAsync();

 return MapToDetailDto(supportCase, notes);
 }

 // Append internal investigation or communication note
 public async Task<SupportCaseNoteDto> AddNoteAsync(int caseId, AddSupportCaseNoteRequest req, int authorId, string authorName, string authorRole)
 {
 var supportCase = await _db.SupportCases.FirstOrDefaultAsync(c => c.CaseId == caseId);
 if (supportCase == null)
 {
 throw new KeyNotFoundException($"Support case #{caseId} not found.");
 }

 var note = new SupportCaseNote
 {
 CaseId = caseId,
 AuthorId = authorId,
 AuthorName = authorName,
 AuthorRole = authorRole,
 NoteType = string.IsNullOrWhiteSpace(req.NoteType) ? "InternalNote" : req.NoteType.Trim(),
 Content = req.Content.Trim(),
 CreatedAt = DateTime.UtcNow
 };

 _db.SupportCaseNotes.Add(note);
 await _db.SaveChangesAsync();

 // Dispatch real-time SignalR & In-app notification for recipient
 try
 {
 int targetUserId = 0;
 string targetUrl = "";
 string title = "";
 string message = "";
 var preview = req.Content.Trim().Length > 80 ? req.Content.Trim().Substring(0, 77) + "..." : req.Content.Trim();

 if (authorRole.Equals("Staff", StringComparison.OrdinalIgnoreCase) || authorRole.Equals("SupportStaff", StringComparison.OrdinalIgnoreCase))
 {
 // Staff wrote a note -> Notify Admin
 targetUserId = supportCase.AssignedBy ?? 1;
 targetUrl = $"/admin/support-cases/{caseId}";
 title = $"Staff Note on Case #{supportCase.CaseNumber}";
 message = $"{authorName} added a note: \"{preview}\"";
 }
 else if (authorRole.Equals("Admin", StringComparison.OrdinalIgnoreCase))
 {
 // Admin wrote a note -> Notify Assigned Staff
 if (supportCase.AssignedStaffId.HasValue)
 {
 targetUserId = supportCase.AssignedStaffId.Value;
 targetUrl = $"/staff/support-cases/{caseId}";
 title = $"Admin Note on Case #{supportCase.CaseNumber}";
 message = $"Admin {authorName} added a note: \"{preview}\"";
 }
 }

 if (targetUserId > 0)
 {
 // 1. Persist directly to notification_db
 using (var notifConn = new Npgsql.NpgsqlConnection("Host=localhost;Port=5432;Database=notification_db;Username=postgres;Password=123456"))
 {
 await notifConn.OpenAsync();
 using var notifCmd = new Npgsql.NpgsqlCommand(
 "INSERT INTO notifications (user_id, title, message, category, target_url, is_read, is_deleted, created_at) " +
 "VALUES (@uid, @title, @msg, @cat, @url, false, false, NOW());", notifConn);
 notifCmd.Parameters.AddWithValue("uid", targetUserId);
 notifCmd.Parameters.AddWithValue("title", title);
 notifCmd.Parameters.AddWithValue("msg", message);
 notifCmd.Parameters.AddWithValue("cat", "support_note");
 notifCmd.Parameters.AddWithValue("url", targetUrl);
 await notifCmd.ExecuteNonQueryAsync();
 }

 // 2. Real-time ping to NotificationAPI to trigger SignalR notification
 try
 {
 var handler = new System.Net.Http.HttpClientHandler
 {
 ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
 };
 using var httpClient = new System.Net.Http.HttpClient(handler);
 httpClient.Timeout = TimeSpan.FromSeconds(2);
 var payload = new
 {
 userId = targetUserId,
 title = title,
 message = message,
 category = "support_note",
 targetUrl = targetUrl
 };
 var jsonContent = new System.Net.Http.StringContent(
 System.Text.Json.JsonSerializer.Serialize(payload),
 System.Text.Encoding.UTF8,
 "application/json"
 );
 await httpClient.PostAsync("http://localhost:7186/api/notifications", jsonContent);
 }
 catch
 {
 // Non-blocking real-time ping
 }
 }
 }
 catch (Exception ex)
 {
 _logger.LogWarning(ex, "Failed to persist note notification for case #{CaseId}", caseId);
 }

 return new SupportCaseNoteDto
 {
 NoteId = note.NoteId,
 CaseId = note.CaseId,
 AuthorId = note.AuthorId,
 AuthorName = note.AuthorName,
 AuthorRole = note.AuthorRole,
 NoteType = note.NoteType,
 Content = note.Content,
 CreatedAt = note.CreatedAt
 };
 }

 // Helper mapper from Entity to Detail DTO
 private static SupportCaseDetailDto MapToDetailDto(SupportCase c, List<SupportCaseNote> notes)
 {
 return new SupportCaseDetailDto
 {
 CaseId = c.CaseId,
 CaseNumber = c.CaseNumber,
 CustomerId = c.CustomerId,
 CustomerName = c.CustomerName,
 CustomerEmail = c.CustomerEmail,
 CustomerPhone = c.CustomerPhone,
 Title = c.Title,
 Description = c.Description,
 Category = c.Category,
 Priority = c.Priority,
 Status = c.Status,
 OrderId = c.OrderId,
 TicketId = c.TicketId,
 PaymentReference = c.PaymentReference,
 AssignedStaffId = c.AssignedStaffId,
 AssignedStaffName = c.AssignedStaffName,
 AssignedAt = c.AssignedAt,
 AssignedBy = c.AssignedBy,
 Resolution = c.Resolution,
 ResolvedBy = c.ResolvedBy,
 ResolvedByName = c.ResolvedByName,
 ResolvedAt = c.ResolvedAt,
 CreatedBy = c.CreatedBy,
 CreatedByName = c.CreatedByName,
 CreatedAt = c.CreatedAt,
 UpdatedAt = c.UpdatedAt,
 Notes = notes.Select(n => new SupportCaseNoteDto
 {
 NoteId = n.NoteId,
 CaseId = n.CaseId,
 AuthorId = n.AuthorId,
 AuthorName = n.AuthorName,
 AuthorRole = n.AuthorRole,
 NoteType = n.NoteType,
 Content = n.Content,
 CreatedAt = n.CreatedAt
 }).ToList()
 };
 }
}
