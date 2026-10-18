using Microsoft.EntityFrameworkCore;
using PaymentAPI.Data;
using PaymentAPI.Models;

namespace PaymentAPI.Risk;

public interface IPaymentRiskService
{
    /// <summary>Gọi sau khi một giao dịch VNPay được ghi nhận là Failed.</summary>
    Task OnPaymentFailedAsync(PaymentTransaction tx, CancellationToken ct = default);
}

/// <summary>
/// FRD_P01: thanh toán thất bại >= 3 lần trong 10 phút (theo tài khoản).
/// Không làm được với VNPay (khách nhập thẻ trên trang VNPay, hệ thống không nhận số thẻ/hash):
/// FRD_P02, FRD_P03, FRD_P04. Xem README.
/// </summary>
public sealed class PaymentRiskService : IPaymentRiskService
{
    private const int FailedThreshold = 3;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private readonly PaymentDbContext _db;
    private readonly IRiskClient _client;
    private readonly IConfiguration _cfg;
    private readonly ILogger<PaymentRiskService> _logger;

    public PaymentRiskService(PaymentDbContext db, IRiskClient client, IConfiguration cfg, ILogger<PaymentRiskService> logger)
    { _db = db; _client = client; _cfg = cfg; _logger = logger; }

    public async Task OnPaymentFailedAsync(PaymentTransaction tx, CancellationToken ct = default)
    {
        try
        {
            if (!_cfg.GetValue("Risk:Enabled", true) || tx.UserId == null) return;

            var since = DateTime.UtcNow - Window;
            var failed = await _db.PaymentTransactions.AsNoTracking().CountAsync(t =>
                t.UserId == tx.UserId && t.Status == "Failed" && t.RespondedAt != null && t.RespondedAt >= since, ct);
            if (failed < FailedThreshold) return;

            var evt = new RiskEvent
            {
                UserId = tx.UserId,
                OrderId = tx.OrderId,
                SessionId = tx.SessionId,
                DeviceFp = tx.DeviceFp,
                IpAddress = IsValidIp(tx.ClientIp) ? tx.ClientIp : null,
                OrderAmount = tx.Amount
            };
            evt.FraudRules.Add(new RuleHit("FRD_P01", 1, $"{failed} failed payments in 10 minutes"));
            if (tx.DeviceFp == null) evt.SignalsMissing.Add("device_fp");

            // Đây là callback từ VNPay (không có người dùng đợi) nên chỉ ghi nhận.
            // Việc chặn thật xảy ra lần sau khi user giữ vé/tạo đơn (PreCheckAsync ở TicketAPI).
            await _client.IngestAsync(evt, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Risk evaluation after failed payment {OrderId} failed (ignored)", tx.OrderId);
        }
    }

    private static bool IsValidIp(string? ip) => System.Net.IPAddress.TryParse(ip, out _);
}
