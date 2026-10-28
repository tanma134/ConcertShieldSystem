using IncidentAPI.DTOs;

namespace IncidentAPI.Services;

public interface IIncidentService
{
    // UC_16.1: Staff tạo báo cáo sự cố mới
    Task<IncidentDetailDto> CreateIncidentAsync(CreateIncidentRequest req, int reporterId, string reporterName, CancellationToken ct = default);

    // Lấy chi tiết sự cố
    Task<IncidentDetailDto> GetDetailAsync(int id, int requestingUserId, bool isAdmin, CancellationToken ct = default);

    // Lấy danh sách sự cố có lọc và phân trang
    Task<List<IncidentListItemDto>> SearchAsync(IncidentQuery query, int requestingUserId, bool isAdmin, CancellationToken ct = default);

    // UC_16.3: Cập nhật thông tin chi tiết sự cố
    Task<IncidentDetailDto> UpdateIncidentAsync(int id, UpdateIncidentRequest req, int updatedBy, string updaterName, bool isAdmin, CancellationToken ct = default);

    // Cập nhật nhanh trạng thái
    Task<IncidentDetailDto> UpdateStatusAsync(int id, UpdateStatusRequest req, int updatedBy, string updaterName, bool isAdmin, CancellationToken ct = default);

    // UC_16.4: Staff chuyển cấp / leo thang sự cố lên Admin
    Task<IncidentDetailDto> EscalateIncidentAsync(int id, EscalateIncidentRequest req, int staffId, string staffName, CancellationToken ct = default);

    // UC_16.5: Admin giải quyết sự cố
    Task<IncidentDetailDto> ResolveIncidentAsync(int id, ResolveIncidentRequest req, int adminId, string adminName, CancellationToken ct = default);

    // UC_16.5: Xem kết quả giải quyết
    Task<ResolutionDto> GetResolutionAsync(int id, CancellationToken ct = default);

    // Lấy lịch sử dòng thời gian (timeline updates)
    Task<List<IncidentUpdateDto>> GetHistoryAsync(int id, CancellationToken ct = default);
}
