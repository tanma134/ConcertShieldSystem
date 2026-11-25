using AdminAPI.DTOs;
using AdminAPI.Repositories;

namespace AdminAPI.Services;

/// <summary>
/// Governance, policy versioning & compliance. Truy vấn nằm ở
/// <see cref="ISystemPolicyRepository"/>; service chỉ chuẩn hoá danh mục,
/// sinh mã phiên bản và ánh xạ DTO.
/// </summary>
public interface ISystemPolicyService
{
    Task<List<PolicyVersionDTO>> GetHistoryAsync(string? category, CancellationToken ct = default);

    Task<Dictionary<string, PolicyVersionDTO>> GetActiveAsync(CancellationToken ct = default);

    Task<PolicyVersionDTO> CreateAsync(CreatePolicyVersionRequestDTO request, int adminId, string adminName,
        CancellationToken ct = default);
}

public sealed class SystemPolicyService(ISystemPolicyRepository repository) : ISystemPolicyService
{
    private const string DefaultScope = "All Platform Attendees & Event Organizers";

    public async Task<List<PolicyVersionDTO>> GetHistoryAsync(string? category, CancellationToken ct = default)
    {
        var normalized = NormalizeCategory(category);
        var rows = await repository.GetHistoryAsync(normalized, ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<Dictionary<string, PolicyVersionDTO>> GetActiveAsync(CancellationToken ct = default)
    {
        var rows = await repository.GetActiveAsync(ct);
        return rows.GroupBy(r => r.Category).ToDictionary(g => g.Key, g => ToDto(g.First()));
    }

    public async Task<PolicyVersionDTO> CreateAsync(CreatePolicyVersionRequestDTO request, int adminId,
        string adminName, CancellationToken ct = default)
    {
        var category = NormalizeCategory(request.Category);
        var effectiveDate = request.EffectiveDate?.ToUniversalTime() ?? DateTime.UtcNow;
        var feePercent = request.PlatformFeePercent ?? 5.0m;
        var payoutDays = request.PayoutDays ?? 7;
        var nextVersion = await repository.GetNextVersionNumberAsync(category, ct);

        // Generate version code (e.g. POL-PRICE-V2.0)
        var versionCode = $"POL-{PrefixFor(category)}-V{nextVersion}.0";
        var scope = string.IsNullOrWhiteSpace(request.Scope) ? DefaultScope : request.Scope.Trim();

        var (policyId, createdAt) = await repository.PublishAsync(new NewSystemPolicyVersion(
            category,
            versionCode,
            nextVersion,
            request.Title.Trim(),
            scope,
            request.ChangeSummary.Trim(),
            request.Content.Trim(),
            effectiveDate,
            adminId,
            adminName,
            feePercent,
            payoutDays), ct);

        return new PolicyVersionDTO
        {
            PolicyId = policyId,
            Category = category,
            VersionCode = versionCode,
            VersionNumber = nextVersion,
            Title = request.Title.Trim(),
            Scope = scope,
            ChangeSummary = request.ChangeSummary.Trim(),
            Content = request.Content.Trim(),
            PlatformFeePercent = feePercent,
            PayoutDays = payoutDays,
            Status = "Active",
            EffectiveDate = effectiveDate,
            CreatedAt = createdAt,
            CreatedByAdminId = adminId,
            CreatedByAdminName = adminName
        };
    }

    public static string NormalizeCategory(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "PRICE_TICKETING";
        var upper = raw.Trim().ToUpperInvariant();
        if (upper.Contains("PRICE")) return "PRICE_TICKETING";
        if (upper.Contains("REFUND")) return "REFUND_CANCELLATION";
        if (upper.Contains("QUEUE")) return "VIRTUAL_QUEUE";
        if (upper.Contains("RISK")) return "RISK_ANTI_FRAUD";
        if (upper.Contains("TERMS") || upper.Contains("TOS")) return "TERMS_OF_SERVICE";
        return "PRICE_TICKETING";
    }

    private static string PrefixFor(string category) => category switch
    {
        "PRICE_TICKETING" => "PRICE",
        "REFUND_CANCELLATION" => "REFUND",
        "VIRTUAL_QUEUE" => "QUEUE",
        "RISK_ANTI_FRAUD" => "RISK",
        _ => "TOS"
    };

    private static PolicyVersionDTO ToDto(SystemPolicyRecord r) => new()
    {
        PolicyId = r.PolicyId,
        Category = r.Category,
        VersionCode = r.VersionCode,
        VersionNumber = r.VersionNumber,
        Title = r.Title,
        Scope = r.Scope,
        ChangeSummary = r.ChangeSummary,
        Content = r.Content,
        PlatformFeePercent = r.PlatformFeePercent,
        PayoutDays = r.PayoutDays,
        Status = r.Status,
        EffectiveDate = r.EffectiveDate,
        CreatedAt = r.CreatedAt,
        CreatedByAdminId = r.CreatedByAdminId,
        CreatedByAdminName = r.CreatedByAdminName
    };
}
