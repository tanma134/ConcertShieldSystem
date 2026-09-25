using EventAPI.Models;

namespace EventAPI.Repositories
{
    public interface ITicketTypeRepository
    {
        Task<List<TicketType>> GetByEventIdAsync(int eventId);
        Task<TicketType?> GetByIdAsync(int id);
        Task<bool> ExistsNameAsync(int eventId, string typeName, int? excludeId = null);
        Task<TicketType> CreateAsync(TicketType entity);
        Task UpdateAsync(TicketType entity);
        Task SoftDeleteAsync(int id, int deletedBy);

        // Atomically consumes <paramref name="quantity"/> units of inventory in a single
        // conditional UPDATE (SoldQuantity += qty WHERE SoldQuantity + qty &lt;= Quantity).
        // Two concurrent callers racing for the last seat/ticket can never both succeed:
        // Postgres row-locks the target row for the duration of the UPDATE, so the second
        // writer waits, re-evaluates the WHERE clause against the already-committed value,
        // and correctly fails. Returns false (no row updated) on insufficient inventory,
        // missing/deleted ticket type, or a non-Active ticket type — never throws for that.

        Task<bool> TryReserveAsync(int ticketTypeId, int quantity);

        // Atomically releases previously-reserved/sold inventory (hold timeout, payment
        // failure, refund, event cancellation). Clamps at 0 so a duplicate/late release
        // (e.g. a retried webhook) can never drive SoldQuantity negative.

        Task<bool> ReleaseAsync(int ticketTypeId, int quantity);
    }
}
