using System.Text.Json;
using EventAPI.Common;
using EventAPI.Data;
using EventAPI.DTOs;
using EventAPI.Models;
using Microsoft.EntityFrameworkCore;
namespace EventAPI.Services;

public class GovernanceService(EventDbContext db, IComplianceAssetStore assets, IHttpClientFactory http, IConfiguration config)
{
    public string[] RequiredTypes => config.GetSection("Compliance:RequiredTypes").Get<string[]>() ?? new[] { "EventPermit", "SafetyPlan" };
    public long MaxBytes => Math.Clamp(config.GetValue<long>("Compliance:MaxFileBytes", 10485760), 1, 20971520);
    // Khóa event trong transaction để upload/review/submit/apply cùng tuân thủ một thứ tự cập nhật.
    public async Task<Event> GetAsync(int id, int caller, bool admin, bool locked, CancellationToken ct)
    {
        if (id <= 0) throw new ArgumentException("Invalid event ID.");
        var ev = locked ? await db.Events.FromSqlInterpolated($"SELECT *, xmin FROM events WHERE event_id = {id} AND NOT is_deleted FOR UPDATE").SingleOrDefaultAsync(ct)
            : await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == id && !x.IsDeleted, ct);
        if (locked && ev != null) await db.Entry(ev).ReloadAsync(ct);
        if (ev == null) throw new KeyNotFoundException("Concert not found.");
        if (!admin && ev.OrganizerId != caller) throw new UnauthorizedAccessException("You cannot manage this concert.");
        return ev;
    }
    public void Queue(string kind, object payload)
    {
        db.Set<GovernanceOutbox>().Add(new() { Kind = kind, Payload = JsonSerializer.Serialize(payload), CreatedAt = DateTime.UtcNow, NextAttemptAt = DateTime.UtcNow });
    }
    public void Notify(Event ev, string title, string message) => Queue("Notification", new NotificationMessage(ev.OrganizerId, title, message, "event_compliance", $"/organizer/events/{ev.EventId}/compliance"));
    // Gửi thông báo cho từng admin được cấu hình; không cấu hình thì bỏ qua (badge ở sidebar admin vẫn hiện).
    public void NotifyAdmins(Event ev, string title, string message)
    {
        var ids = config.GetSection("Governance:AdminUserIds").Get<int[]>() ?? Array.Empty<int>();
        foreach (var adminId in ids.Where(x => x > 0).Distinct())
            Queue("Notification", new NotificationMessage(adminId, title, message, "admin_change_request", "/admin/change-requests"));
    }
    public async Task EnsureCompleteAsync(Event ev, CancellationToken ct)
    {
        var types = await db.Set<ComplianceDocument>().Where(x => x.EventId == ev.EventId && x.Version == ev.ComplianceVersion).Select(x => x.DocumentType).ToListAsync(ct);
        if (!GovernanceRules.IsComplete(ev.ComplianceVersion, RequiredTypes, types))
            throw new InvalidOperationException("Upload all required compliance documents before submitting.");
    }
    public async Task<object> ComplianceAsync(int id, int caller, bool admin, CancellationToken ct)
    {
        var ev = await GetAsync(id, caller, admin, false, ct);
        var documents = await db.Set<ComplianceDocument>().AsNoTracking().Where(x => x.EventId == id).OrderByDescending(x => x.Version)
            .Select(x => new { x.Id, x.Version, x.DocumentType, x.FileName, x.ContentType, x.Size, x.SubmittedAt }).ToListAsync(ct);
        var reviews = await db.Set<ComplianceReview>().AsNoTracking().Where(x => x.EventId == id).OrderByDescending(x => x.Version).ToListAsync(ct);
        return new { ev.EventId, ev.Title, EventStatus = ev.Status, Status = ev.ComplianceStatus, Version = ev.ComplianceVersion, ReviewedVersion = ev.ComplianceReviewedVersion, RequiredTypes, MaxBytes, Documents = documents, Reviews = reviews };
    }
    public async Task UploadAsync(int id, int caller, string type, IFormFile file, CancellationToken ct)
    {
        if (!RequiredTypes.Contains(type)) throw new ArgumentException("Unsupported document type.");
        if (file == null || file.Length == 0 || file.Length > MaxBytes) throw new ArgumentException("File is empty or too large.");
        using var memory = new MemoryStream(); await file.CopyToAsync(memory, ct);
        var bytes = memory.ToArray(); var mime = GovernanceRules.ValidateFile(file.FileName, file.ContentType, bytes, MaxBytes);
        var preflight = await GetAsync(id, caller, false, false, ct);
        if (!EventStatus.Editable.Contains(EventStatus.Normalize(preflight.Status))) throw new InvalidOperationException("Documents can only be changed in Draft or Rejected concerts.");
        var publicId = "concertshield/compliance/" + Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName).ToLowerInvariant();
        // Ghi job dọn trước khi upload: DB chết sau upload vẫn có dấu vết để dọn khi DB phục hồi.
        var cleanup = new GovernanceOutbox { Kind = "DeleteAsset", Payload = JsonSerializer.Serialize(publicId), CreatedAt = DateTime.UtcNow, NextAttemptAt = DateTime.UtcNow.AddHours(2) };
        db.Set<GovernanceOutbox>().Add(cleanup); await db.SaveChangesAsync(ct);
        try
        {
            // Không giữ transaction/row lock trong lúc gửi file qua mạng.
            // Job cleanup đã lưu; nếu request hủy hoặc trạng thái đổi, asset vẫn được dọn.
            var stored = await assets.UploadAsync(bytes, publicId, ct);
            ct.ThrowIfCancellationRequested();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var ev = await GetAsync(id, caller, false, true, ct);
            if (!EventStatus.Editable.Contains(EventStatus.Normalize(ev.Status))) throw new InvalidOperationException("Documents can only be changed in Draft or Rejected concerts.");
            var prior = await db.Set<ComplianceDocument>().Where(x => x.EventId == id && x.Version == ev.ComplianceVersion && x.DocumentType != type).ToListAsync(ct);
            var version = ev.ComplianceVersion + 1;
            // Copy tham chiếu sang bộ hồ sơ mới, không thay đổi các phiên bản đã review.
            foreach (var doc in prior) db.Set<ComplianceDocument>().Add(new() { EventId = id, Version = version, DocumentType = doc.DocumentType, FileName = doc.FileName, ContentType = doc.ContentType, PublicId = doc.PublicId, SecureUrl = doc.SecureUrl, ResourceType = doc.ResourceType, Size = doc.Size, SubmittedBy = doc.SubmittedBy, SubmittedAt = doc.SubmittedAt });
            db.Set<ComplianceDocument>().Add(new() { EventId = id, Version = version, DocumentType = type, FileName = Path.GetFileName(file.FileName), ContentType = mime, PublicId = stored.PublicId, SecureUrl = stored.SecureUrl, Size = bytes.LongLength, SubmittedBy = caller, SubmittedAt = DateTime.UtcNow });
            cleanup.CompletedAt = DateTime.UtcNow;
            ev.ComplianceVersion = version; ev.ComplianceReviewedVersion = null; ev.ComplianceStatus = "NotSubmitted"; ev.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        catch
        {
            // Transaction rollback giữ job dọn ở trạng thái Pending; worker sẽ xử lý lại sau.
            db.ChangeTracker.Clear();
            throw;
        }
    }
    public async Task<(byte[] Bytes, string Mime, string Name)> DownloadAsync(int id, long docId, int caller, bool admin, CancellationToken ct)
    {
        await GetAsync(id, caller, admin, false, ct);
        var doc = await db.Set<ComplianceDocument>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == docId && x.EventId == id, ct) ?? throw new KeyNotFoundException("Document not found.");
        return (await assets.DownloadAsync(doc.PublicId, ct), doc.ContentType, doc.FileName);
    }
    public async Task ReviewComplianceAsync(int id, int admin, ComplianceReviewInput input, CancellationToken ct)
    {
        var notes = GovernanceRules.ValidateReview(input.Decision, input.Notes, true);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var ev = await GetAsync(id, admin, true, true, ct);
        if (ev.Status != EventStatus.Pending || ev.ComplianceStatus != "PendingReview" || input.Version != ev.ComplianceVersion)
            throw new GovernanceConflictException("The event or document version has changed. Reload before reviewing.");
        await EnsureCompleteAsync(ev, ct);
        db.Set<ComplianceReview>().Add(new() { EventId = id, Version = input.Version, Decision = input.Decision, Notes = notes, ReviewedBy = admin, ReviewedAt = DateTime.UtcNow });
        ev.ComplianceStatus = input.Decision; ev.ComplianceReviewedVersion = input.Version;
        if (input.Decision != "Approved") { ev.Status = EventStatus.Rejected; ev.RejectedReason = ("Compliance: " + notes)[..Math.Min(500, 12 + notes.Length)]; ev.RejectedAt = DateTime.UtcNow; }
        ev.UpdatedAt = DateTime.UtcNow; ev.ReviewedBy = admin;
        Notify(ev, "Compliance review: " + input.Decision, notes);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    private HttpClient TicketClient()
    {
        var client = http.CreateClient("GovernanceTicket"); return client;
    }
    public async Task<object> PreviewAsync(int id, int caller, bool admin, ChangeInput input, CancellationToken ct)
    {
        var ev = await GetAsync(id, caller, admin, false, ct); var clean = GovernanceRules.ValidateChange(ev, input, DateTime.UtcNow);
        using var response = await TicketClient().GetAsync($"api/internal/event-changes/preview/{id}", ct); response.EnsureSuccessStatusCode();
        var affected = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        return new { ev.EventId, ev.Title, ev.Timezone, ev.StartsAt, ev.EndsAt, Change = clean, Affected = affected, IsPreview = true };
    }
    public async Task<long> SubmitChangeAsync(int id, int caller, ChangeInput input, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var ev = await GetAsync(id, caller, false, true, ct); var clean = GovernanceRules.ValidateChange(ev, input, DateTime.UtcNow);
        if (await db.Set<EventChangeRequest>().AnyAsync(x => x.EventId == id && x.Status == "Pending", ct)) throw new GovernanceConflictException("A pending change already exists for this concert.");
        var request = new EventChangeRequest { EventId = id, Type = clean.Type, Reason = clean.Reason, OldStartsAt = ev.StartsAt, OldEndsAt = ev.EndsAt, NewStartsAt = clean.NewStartsAt?.UtcDateTime, NewEndsAt = clean.NewEndsAt?.UtcDateTime, OldStatus = ev.Status, EventVersion = ev.ScheduleVersion, SubmittedBy = caller, SubmittedAt = DateTime.UtcNow };
        db.Set<EventChangeRequest>().Add(request); await db.SaveChangesAsync(ct);
        // Báo cho admin biết có yêu cầu mới cần duyệt (danh sách admin cấu hình ở Governance:AdminUserIds).
        NotifyAdmins(ev, "Schedule change request: " + clean.Type, $"The organizer asked to {clean.Type.ToLowerInvariant()} \"{ev.Title}\". Please review it.");
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return request.Id;
    }
    public async Task<object> ChangesAsync(int id, int caller, bool admin, CancellationToken ct)
    {
        await GetAsync(id, caller, admin, false, ct);
        return await db.Set<EventChangeRequest>().AsNoTracking().Where(x => x.EventId == id).OrderByDescending(x => x.Id).ToListAsync(ct);
    }
    public async Task ReviewChangeAsync(int id, long requestId, int admin, ChangeReviewInput input, CancellationToken ct)
    {
        var notes = GovernanceRules.ValidateChangeReview(input.Decision, input.Notes);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var ev = await GetAsync(id, admin, true, true, ct);
        var request = await db.Set<EventChangeRequest>().SingleOrDefaultAsync(x => x.Id == requestId && x.EventId == id, ct) ?? throw new KeyNotFoundException("Change request not found.");
        if (request.Status != "Pending") throw new GovernanceConflictException("This change request has already been reviewed.");
        if (input.Decision == "Approved")
        {
            if (ev.ScheduleVersion != request.EventVersion || ev.StartsAt != request.OldStartsAt || ev.EndsAt != request.OldEndsAt || ev.Status != request.OldStatus)
                throw new GovernanceConflictException("The concert has changed. Reject this request and submit a new one.");
            GovernanceRules.ValidateChange(ev, new(request.Type, request.Reason, request.NewStartsAt.HasValue ? new DateTimeOffset(request.NewStartsAt.Value) : null, request.NewEndsAt.HasValue ? new DateTimeOffset(request.NewEndsAt.Value) : null), DateTime.UtcNow);
            if (request.Type == "Postpone") ev.Status = EventStatus.Postponed;
            else
            {
                ev.StartsAt = request.NewStartsAt!.Value; ev.EndsAt = request.NewEndsAt!.Value; ev.Status = EventStatus.Published;
                var types = await db.TicketTypes.Where(x => x.EventId == id && !x.IsDeleted).ToListAsync(ct);
                foreach (var type in types)
                {
                    if (type.SalesEndsAt > ev.StartsAt) type.SalesEndsAt = ev.StartsAt;
                    if (type.SalesStartsAt >= type.SalesEndsAt) throw new InvalidOperationException("Adjust ticket sale windows before applying this earlier schedule.");
                }
            }
            // Tạm đóng bán đến khi TicketAPI đã ghi snapshot và đồng bộ lịch; callback đã trả tiền vẫn được ghi nhận.
            ev.SalesFrozen = true; ev.ScheduleVersion++; ev.UpdatedAt = DateTime.UtcNow;
            request.ProcessingStatus = "Pending";
            Queue("ApplyChange", new ApplyChangeMessage(request.Id, id, request.Type, request.NewStartsAt, request.NewEndsAt, ev.ScheduleVersion, request.Reason, ev.Title, ev.Slug));
        }
        request.Status = input.Decision; request.ReviewedBy = admin; request.ReviewedAt = DateTime.UtcNow; request.ReviewNotes = notes;
        Notify(ev, "Schedule change: " + input.Decision, notes.Length > 0 ? notes : "The schedule change request was approved.");
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    // Admin list of change requests across all concerts (Report 3, 3.10.10 "Change requests" page), newest first.
    public async Task<object> AllChangesAsync(string? status, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(status) && status != "Pending" && status != "Approved" && status != "Rejected") throw new ArgumentException("Invalid status filter.");
        var query = from c in db.Set<EventChangeRequest>().AsNoTracking()
                    join e in db.Events.AsNoTracking() on c.EventId equals e.EventId
                    where string.IsNullOrEmpty(status) || c.Status == status
                    orderby c.Id descending
                    select new { c.Id, c.EventId, EventTitle = e.Title, e.OrganizerId, e.Timezone, EventStatus = e.Status, c.Type, c.Status, c.Reason, c.OldStartsAt, c.OldEndsAt, c.NewStartsAt, c.NewEndsAt, c.SubmittedAt, c.ReviewedBy, c.ReviewedAt, c.ReviewNotes, c.ProcessingStatus };
        return await query.Take(200).ToListAsync(ct);
    }
    public async Task<JsonElement> AffectedAsync(int id, long requestId, int caller, bool admin, string tab, string? status, string? orderCode, int? userId, int page, int pageSize, CancellationToken ct)
    {
        await GetAsync(id, caller, admin, false, ct);
        if (tab != "orders" && tab != "tickets") throw new ArgumentException("Invalid tab.");
        if (page < 1 || pageSize < 1 || pageSize > 100 || userId <= 0) throw new ArgumentException("Invalid paging or user ID.");
        var request = await db.Set<EventChangeRequest>().AsNoTracking().SingleOrDefaultAsync(x => x.EventId == id && x.Id == requestId, ct) ?? throw new KeyNotFoundException("Change request not found.");
        var route = request.Status == "Pending" ? $"preview/{id}" : request.Status == "Approved" ? $"{requestId}" : throw new InvalidOperationException("A rejected request has no applied affected records.");
        var query = $"?tab={tab}&page={page}&pageSize={pageSize}&status={Uri.EscapeDataString(status ?? "")}&orderCode={Uri.EscapeDataString(orderCode ?? "")}&userId={userId}";
        using var response = await TicketClient().GetAsync("api/internal/event-changes/" + route + query, ct);
        if ((int)response.StatusCode == 400) throw new ArgumentException("Invalid affected-record filter.");
        if ((int)response.StatusCode == 404) throw new GovernanceConflictException("Affected records are being synchronized. Retry shortly.");
        response.EnsureSuccessStatusCode(); return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
    }
    public async Task<SaleEligibility> EligibilityAsync(int id, CancellationToken ct)
    {
        var ev = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == id && !x.IsDeleted, ct) ?? throw new KeyNotFoundException("Concert not found.");
        var canSell = ev.Status == EventStatus.Published && !ev.SalesFrozen && GovernanceRules.ComplianceApproved(ev) && ev.StartsAt > DateTime.UtcNow;
        return new(canSell, canSell ? "" : "Sales are unavailable: check event status, schedule and compliance approval.", ev.Status, ev.StartsAt, ev.EndsAt, ev.ScheduleVersion);
    }
}
public class GovernanceConflictException(string message) : Exception(message) { }
