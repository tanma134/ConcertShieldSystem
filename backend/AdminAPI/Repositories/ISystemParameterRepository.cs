using AdminAPI.Models;

namespace AdminAPI.Repositories;

public interface ISystemParameterRepository
{
    /// <summary>Các tham số thuộc danh mục cấu hình rủi ro, chỉ đọc (dùng cho RiskConfigProvider).</summary>
    Task<List<(string Key, string Value)>> GetKeyValuesAsync(string[] categories, CancellationToken ct = default);

    /// <summary>Danh sách tham số đã sắp xếp theo category rồi param_key (dùng cho màn hình cấu hình).</summary>
    Task<List<SystemParameter>> GetOrderedAsync(string[] categories, string? category, CancellationToken ct = default);

    /// <summary>Các tham số có theo dõi thay đổi, dùng để cập nhật (dùng cho RiskPolicyService).</summary>
    Task<List<SystemParameter>> GetForUpdateAsync(string[] categories);

    Task SaveChangesAsync();
}
