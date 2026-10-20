using AdminAPI.DTOs;

namespace AdminAPI.Services;

// Service contract for Support Case Management and Dispute Resolution
public interface ISupportCaseService
{
    // Create a new customer support case with transaction context
    Task<SupportCaseDetailDto> CreateCaseAsync(CreateSupportCaseRequest req, int creatorId, string creatorName);

    // List support cases with role-based visibility scope
    Task<List<SupportCaseListDto>> GetCasesAsync(SupportCaseQuery query, int currentUserId, bool isAdmin);

    // View support case detail including notes timeline
    Task<SupportCaseDetailDto> GetCaseDetailAsync(int caseId, int currentUserId, bool isAdmin);

    // Assign support case to eligible staff (Admin only)
    Task<SupportCaseDetailDto> AssignStaffAsync(int caseId, AssignStaffRequest req, int adminId, string adminName);

    // Resolve and close support case with mandatory resolution
    Task<SupportCaseDetailDto> ResolveCaseAsync(int caseId, ResolveCaseRequest req, int resolverId, string resolverName, string role);

    // Append internal investigation or communication note to case timeline
    Task<SupportCaseNoteDto> AddNoteAsync(int caseId, AddSupportCaseNoteRequest req, int authorId, string authorName, string authorRole);

    // Get the latest active or completed support case linked to an order
    Task<SupportCaseDetailDto?> GetCaseByOrderIdAsync(int orderId);
}
