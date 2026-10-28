using EventAPI.Common;
using EventAPI.Models;
using EventAPI.Repositories;

namespace EventAPI.Services
{
    public class EventAccessService : IEventAccessService
    {
        private readonly IEventRepository _eventRepository;

        public EventAccessService(IEventRepository eventRepository)
        {
            _eventRepository = eventRepository;
        }

        public async Task<Event> EnsureVisibleAsync(int eventId, int? callerId, bool isAdmin)
        {
            var ev = await _eventRepository.GetByIdAsync(eventId)
                ?? throw new KeyNotFoundException($"Event {eventId} not found.");

            if (EventStatus.IsPublic(ev.Status))
                return ev;

            // Not published: only the owner or an Admin may see it. Everyone else,
            // including anonymous callers, gets the same "not found" a guesser
            // would get for a nonexistent id — never a 403 that would confirm the
            // concert exists.
            var isOwner = callerId.HasValue && ev.OrganizerId == callerId.Value;
            if (!isAdmin && !isOwner)
                throw new KeyNotFoundException($"Event {eventId} not found.");

            return ev;
        }
    }
}
