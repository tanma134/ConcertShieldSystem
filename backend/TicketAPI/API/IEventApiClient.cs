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

        // Active refund tiers the organizer configured for the event (empty when none).
        Task<IReadOnlyList<TicketAPI.Domain.RefundTier>> GetRefundTiersAsync(int eventId);

        // Event ids where this Staff account was given the return-review duty by the organizer.
        // Fails closed: an error returns an empty list, so nobody reviews events they were not assigned to.
        Task<IReadOnlyList<int>> GetReturnReviewEventIdsAsync(int staffUserId);
    }

    // NotFound = the ticket type no longer exists in EventAPI, so there is no inventory to give back.
    public sealed record InventoryReleaseOutcome(bool Success, bool NotFound, string? Error);
}
