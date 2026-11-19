using EventAPI.Models;
using Microsoft.EntityFrameworkCore.Storage;

namespace EventAPI.Repositories;

// Khai bao cac phuong thuc truy van va thao tac du lieu lien quan den quan tri va thay doi su kien
public interface IGovernanceRepository
{
    // Lay thong tin su kien theo khoa chinh va tuy chon khoa dong FOR UPDATE trong transaction
    Task<Event?> GetEventByIdAsync(int id, bool locked, CancellationToken ct);

    // Lay thong tin su kien che do khong theo doi de toi uu toc do doc
    Task<Event?> GetEventAsNoTrackingAsync(int id, CancellationToken ct);

    // Kiem tra su kien co yeu cau thay doi nao dang o trang thai cho duyet hay khong
    Task<bool> HasPendingChangeRequestAsync(int eventId, CancellationToken ct);

    // Them moi mot yeu cau thay doi lich hoan hoac doi ngay vao co so du lieu
    Task<EventChangeRequest> AddChangeRequestAsync(EventChangeRequest request, CancellationToken ct);

    // Lay chi tiet mot yeu cau thay doi lich theo ma su kien va ma yeu cau
    Task<EventChangeRequest?> GetChangeRequestByIdAsync(int eventId, long requestId, CancellationToken ct);

    // Lay danh sach tat ca cac yeu cau thay doi lich cua mot su kien sap xep moi nhat truoc
    Task<List<EventChangeRequest>> GetChangeRequestsByEventIdAsync(int eventId, CancellationToken ct);

    // Lay danh sach yeu cau thay doi lich tren toan he thong co loc theo trang thai cho Admin
    Task<List<object>> GetAllChangeRequestsAsync(string? status, CancellationToken ct);

    // Lay danh sach cac loai ve dang hoat dong cua su kien
    Task<List<TicketType>> GetActiveTicketTypesAsync(int eventId, CancellationToken ct);

    // Lay danh sach tai lieu phap ly cua su kien sap xep giam dan theo phien ban
    Task<List<ComplianceDocument>> GetComplianceDocumentsAsync(int eventId, CancellationToken ct);

    // Lay chi tiet mot tai lieu phap ly theo ma tai lieu va ma su kien
    Task<ComplianceDocument?> GetComplianceDocumentByIdAsync(int eventId, long documentId, CancellationToken ct);

    // Lay danh sach cac quyet dinh tham dinh ho so phap ly cua su kien
    Task<List<ComplianceReview>> GetComplianceReviewsAsync(int eventId, CancellationToken ct);

    // Them moi mot tai lieu phap ly vao co so du lieu
    Task AddComplianceDocumentAsync(ComplianceDocument doc, CancellationToken ct);

    // Them moi danh sach cac tai lieu phap ly sao chep sang phien ban ho so moi
    Task AddComplianceDocumentsAsync(IEnumerable<ComplianceDocument> docs, CancellationToken ct);

    // Them moi mot ban ghi tham dinh ho so phap ly cua Admin
    Task AddComplianceReviewAsync(ComplianceReview review, CancellationToken ct);

    // Ghi them ban ghi vao hang doi outbox de xu ly bat dong bo
    void AddOutbox(GovernanceOutbox outbox);

    // Khoi tao transaction de dam bao tinh toan ven du lieu
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct);

    // Luu toan bo cac thay doi tren context vao co so du lieu
    Task<int> SaveChangesAsync(CancellationToken ct);

    // Xoa sach bo nho theo doi change tracker khi gap su co
    void ClearChangeTracker();
}
