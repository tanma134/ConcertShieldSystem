using System.Text.Json;
using BookingAPI.DTOS;
using static TicketAPI.DTOs.ConfirmEventSaleDto;

namespace TicketAPI.API
{
    public interface IEventApiClient
    {
        Task<EventApiResponseDto?> GetEventByIdAsync(int eventId);

        Task<EventApiResponseDto?> GetOwnedEventAsync(int eventId, string? bearerToken);
        //Task<TicketTypeApiResponseDto?> GetTicketTypeByIdAsync(int eventId, int ticketTypeId);

        Task<bool> ConfirmPaidOrderSaleAsync(int eventId, ConfirmEventSaleRequestDto request);
        Task<bool> ReleaseReturnedTicketAsync(int eventId, int ticketTypeId, int quantity);

        // Same call, but says why it failed (wrong key, EventAPI down, ticket type gone).
        Task<InventoryReleaseOutcome> TryReleaseReturnedTicketAsync(int eventId, int ticketTypeId, int quantity);

        // Active refund tiers the organizer configured for the event.
        // An empty list means the event genuinely offers no customer refunds.
        // Null means the policy could not be read (EventAPI down or error), which is a
        // different situation and must not be reported to the customer as "not allowed".
        Task<IReadOnlyList<TicketAPI.Domain.RefundTier>?> GetRefundTiersAsync(int eventId);

        // Event ids where this Staff account was given the return-review duty by the organizer.
        // Fails closed: an error returns an empty list, so nobody reviews events they were not assigned to.
        Task<IReadOnlyList<int>> GetReturnReviewEventIdsAsync(int staffUserId);

        // Governance: fetch events disrupted (cancelled, postponed, rescheduled or approved change requests)
        Task<List<TicketAPI.DTOs.EventSelectionDTO>> GetDisruptedEventsForGovernanceAsync();

        // Physical gates configured by the organizer for the event
        Task<List<EventGateDto>> GetEventGatesAsync(int eventId);
    }

    public sealed record EventGateDto
    {
        public int GateId { get; set; }
        public int EventId { get; set; }
        public string GateName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? AllowedTicketTypes { get; set; }
        public List<string> AllowedTicketTypeList { get; set; } = new();
    }

    // NotFound = the ticket type no longer exists in EventAPI, so there is no inventory to give back.
    public sealed record InventoryReleaseOutcome(bool Success, bool NotFound, string? Error);
}
