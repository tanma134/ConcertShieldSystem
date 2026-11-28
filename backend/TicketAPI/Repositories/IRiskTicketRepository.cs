using TicketAPI.Models;

namespace TicketAPI.Repositories
{
    public interface IRiskTicketRepository
    {
        // Read-only lookup (no tracking).
        Task<Ticket?> FindTicketAsync(int ticketId, CancellationToken ct = default);

        // Tracked ticket, used for block/unblock.
        Task<Ticket?> GetTicketAsync(int ticketId, CancellationToken ct = default);

        // Tracked ticket with order, order details and QR tokens, used for revoke + refund.
        Task<Ticket?> GetTicketForRevokeAsync(int ticketId, CancellationToken ct = default);

        Task<bool> HasPendingReturnRequestAsync(int ticketId, CancellationToken ct = default);

        Task<TicketReturnRequest?> GetLatestReturnRequestAsync(int ticketId, CancellationToken ct = default);

        void AddReturnRequest(TicketReturnRequest request);

        Task SaveChangesAsync(CancellationToken ct = default);
    }
}
