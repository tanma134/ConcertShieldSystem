using EventAPI.DTOs;
using EventAPI.Models;
namespace EventAPI.Common;

public static class GovernanceRules
{
    // Chuan hoa du lieu va kiem tra tinh hop le cua yeu cau doi lich o phia backend
    public static ChangeInput ValidateChange(Event ev, ChangeInput input, DateTime now)
    {
        // Luu y: Chi su kien Published hoac Postponed moi duoc phep thay doi lich trinh
        var status = EventStatus.Normalize(ev.Status);
        if (status != EventStatus.Published && status != EventStatus.Postponed)
            throw new InvalidOperationException("Only Published or Postponed concerts support schedule changes.");
        if (status == EventStatus.Published && ev.StartsAt <= now)
            throw new InvalidOperationException("A concert that has started cannot change its schedule.");
        if (input.Type != "Reschedule" && input.Type != "Postpone")
            throw new ArgumentException("Change type must be Postpone or Reschedule.");
        if (status == EventStatus.Postponed && input.Type != "Reschedule")
            throw new InvalidOperationException("A postponed concert needs a new schedule.");
        var reason = input.Reason?.Trim() ?? "";
        if (reason.Length < 10 || reason.Length > 1000)
            throw new ArgumentException("Reason must contain 10 to 1000 characters.");
        if (input.Type == "Postpone") return input with { Reason = reason, NewStartsAt = null, NewEndsAt = null };
        if (!input.NewStartsAt.HasValue || !input.NewEndsAt.HasValue)
            throw new ArgumentException("New start and end are required.");
        var start = input.NewStartsAt.Value.UtcDateTime;
        var end = input.NewEndsAt.Value.UtcDateTime;
        // Luu y: Reschedule bat buoc thoi gian bat dau moi phai o tuong lai va ket thuc sau bat dau
        if (start <= now || end <= start)
            throw new ArgumentException("New start must be in the future and end must be after start.");
        if (status != EventStatus.Postponed && start == ev.StartsAt && end == ev.EndsAt)
            throw new ArgumentException("The requested schedule has not changed.");
        return input with { Reason = reason };
    }

    // Kiem tra tinh hop le cua quyet dinh tham dinh ho so phap ly
    public static string ValidateReview(string decision, string? notes, bool compliance)
    {
        var allowed = compliance ? new[] { "Approved", "Rejected", "RequestMoreInfo" } : new[] { "Approved", "Rejected" };
        if (!allowed.Contains(decision)) throw new ArgumentException("Invalid review decision.");
        var clean = notes?.Trim() ?? "";
        if (clean.Length == 0 || clean.Length > 2000) throw new ArgumentException("Review notes must contain 1 to 2000 characters.");
        return clean;
    }

    // Kiem tra bo ho so da day du cac loai giay to phap ly theo yeu cau
    public static bool IsComplete(int version, IEnumerable<string> required, IEnumerable<string> present)
        => version > 0 && !required.Except(present).Any();

    // Kiem tra tinh hop le cua quyet dinh phe duyet va ghi chu cua Admin khi doi lich
    public static string ValidateChangeReview(string decision, string? notes)
    {
        // Luu y: Quyet dinh bat buoc la Approved hoac Rejected, tu choi phai co ghi chu toi thieu 10 ky tu
        if (decision != "Approved" && decision != "Rejected") throw new ArgumentException("Invalid review decision.");
        var clean = notes?.Trim() ?? "";
        if (clean.Length > 1000) throw new ArgumentException("Admin note must contain at most 1000 characters.");
        if (decision == "Rejected" && clean.Length < 10) throw new ArgumentException("Please enter a note of at least 10 characters when rejecting.");
        return clean;
    }

    // Kiem tra concert da duoc Admin phe duyet day du ho so phap ly hay chua
    public static bool ComplianceApproved(Event ev) => ev.ComplianceVersion > 0 &&
        ev.ComplianceStatus == "Approved" && ev.ComplianceReviewedVersion == ev.ComplianceVersion;

    // Kiem tra chu ky file va dinh dang PDF PNG JPEG hop le truoc khi xu ly
    public static string ValidateFile(string name, string mime, byte[] bytes, long maxBytes)
    {
        // Luu y: Gioi han dung luong toi da 20MB va kiem tra magic bytes de ngan chan ma doc
        if (bytes.Length == 0 || bytes.LongLength > maxBytes) throw new ArgumentException("File is empty or exceeds the maximum size.");
        var ext = Path.GetExtension(name).ToLowerInvariant();
        var pdf = bytes.Length >= 5 && bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8);
        var png = bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10});
        var jpg = bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255;
        if (ext == ".pdf" && pdf && mime == "application/pdf") return "application/pdf";
        if (ext == ".png" && png && mime == "image/png") return "image/png";
        if ((ext == ".jpg" || ext == ".jpeg") && jpg && mime == "image/jpeg") return "image/jpeg";
        throw new ArgumentException("Only PDF, PNG and JPEG with matching content and MIME type are supported.");
    }
}
