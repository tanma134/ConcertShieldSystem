using EventAPI.Models;

namespace EventAPI.Services
{
    // Single gate for child-resource visibility (TicketTypes, EventImages, PricingRules, RefundPolicies, seating).
    // Published concerts are public; non-published concerts return 404 (never 403) to non-owners/non-admins to avoid ID probing.
    public interface IEventAccessService
    {
        Task<Event> EnsureVisibleAsync(int eventId, int? callerId, bool isAdmin);
    }
}
