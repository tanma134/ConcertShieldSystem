using PaymentAPI.Data;
using PaymentAPI.Models;
using PaymentAPI.Repositories;

namespace PaymentAPI.Risk;

public interface IPaymentRiskService
{
    Task OnPaymentFailedAsync(PaymentTransaction tx, CancellationToken ct = default);
}

// Implements FRD_P01 (>= 3 failed payments in 10 min per account).
// FRD_P02..P04 are omitted because VNPay collects card details off-site.
public sealed class PaymentRiskService : IPaymentRiskService
{
    private const int FailedThreshold = 3;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private readonly IPaymentTransactionRepository _transactions;
    private readonly IRiskClient _client;
    private readonly IConfiguration _cfg;
    private readonly ILogger<PaymentRiskService> _logger;

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public PaymentRiskService(IPaymentTransactionRepository transactions, IRiskClient client, IConfiguration cfg, ILogger<PaymentRiskService> logger)
    { _transactions = transactions; _client = client; _cfg = cfg; _logger = logger; }

    public PaymentRiskService(PaymentDbContext db, IRiskClient client, IConfiguration cfg, ILogger<PaymentRiskService> logger)
        : this(new PaymentTransactionRepository(db), client, cfg, logger)
    { }

    public async Task OnPaymentFailedAsync(PaymentTransaction tx, CancellationToken ct = default)
    {
        try
        {
            if (!_cfg.GetValue("Risk:Enabled", true) || tx.UserId == null) return;

            var since = DateTime.UtcNow - Window;
            var failed = await _transactions.CountFailedSinceAsync(tx.UserId.Value, since, ct);
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

            // VNPay callback has no waiting user, so only record here;
            // blocking takes effect on the next TicketAPI PreCheckAsync.
            await _client.IngestAsync(evt, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Risk evaluation after failed payment {OrderId} failed (ignored)", tx.OrderId);
        }
    }

    private static bool IsValidIp(string? ip) => System.Net.IPAddress.TryParse(ip, out _);
}
