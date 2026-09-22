using EventAPI.Models;

namespace EventAPI.Services
{
    /// <summary>
    /// Single place that decides whether a caller may SEE a concert's child data
    /// (TicketTypes, EventImages, PricingRules, RefundPolicies, seating).
    ///
    /// Rule (spec section 2 — "Fix child-resource authorization"):
    ///   Published            -> anyone (including anonymous callers)
    ///   Draft/Pending/Rejected/Cancelled -> owner or Admin only; everyone else gets
    ///   a 404 (never a 403), so a guessed id can't be used to confirm a concert
    ///   exists.
    ///
    /// Every read endpoint for a child resource must resolve the parent Event
    /// through here before returning data, so a guessed TicketType/Image/Zone id
    /// can never bypass event ownership.
    /// </summary>
    public interface IEventAccessService
    {
        Task<Event> EnsureVisibleAsync(int eventId, int? callerId, bool isAdmin);
    }
}
