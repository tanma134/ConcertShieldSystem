using TicketAPI.Models;

namespace TicketAPI.Repositories
{
    public interface ITicketReturnRepository
    {
        Task<List<Ticket>> GetPaidTicketsByOwnerAsync(int userId);
        Task<List<TicketReturnRequest>> GetRequestsByTicketIdsAsync(IEnumerable<int> ticketIds);
        Task<Dictionary<int, AppliedEventChange>> GetLatestChangesByTicketIdsAsync(IEnumerable<int> ticketIds);
        Task<AppliedEventChange?> GetLatestChangeForTicketAsync(int ticketId);
        Task<Ticket?> GetTicketForReturnAsync(int ticketId, int userId);
        Task<List<Ticket>> GetTicketsForReturnAsync(IEnumerable<int> ticketIds, int userId);
        Task<DateTime?> GetLatestRequestCreatedAtByUserAsync(int userId);
        Task<bool> HasPendingRequestForTicketAsync(int ticketId);
        Task AddRequestAsync(TicketReturnRequest request);
        Task AddRequestsAsync(IEnumerable<TicketReturnRequest> requests);
        Task<List<TicketReturnRequest>> ListMineAsync(int userId, string? status);
        Task<TicketReturnRequest?> GetMineAsync(int userId, int requestId);
        Task<TicketReturnRequest?> GetMineForCancelAsync(int userId, int requestId);
        Task<List<TicketReturnRequest>> ListForReviewAsync(string? status, IReadOnlyCollection<int>? eventScope = null);
        Task<TicketReturnRequest?> GetForReviewAsync(int requestId);
        Task SyncOrderRefundStatusAsync(int orderId);
        Task SaveChangesAsync();
    }
}

