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

    Task<PagedResult<PolicyVersionDTO>> GetPolicyVersionsAsync(
        string? category = null,
        string? status = null,
        int page = 1,
        int pageSize = 10,
        string callerRole = "Admin",
        CancellationToken ct = default);

    Task<PolicyVersionDTO> CreatePolicyVersionAsync(
        CreatePolicyVersionRequestDTO request,
        int adminId = 1,
        string adminName = "Admin",
        string callerRole = "Admin",
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

    public async Task<PagedResult<PolicyVersionDTO>> GetPolicyVersionsAsync(
        string? category = null,
        string? status = null,
        int page = 1,
        int pageSize = 10,
        string callerRole = "Admin",
        CancellationToken ct = default)
    {
        if (!string.Equals(callerRole, "Admin", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Admin access required");

        if (page < 1)
            throw new ArgumentException("Page must be >= 1");

        if (pageSize < 1 || pageSize > 50)
            throw new ArgumentException("PageSize must be between 1 and 50");

        if (!string.IsNullOrWhiteSpace(category) && !IsValidCategory(category))
            throw new ArgumentException("Invalid policy category");

        var normalized = NormalizeCategory(category);
        var rows = await repository.GetHistoryAsync(normalized, ct);
        var filtered = rows.Select(ToDto).ToList();

        if (!string.IsNullOrWhiteSpace(status))
        {
            filtered = filtered
                .Where(p => string.Equals(p.Status, status.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var total = filtered.Count;
        var paged = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<PolicyVersionDTO>
        {
            Items = paged,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<PolicyVersionDTO> CreatePolicyVersionAsync(
        CreatePolicyVersionRequestDTO request,
        int adminId = 1,
        string adminName = "Admin",
        string callerRole = "Admin",
        CancellationToken ct = default)
    {
        if (!string.Equals(callerRole, "Admin", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Admin role required");

        if (request.VersionNumber != null && string.IsNullOrWhiteSpace(request.VersionNumber))
            throw new ArgumentException("Version number is required");

        if (!string.IsNullOrWhiteSpace(request.RulesJson))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(request.RulesJson);
            }
            catch (System.Text.Json.JsonException)
            {
                throw new ArgumentException("RulesJson must be valid JSON");
            }
        }

        if (request.EffectiveDate.HasValue && request.EffectiveDate.Value.ToUniversalTime() < DateTime.UtcNow.AddMinutes(-1))
            throw new ArgumentException("EffectiveDate cannot be in the past");

        var category = NormalizeCategory(request.Category);
        if (!string.IsNullOrWhiteSpace(request.VersionNumber))
        {
            var existing = await repository.GetHistoryAsync(category, ct);
            if (existing.Any(r =>
                    string.Equals(r.VersionCode, request.VersionNumber.Trim(), StringComparison.OrdinalIgnoreCase) ||
                    r.VersionCode.EndsWith($"V{request.VersionNumber.Trim()}", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Policy version already exists");
            }
        }

        if (string.IsNullOrWhiteSpace(request.Title))
            request.Title = $"Policy {category} {request.VersionNumber ?? "v1"}";
        if (string.IsNullOrWhiteSpace(request.ChangeSummary))
            request.ChangeSummary = $"Updated {category} rules";
        if (string.IsNullOrWhiteSpace(request.Content))
            request.Content = request.RulesJson ?? "Standard policy content";

        return await CreateAsync(request, adminId, adminName, ct);
    }

    public static bool IsValidCategory(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return true;
        var upper = raw.Trim().ToUpperInvariant();
        return upper.Contains("PRICE") ||
               upper.Contains("REFUND") ||
               upper.Contains("QUEUE") ||
               upper.Contains("RISK") ||
               upper.Contains("TERMS") ||
               upper.Contains("TOS");
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
