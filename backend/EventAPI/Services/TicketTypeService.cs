using EventAPI.Common;
using EventAPI.DTOs;
using EventAPI.Models;
using EventAPI.Repositories;

namespace EventAPI.Services
{
    public class TicketTypeService : ITicketTypeService
    {
        private readonly ITicketTypeRepository _ticketTypeRepository;
        private readonly IEventRepository _eventRepository;
        private readonly ISeatingRepository _seatingRepository;

        // Ticket types can't be edited once the event has entered final states.
        private static readonly string[] LockedEventStatuses = { EventStatus.Cancelled };

        public TicketTypeService(
            ITicketTypeRepository ticketTypeRepository,
            IEventRepository eventRepository,
            ISeatingRepository seatingRepository)
        {
            _ticketTypeRepository = ticketTypeRepository;
            _eventRepository = eventRepository;
            _seatingRepository = seatingRepository;
        }

        public async Task<List<TicketTypeResponseDTO>> GetByEventIdAsync(int eventId)
        {
            var items = await _ticketTypeRepository.GetByEventIdAsync(eventId);
            return items.Select(Map).ToList();
        }

        public async Task<TicketTypeResponseDTO> CreateAsync(int eventId, CreateTicketTypeDTO dto, int callerId, bool isAdmin)
        {
            var ev = await GetOwnedEventAsync(eventId, callerId, isAdmin);

            // With a layout present, capacity comes from the zones, so a new ticket
            // type starts at 0 and is filled in when it gets placed in a zone.
            var hasLayout = await _seatingRepository.GetByEventIdAsync(eventId, includeSeats: false) != null;
            var quantity = hasLayout ? 0 : dto.Quantity;

            var entity = new TicketType
            {
                EventId = eventId,
                TypeName = dto.TypeName,
                Description = dto.Description,
                Price = dto.Price,
                OriginalPrice = dto.OriginalPrice,
                Quantity = quantity,
                MinPerOrder = dto.MinPerOrder,
                MaxPerOrder = dto.MaxPerOrder,
                ColorCode = dto.ColorCode,
                SortOrder = dto.SortOrder,
                SalesStartsAt = dto.SalesStartsAt,
                SalesEndsAt = dto.SalesEndsAt,
                Status = "Active",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var created = await _ticketTypeRepository.CreateAsync(entity);
            return Map(created);
        }

        public async Task<TicketTypeResponseDTO> UpdateAsync(int ticketTypeId, UpdateTicketTypeDTO dto, int callerId, bool isAdmin)
        {
            var entity = await _ticketTypeRepository.GetByIdAsync(ticketTypeId)
                ?? throw new KeyNotFoundException($"TicketType {ticketTypeId} not found.");

            await GetOwnedEventAsync(entity.EventId, callerId, isAdmin);

            if (dto.TypeName != null) entity.TypeName = dto.TypeName;
            if (dto.Description != null) entity.Description = dto.Description;
            if (dto.Price.HasValue) entity.Price = dto.Price.Value;
            if (dto.OriginalPrice.HasValue) entity.OriginalPrice = dto.OriginalPrice;
            if (dto.Quantity.HasValue && dto.Quantity.Value != entity.Quantity)
            {
                var placedInLayout = await IsPlacedInLayoutAsync(entity.EventId, entity.TicketTypeId);

                if (placedInLayout)
                    throw new InvalidOperationException(
                        $"Quantity for '{entity.TypeName}' is derived from the seating layout and cannot be set by hand. " +
                        "Change the zone's rows/seats or its standing capacity instead.");

                entity.Quantity = dto.Quantity.Value;
            }
            if (dto.MinPerOrder.HasValue) entity.MinPerOrder = dto.MinPerOrder.Value;
            if (dto.MaxPerOrder.HasValue) entity.MaxPerOrder = dto.MaxPerOrder.Value;
            if (dto.ColorCode != null) entity.ColorCode = dto.ColorCode;
            if (dto.SortOrder.HasValue) entity.SortOrder = dto.SortOrder.Value;
            if (dto.Status != null) entity.Status = dto.Status;
            if (dto.SalesStartsAt.HasValue) entity.SalesStartsAt = dto.SalesStartsAt;
            if (dto.SalesEndsAt.HasValue) entity.SalesEndsAt = dto.SalesEndsAt;

            if (entity.Quantity < entity.SoldQuantity)
                throw new InvalidOperationException("Quantity cannot be less than the number already sold.");

            await _ticketTypeRepository.UpdateAsync(entity);
            return Map(entity);
        }

        public async Task DeleteAsync(int ticketTypeId, int callerId, bool isAdmin)
        {
            var entity = await _ticketTypeRepository.GetByIdAsync(ticketTypeId)
                ?? throw new KeyNotFoundException($"TicketType {ticketTypeId} not found.");

            await GetOwnedEventAsync(entity.EventId, callerId, isAdmin);

            if (entity.SoldQuantity > 0)
                throw new InvalidOperationException("Cannot delete a ticket type that already has sold tickets.");

            // Deleting a placed ticket type would orphan its zones.
            if (await IsPlacedInLayoutAsync(entity.EventId, ticketTypeId))
                throw new InvalidOperationException(
                    $"'{entity.TypeName}' is still used by one or more zones in the seating layout. " +
                    "Delete those zones first.");

            await _ticketTypeRepository.SoftDeleteAsync(ticketTypeId, callerId);
        }

        /// <summary>True when at least one zone in the concert's layout uses this ticket type.</summary>
        private async Task<bool> IsPlacedInLayoutAsync(int eventId, int ticketTypeId)
        {
            var map = await _seatingRepository.GetByEventIdAsync(eventId, includeSeats: false);
            return map != null && map.SeatZones.Any(z => z.TicketTypeId == ticketTypeId);
        }

        private async Task<Event> GetOwnedEventAsync(int eventId, int callerId, bool isAdmin)
        {
            var ev = await _eventRepository.GetByIdAsync(eventId)
                ?? throw new KeyNotFoundException($"Event {eventId} not found.");

            if (!isAdmin && ev.OrganizerId != callerId)
                throw new UnauthorizedAccessException("You do not own this event.");

            var status = EventStatus.Normalize(ev.Status);

            if (LockedEventStatuses.Contains(status))
                throw new InvalidOperationException($"Ticket types cannot be modified while the concert is '{status}'.");

            if (!isAdmin && !EventStatus.Editable.Contains(status))
                throw new InvalidOperationException(
                    $"Ticket types can only be changed while the concert is Draft or Rejected. Current status: '{status}'.");

            return ev;
        }

        private static TicketTypeResponseDTO Map(TicketType t) => new()
        {
            TicketTypeId = t.TicketTypeId,
            EventId = t.EventId,
            TypeName = t.TypeName,
            Description = t.Description,
            Price = t.Price,
            OriginalPrice = t.OriginalPrice,
            Quantity = t.Quantity,
            SoldQuantity = t.SoldQuantity,
            MinPerOrder = t.MinPerOrder,
            MaxPerOrder = t.MaxPerOrder,
            ColorCode = t.ColorCode,
            SortOrder = t.SortOrder,
            Status = t.Status,
            SalesStartsAt = t.SalesStartsAt,
            SalesEndsAt = t.SalesEndsAt,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt
        };
    }
}
