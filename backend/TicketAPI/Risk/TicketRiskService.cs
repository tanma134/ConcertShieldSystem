using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TicketAPI.DTOs;
using TicketAPI.Models;

namespace TicketAPI.Risk;

public interface ITicketRiskService
{
    Task<RiskVerdict> CheckHoldAsync(HttpContext http, int userId, CreateHoldSessionRequest req, CancellationToken ct = default);
    Task<RiskVerdict> CheckOrderAsync(HttpContext http, int userId, CreateOrderDTO dto, CancellationToken ct = default);
    Task<RiskVerdict> CheckReturnAsync(HttpContext http, int userId, int? ticketId, CancellationToken ct = default);
}

/// <summary>
/// Tính các rule gian lận dựa trên DB của TicketAPI rồi gửi sang AdminAPI.
/// Rule: FRD_T01, FRD_T02, FRD_T03, FRD_A01 (cần claim account_created_at), BOT_V02, BOT_B01, TRU_01
/// + rule bot từ header/tốc độ (BotRuleDetector).
/// </summary>
public sealed class TicketRiskService : ITicketRiskService
{
    // Ngưỡng - sửa ở đây cho khớp bảng tham số AdminAPI (mục 12 tài liệu: > hay >=)
    private const int MaxTicketsPerEvent = 6;            // FRD_T01: mua > 6 vé / sự kiện / tài khoản
    private const int ReturnsThreshold = 3;              // FRD_T02: >= 3 lần hoàn/hủy trong 7 ngày
    private static readonly TimeSpan ReturnsWindow = TimeSpan.FromDays(7);
    private const int AccountsPerContact = 3;            // FRD_T03: >= 3 tài khoản nhận vé về cùng email/SĐT / 24h
    private static readonly TimeSpan ContactWindow = TimeSpan.FromHours(24);
    private const int NewAccountMinTickets = 4;          // FRD_A01: tài khoản < 24h tuổi mua >= 4 vé
    private const double MinSelectToSubmitSeconds = 3;   // BOT_V02
    private const int HoldDurationSeconds = 600;         // phải bằng durationSeconds trong HoldsController

    private readonly TicketDbContext _db;
    private readonly IRiskGuard _guard;
    private readonly IHoldService _holds;
    private readonly ILogger<TicketRiskService> _logger;

    public TicketRiskService(TicketDbContext db, IRiskGuard guard, IHoldService holds, ILogger<TicketRiskService> logger)
    { _db = db; _guard = guard; _holds = holds; _logger = logger; }

    public async Task<RiskVerdict> CheckHoldAsync(HttpContext http, int userId, CreateHoldSessionRequest req, CancellationToken ct = default)
    {
        var pre = await _guard.PreCheckAsync(http, userId, ct);
        if (!pre.Allowed) return pre;

        var evt = new RiskEvent { UserId = userId, EventId = req.EventId };
        try
        {
            var incoming = Math.Max(req.Tickets.Sum(t => t.Quantity), req.SeatIds.Count);
            await AddTicketRulesAsync(http, evt, userId, req.EventId, incoming, ct);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Risk rule calculation failed for hold (ignored)"); }

        return await _guard.EvaluateAsync(http, evt, isBooking: true, ct);
    }

    public async Task<RiskVerdict> CheckOrderAsync(HttpContext http, int userId, CreateOrderDTO dto, CancellationToken ct = default)
    {
        var pre = await _guard.PreCheckAsync(http, userId, ct);
        if (!pre.Allowed) return pre;

        var evt = new RiskEvent { UserId = userId, EventId = dto.EventId };
        var now = DateTime.UtcNow;
        try
        {
            var incoming = Math.Max(dto.OrderDetails.Sum(d => d.Quantity), dto.OrderDetails.Sum(d => d.SeatIds.Count));
            await AddTicketRulesAsync(http, evt, userId, dto.EventId, incoming, ct);

            // FRD_T03: nhiều tài khoản cùng email/SĐT nhận vé trong 24h
            var since = now - ContactWindow;
            var email = dto.Email?.Trim().ToLower();
            var phone = dto.Phone?.Trim();
            var hasEmail = !string.IsNullOrEmpty(email);
            var hasPhone = !string.IsNullOrEmpty(phone);
            if (hasEmail || hasPhone)
            {
                var accounts = await _db.Orders.AsNoTracking()
                    .Where(o => !o.IsDeleted && o.OrderDate >= since
                        && ((hasEmail && o.Email != null && o.Email.ToLower() == email) || (hasPhone && o.Phone == phone)))
                    .Select(o => o.CustomerId).Distinct().ToListAsync(ct);
                if (!accounts.Contains(userId)) accounts.Add(userId);
                if (accounts.Count >= AccountsPerContact)
                    evt.FraudRules.Add(new RuleHit("FRD_T03", 1, $"{accounts.Count} accounts share {(hasEmail ? email : phone)}"));
            }

            // BOT_B01 (xấp xỉ): vào thẳng bước tạo đơn mà không có hold session (chưa qua bước chọn vé/sơ đồ ghế)
            if (string.IsNullOrWhiteSpace(dto.HoldId))
                evt.BotRules.Add(new RuleHit("BOT_B01", 1, "order created without a hold session"));
            else
            {
                // BOT_V02 (xấp xỉ): từ lúc giữ chỗ (chọn ghế) đến lúc submit đơn < 3 giây
                try
                {
                    var hold = await _holds.GetHoldDetailsAsync(dto.HoldId, userId);
                    var heldAt = hold.ExpiresAtUtc.AddSeconds(-HoldDurationSeconds);
                    var elapsed = (now - heldAt).TotalSeconds;
                    if (elapsed >= 0 && elapsed < MinSelectToSubmitSeconds)
                        evt.BotRules.Add(new RuleHit("BOT_V02", 1, $"{elapsed:0.0}s from hold to submit"));
                }
                catch (Exception ex) { _logger.LogDebug(ex, "Could not read hold {HoldId} for BOT_V02", dto.HoldId); }
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Risk rule calculation failed for order (ignored)"); }

        return await _guard.EvaluateAsync(http, evt, isBooking: true, ct);
    }

    public async Task<RiskVerdict> CheckReturnAsync(HttpContext http, int userId, int? ticketId, CancellationToken ct = default)
    {
        var evt = new RiskEvent { UserId = userId, TicketId = ticketId };
        try
        {
            var since = DateTime.UtcNow - ReturnsWindow;
            var n = await CountRecentReturnsAsync(userId, since, ct) + 1;   // +1 = yêu cầu đang gửi
            if (n >= ReturnsThreshold) evt.FraudRules.Add(new RuleHit("FRD_T02", 1, $"{n} returns/refunds in 7 days"));
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Risk rule calculation failed for return (ignored)"); }
        return await _guard.EvaluateAsync(http, evt, isBooking: false, ct);
    }

    // ------------------------------------------------------------------ helpers

    private async Task AddTicketRulesAsync(HttpContext http, RiskEvent evt, int userId, int eventId, int incoming, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // FRD_T01: mua > 6 vé / sự kiện / tài khoản (đã mua + đang giữ + lần này)
        var existing = await (
            from o in _db.Orders.AsNoTracking()
            join d in _db.OrderDetails.AsNoTracking() on o.OrderId equals d.OrderId
            where o.CustomerId == userId && o.EventId == eventId && !o.IsDeleted
                  && (o.Status == "Paid" || (o.Status == "Pending" && (o.ExpiresAt == null || o.ExpiresAt > now)))
            select d.Quantity).SumAsync(ct);
        if (existing + incoming > MaxTicketsPerEvent)
            evt.FraudRules.Add(new RuleHit("FRD_T01", 1, $"{existing + incoming} tickets for event {eventId}"));

        // FRD_T02: mua rồi hủy/hoàn >= 3 lần trong 7 ngày
        var returns = await CountRecentReturnsAsync(userId, now - ReturnsWindow, ct);
        if (returns >= ReturnsThreshold)
            evt.FraudRules.Add(new RuleHit("FRD_T02", 1, $"{returns} returns/refunds in 7 days"));

        // Cần AuthenticationAPI thêm claim "account_created_at" vào JWT (xem README). Không có claim => bỏ qua.
        var createdText = http.User.FindFirst("account_created_at")?.Value;
        if (DateTime.TryParse(createdText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var created))
        {
            var age = now - created.ToUniversalTime();
            // FRD_A01: tài khoản < 24h tuổi mua >= 4 vé (nhánh "đơn >= 5.000.000đ" cần giá, chưa biết ở bước này)
            if (age < TimeSpan.FromHours(24) && incoming >= NewAccountMinTickets)
                evt.FraudRules.Add(new RuleHit("FRD_A01", 1, $"account {age.TotalHours:0.#}h old, {incoming} tickets"));

            // TRU_01: tài khoản > 6 tháng, >= 3 giao dịch thành công
            if (age > TimeSpan.FromDays(183))
            {
                var paid = await _db.Orders.AsNoTracking().CountAsync(o => o.CustomerId == userId && !o.IsDeleted && o.Status == "Paid", ct);
                if (paid >= 3) evt.TrustRules.Add(new RuleHit("TRU_01", 1, $"{paid} paid orders"));
            }
        }
        else evt.SignalsMissing.Add("account_created_at");
    }

    private Task<int> CountRecentReturnsAsync(int userId, DateTime since, CancellationToken ct) =>
        _db.TicketReturnRequests.AsNoTracking().CountAsync(r =>
            r.RequesterUserId == userId && r.CreatedAt >= since
            && r.Status != "Rejected" && r.Status != "Cancelled", ct);
}
