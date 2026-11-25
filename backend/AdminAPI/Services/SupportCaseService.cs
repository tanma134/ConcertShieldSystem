using System.Text;
using AdminAPI.Data;
using AdminAPI.DTOs;
using AdminAPI.Models;
using AdminAPI.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AdminAPI.Services;

// Handles customer support cases; ticket disputes live in a separate module.
public class SupportCaseService : ISupportCaseService
{
    private static readonly HashSet<string> AllowedCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Account & Security", "Technical Issue", "General Inquiry",
        "Event Information", "Order & Ticket Help",
        "Denied Entry", "Invalid Ticket", "Payment Dispute", "Refund Complaint",
        "Ticketing", "Payment & Billing", "Event Dispute"
    };

    private static string NormalizeCategory(string? category)
    {
        var value = string.IsNullOrWhiteSpace(category) ? "General Inquiry" : category.Trim();
        var match = AllowedCategories.FirstOrDefault(x => x.Equals(value, StringComparison.OrdinalIgnoreCase));
        if (match is null)
            throw new ArgumentException("Invalid support category.");
        return match;
    }

    private readonly ISupportCaseRepository _repository;
    private readonly ILogger<SupportCaseService> _logger;
    private readonly ISupportEmailService _emailService;
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly IConfiguration? _configuration;

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public SupportCaseService(
        ISupportCaseRepository repository,
        ILogger<SupportCaseService> logger,
        ISupportEmailService emailService,
        IHttpClientFactory? httpClientFactory = null,
        IConfiguration? configuration = null)
    {
        _repository = repository;
        _logger = logger;
        _emailService = emailService;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    // Testing constructor overload with default logger and no-op email service
    public SupportCaseService(ISupportCaseRepository repository, ILogger<SupportCaseService> logger)
        : this(repository, logger, new NullSupportEmailService(), null, null)
    {
    }

    public SupportCaseService(ISupportCaseRepository repository, ISupportEmailService emailService)
        : this(repository, Microsoft.Extensions.Logging.Abstractions.NullLogger<SupportCaseService>.Instance, emailService, null, null)
    {
    }

    public SupportCaseService(ISupportCaseRepository repository)
        : this(repository, Microsoft.Extensions.Logging.Abstractions.NullLogger<SupportCaseService>.Instance, new NullSupportEmailService(), null, null)
    {
    }

    public SupportCaseService(AdminDbContext db)
        : this(new SupportCaseRepository(db), Microsoft.Extensions.Logging.Abstractions.NullLogger<SupportCaseService>.Instance, new NullSupportEmailService(), null, null)
    {
    }

    public SupportCaseService(AdminDbContext db, ILogger<SupportCaseService> logger)
        : this(new SupportCaseRepository(db), logger, new NullSupportEmailService(), null, null)
    {
    }

    public SupportCaseService(AdminDbContext db, ISupportEmailService emailService)
        : this(new SupportCaseRepository(db), Microsoft.Extensions.Logging.Abstractions.NullLogger<SupportCaseService>.Instance, emailService, null, null)
    {
    }

    private async Task DispatchNotificationAsync(int userId, string title, string message, string category, string targetUrl, string idempotencyKey, CancellationToken ct = default)
    {
        if (userId <= 0) return;
        try
        {
            var notifApiUrl = _configuration?["NotificationService:Url"] ?? "http://localhost:7186";
            var client = _httpClientFactory?.CreateClient() ?? new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(3);

            var internalApiKey = _configuration?["Governance:InternalApiKey"] ?? _configuration?["InternalApi:Key"];
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{notifApiUrl}/api/internal/notifications");
            if (!string.IsNullOrEmpty(internalApiKey))
            {
                req.Headers.Add("X-Internal-Api-Key", internalApiKey);
            }
            if (!string.IsNullOrEmpty(idempotencyKey))
            {
                req.Headers.Add("Idempotency-Key", idempotencyKey);
            }
            req.Content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    userId,
                    title,
                    message,
                    category,
                    targetUrl
                }),
                Encoding.UTF8,
                "application/json");

            var resp = await client.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                using var fallbackReq = new HttpRequestMessage(HttpMethod.Post, $"{notifApiUrl}/api/notifications");
                fallbackReq.Content = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        userId,
                        title,
                        message,
                        category,
                        targetUrl
                    }),
                    Encoding.UTF8,
                    "application/json");
                await client.SendAsync(fallbackReq, ct);
            }
            _logger.LogInformation("Notification dispatched for user {UserId} with key {Key}", userId, idempotencyKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not dispatch notification via NotificationAPI for user {UserId}. Trying DB fallback if configured.", userId);

            // Only fall back to direct DB when a connection string is configured.
            var notifConnStr = _configuration?.GetConnectionString("NotificationConnection")
                ?? _configuration?.GetConnectionString("NotificationDb")
                ?? Environment.GetEnvironmentVariable("CONCERTSHIELD_NOTIFICATION_POSTGRES")
                ?? Environment.GetEnvironmentVariable("CONCERTSHIELD_TEST_POSTGRES");

            if (!string.IsNullOrWhiteSpace(notifConnStr))
            {
                try
                {
                    using var conn = new NpgsqlConnection(notifConnStr);
                    await conn.OpenAsync(ct);
                    using var cmd = new NpgsqlCommand(
                        "INSERT INTO notifications (user_id, title, message, category, target_url, is_read, is_deleted, created_at) " +
                        "VALUES (@uid, @title, @msg, @cat, @url, false, false, NOW());", conn);
                    cmd.Parameters.AddWithValue("uid", userId);
                    cmd.Parameters.AddWithValue("title", title);
                    cmd.Parameters.AddWithValue("msg", message);
                    cmd.Parameters.AddWithValue("cat", category);
                    cmd.Parameters.AddWithValue("url", targetUrl);
                    await cmd.ExecuteNonQueryAsync(ct);
                }
                catch (Exception dbEx)
                {
                    _logger.LogWarning(dbEx, "Fallback DB notification insert failed for user {UserId}", userId);
                }
            }
        }
    }

    public async Task<SupportCaseDetailDto> CreateCaseAsync(CreateSupportCaseRequest req, int creatorId, string creatorName)
    {
        // Anti-spam Rule 1: Prevent duplicate open cases for the same Order ID
        if (req.OrderId.HasValue && req.OrderId.Value > 0)
        {
            var existingOrderCase = await _repository.Cases
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
        var recentCase = await _repository.Cases
            .Where(c => (req.CustomerId.HasValue && c.CustomerId == req.CustomerId.Value) ||
                        c.CustomerEmail.ToLower() == req.CustomerEmail.Trim().ToLower())
            .Where(c => c.CreatedAt >= cooldownTime)
            .FirstOrDefaultAsync();

        if (recentCase != null)
        {
            throw new InvalidOperationException("You have submitted a request recently. Please wait a moment before sending another request.");
        }

        var datePrefix = $"SC-{DateTime.UtcNow:yyyyMMdd}-";
        var countToday = await _repository.Cases.CountAsync(c => c.CaseNumber.StartsWith(datePrefix));
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
            Category = NormalizeCategory(req.Category),
            Priority = string.IsNullOrWhiteSpace(req.Priority) ? "Medium" : req.Priority.Trim(),
            Status = "Open",
            OrderId = req.OrderId,
            TicketId = req.TicketId,
            PaymentReference = req.PaymentReference?.Trim(),
            CreatedBy = creatorId,
            CreatedByName = creatorName,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _repository.AddCase(supportCase);
        await _repository.SaveChangesAsync();

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
        _repository.AddNote(initialNote);
        await _repository.SaveChangesAsync();

        _logger.LogInformation("Support case #{CaseNumber} created by user {UserId}", caseNumber, creatorId);
        return MapToDetailDto(supportCase, new List<SupportCaseNote> { initialNote });
    }

    public async Task<List<SupportCaseListDto>> GetCasesAsync(SupportCaseQuery query, int currentUserId, bool isAdmin)
    {
        var q = _repository.Cases.AsNoTracking().AsQueryable();

        if (!isAdmin)
        {
            q = q.Where(c => c.AssignedStaffId == currentUserId || c.AssignedStaffId == null);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            q = q.Where(c => c.Status == query.Status.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.Priority))
        {
            q = q.Where(c => c.Priority == query.Priority.Trim());
        }

        if (query.AssignedStaffId.HasValue)
        {
            q = q.Where(c => c.AssignedStaffId == query.AssignedStaffId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim().ToLower();
            q = q.Where(c => c.CaseNumber.ToLower().Contains(term) ||
                             c.Title.ToLower().Contains(term) ||
                             c.CustomerName.ToLower().Contains(term) ||
                             c.CustomerEmail.ToLower().Contains(term));
        }

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

    public async Task<SupportCaseDetailDto> GetCaseDetailAsync(int caseId, int currentUserId, bool isAdmin)
    {
        var supportCase = await _repository.Cases.AsNoTracking().FirstOrDefaultAsync(c => c.CaseId == caseId);
        if (supportCase == null)
        {
            throw new KeyNotFoundException($"Support case #{caseId} not found.");
        }

        // Enforce staff visibility scope: Admin and the customer owner can always view
        var isOwner = (supportCase.CustomerId.HasValue && supportCase.CustomerId.Value == currentUserId);
        if (!isAdmin && !isOwner && supportCase.AssignedStaffId != null && supportCase.AssignedStaffId != currentUserId)
        {
            throw new UnauthorizedAccessException("You do not have permission to view support cases assigned to another staff member.");
        }

        var notes = await _repository.Notes.AsNoTracking()
            .Where(n => n.CaseId == caseId)
            .OrderBy(n => n.CreatedAt)
            .ToListAsync();

        return MapToDetailDto(supportCase, notes);
    }

    public async Task<SupportCaseDetailDto> AssignStaffAsync(int caseId, AssignStaffRequest req, int adminId, string adminName)
    {
        var supportCase = await _repository.Cases.FirstOrDefaultAsync(c => c.CaseId == caseId);
        if (supportCase == null)
        {
            throw new KeyNotFoundException($"Support case #{caseId} not found.");
        }

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
        _repository.AddNote(note);

        await _repository.SaveChangesAsync();
        _logger.LogInformation("Support case #{CaseId} assigned to {StaffName} by Admin {AdminId}", caseId, req.StaffName, adminId);

        await DispatchNotificationAsync(
            req.StaffUserId,
            $"Assigned to Case #{supportCase.CaseNumber}",
            $"You have been assigned to handle support case #{supportCase.CaseNumber} ('{supportCase.Title}').",
            "support_assignment",
            $"/staff/support-cases/{caseId}",
            $"case-{caseId}-assign-staff-{req.StaffUserId}");

        // Do not notify the customer about internal staff assignment.

        var staffEmail = req.StaffEmail?.Trim();
        if (string.IsNullOrWhiteSpace(staffEmail))
        {
            var authConnStr = _configuration?.GetConnectionString("AuthenticationConnection")
                ?? _configuration?.GetConnectionString("AuthenticationDb")
                ?? Environment.GetEnvironmentVariable("CONCERTSHIELD_AUTH_POSTGRES")
                ?? Environment.GetEnvironmentVariable("CONCERTSHIELD_TEST_POSTGRES");

            if (!string.IsNullOrWhiteSpace(authConnStr))
            {
                try
                {
                    using var authConn = new NpgsqlConnection(authConnStr);
                    await authConn.OpenAsync();
                    using var authCmd = new NpgsqlCommand("SELECT email FROM users WHERE user_id = @uid LIMIT 1;", authConn);
                    authCmd.Parameters.AddWithValue("uid", req.StaffUserId);
                    var lookup = await authCmd.ExecuteScalarAsync() as string;
                    if (!string.IsNullOrWhiteSpace(lookup))
                    {
                        staffEmail = lookup.Trim();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not query staff email for user {UserId}", req.StaffUserId);
                }
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
                    caseId
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to dispatch assignment email to staff {StaffEmail}", staffEmail);
            }
        }

        var notes = await _repository.Notes.AsNoTracking().Where(n => n.CaseId == caseId).OrderBy(n => n.CreatedAt).ToListAsync();
        return MapToDetailDto(supportCase, notes);
    }

    public async Task<SupportCaseDetailDto> ResolveCaseAsync(int caseId, ResolveCaseRequest req, int resolverId, string resolverName, string role)
    {
        if (string.IsNullOrWhiteSpace(req.Resolution))
        {
            throw new ArgumentException("Resolution details are strictly required when closing a support case .");
        }

        var supportCase = await _repository.Cases.FirstOrDefaultAsync(c => c.CaseId == caseId);
        if (supportCase == null)
        {
            throw new KeyNotFoundException($"Support case #{caseId} not found.");
        }

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
        _repository.AddNote(resolutionNote);

        await _repository.SaveChangesAsync();
        _logger.LogInformation("Support case #{CaseId} resolved and closed by {ResolverName}", caseId, resolverName);

        if (supportCase.CustomerId.HasValue && supportCase.CustomerId.Value > 0)
        {
            await DispatchNotificationAsync(
                supportCase.CustomerId.Value,
                $"Case #{supportCase.CaseNumber} Resolved",
                $"Your support case #{supportCase.CaseNumber} has been resolved by {resolverName}. Resolution: {req.Resolution.Trim()}",
                "support_status",
                $"/support-cases/{caseId}",
                $"case-{caseId}-resolve");
        }

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

        var notes = await _repository.Notes.AsNoTracking().Where(n => n.CaseId == caseId).OrderBy(n => n.CreatedAt).ToListAsync();
        return MapToDetailDto(supportCase, notes);
    }

    public async Task<SupportCaseDetailDto?> GetCaseByOrderIdAsync(int orderId)
    {
        var supportCase = await _repository.Cases.AsNoTracking()
            .Where(c => c.OrderId == orderId)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();

        if (supportCase == null) return null;

        var notes = await _repository.Notes.AsNoTracking()
            .Where(n => n.CaseId == supportCase.CaseId)
            .OrderBy(n => n.CreatedAt)
            .ToListAsync();

        return MapToDetailDto(supportCase, notes);
    }

    public async Task<SupportCaseNoteDto> AddNoteAsync(int caseId, AddSupportCaseNoteRequest req, int authorId, string authorName, string authorRole)
    {
        var supportCase = await _repository.Cases.FirstOrDefaultAsync(c => c.CaseId == caseId);
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

        _repository.AddNote(note);
        await _repository.SaveChangesAsync();

        var preview = req.Content.Trim().Length > 80 ? req.Content.Trim().Substring(0, 77) + "..." : req.Content.Trim();

        // Only notify the customer for public notes (skip InternalNote).
        if (note.NoteType != "InternalNote" && supportCase.CustomerId.HasValue && supportCase.CustomerId.Value > 0 && authorId != supportCase.CustomerId.Value)
        {
            await DispatchNotificationAsync(
                supportCase.CustomerId.Value,
                $"Support case #{supportCase.CaseNumber} updated",
                $"Your support case has a new update: \"{preview}\"",
                "support_note",
                $"/support-cases/{caseId}",
                $"case-{caseId}-note-{note.NoteId}-cust");
        }

        if (authorRole.Equals("Staff", StringComparison.OrdinalIgnoreCase) || authorRole.Equals("SupportStaff", StringComparison.OrdinalIgnoreCase))
        {
            var adminId = supportCase.AssignedBy ?? 1;
            await DispatchNotificationAsync(
                adminId,
                $"Staff Note on Case #{supportCase.CaseNumber}",
                $"{authorName} added a note: \"{preview}\"",
                "support_note",
                $"/admin/support-cases/{caseId}",
                $"case-{caseId}-note-{note.NoteId}-admin");
        }
        else if (authorRole.Equals("Admin", StringComparison.OrdinalIgnoreCase) && supportCase.AssignedStaffId.HasValue)
        {
            await DispatchNotificationAsync(
                supportCase.AssignedStaffId.Value,
                $"Admin Note on Case #{supportCase.CaseNumber}",
                $"Admin {authorName} added a note: \"{preview}\"",
                "support_note",
                $"/staff/support-cases/{caseId}",
                $"case-{caseId}-note-{note.NoteId}-staff");
        }
        else if (!authorRole.Equals("Admin", StringComparison.OrdinalIgnoreCase) && !authorRole.Equals("Staff", StringComparison.OrdinalIgnoreCase))
        {
            // Customer added note -> notify assigned staff or admin
            var staffTarget = supportCase.AssignedStaffId ?? supportCase.AssignedBy ?? 1;
            await DispatchNotificationAsync(
                staffTarget,
                $"Customer Note on Case #{supportCase.CaseNumber}",
                $"Customer {authorName} added a note: \"{preview}\"",
                "support_note",
                $"/staff/support-cases/{caseId}",
                $"case-{caseId}-note-{note.NoteId}-staff");
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
