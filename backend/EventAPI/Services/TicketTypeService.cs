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
        private readonly IEventAccessService _eventAccessService;

        // Ticket types can't be edited once the event has entered final states.
        private static readonly string[] LockedEventStatuses = { EventStatus.Cancelled };

        public TicketTypeService(
            ITicketTypeRepository ticketTypeRepository,
            IEventRepository eventRepository,
            ISeatingRepository seatingRepository,
            IEventAccessService eventAccessService)
        {
            _ticketTypeRepository = ticketTypeRepository;
            _eventRepository = eventRepository;
            _seatingRepository = seatingRepository;
            _eventAccessService = eventAccessService;
        }

        // Lấy cấu hình theo sự kiện; chỉ đọc dữ liệu, không thay đổi trạng thái.

        public async Task<List<TicketTypeResponseDTO>> GetByEventIdAsync(int eventId, int? callerId, bool isAdmin)
        {
            // Draft/Pending/Rejected/Cancelled ticket types are the owner's/Admin's
            // business only — a guessed eventId must not leak them.
            await _eventAccessService.EnsureVisibleAsync(eventId, callerId, isAdmin);

            var items = await _ticketTypeRepository.GetByEventIdAsync(eventId);
            return items.Select(Map).ToList();
        }

        // Tạo mới cấu hình sau khi kiểm tra các business rule bắt buộc.

        public async Task<TicketTypeResponseDTO> CreateAsync(int eventId, CreateTicketTypeDTO dto, int callerId, bool isAdmin)
        {
            var ev = await GetOwnedEventAsync(eventId, callerId, isAdmin);

            if (await _ticketTypeRepository.ExistsNameAsync(eventId, dto.TypeName))
                throw new InvalidOperationException(
                    $"A ticket type named '{dto.TypeName.Trim()}' already exists for this concert.");

            var entity = new TicketType
            {
                EventId = eventId,
                TypeName = dto.TypeName.Trim(),
                Description = dto.Description,
                Price = dto.Price,
                OriginalPrice = dto.OriginalPrice,
                Quantity = dto.Quantity,
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

        // Cập nhật cấu hình hiện có và giữ các invariant nghiệp vụ trước khi lưu.

        public async Task<TicketTypeResponseDTO> UpdateAsync(int ticketTypeId, UpdateTicketTypeDTO dto, int callerId, bool isAdmin)
        {
            var entity = await _ticketTypeRepository.GetByIdAsync(ticketTypeId)
                ?? throw new KeyNotFoundException($"TicketType {ticketTypeId} not found.");

            var parentEvent = await GetOwnedEventAsync(entity.EventId, callerId, isAdmin);

            if (dto.TypeName != null)
            {
                if (await _ticketTypeRepository.ExistsNameAsync(entity.EventId, dto.TypeName, ticketTypeId))
                    throw new InvalidOperationException(
                        $"A ticket type named '{dto.TypeName.Trim()}' already exists for this concert.");
                entity.TypeName = dto.TypeName.Trim();
            }
            if (dto.Description != null) entity.Description = dto.Description;
            if (dto.Price.HasValue) entity.Price = dto.Price.Value;
            if (dto.OriginalPrice.HasValue) entity.OriginalPrice = dto.OriginalPrice;
            if (dto.Quantity.HasValue && dto.Quantity.Value != entity.Quantity)
            {
                var mappedCapacity = await GetMappedCapacityAsync(entity.EventId, entity.TicketTypeId);
                if (dto.Quantity.Value < mappedCapacity)
                    throw new InvalidOperationException(
                        $"Quantity {dto.Quantity.Value} cannot be lower than mapped zone capacity {mappedCapacity}.");

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

            // Re-check cross-field rules against the FINAL entity state: the DTO
            // validator only catches a bad pair when both sides are sent together,
            // but a partial update (e.g. only MinPerOrder) can just as easily break
            // the pair against the value already stored on the entity.
            if (entity.MaxPerOrder < entity.MinPerOrder)
                throw new InvalidOperationException("MaxPerOrder must be >= MinPerOrder.");

            if (entity.SalesStartsAt.HasValue && entity.SalesEndsAt.HasValue &&
                entity.SalesEndsAt <= entity.SalesStartsAt)
                throw new InvalidOperationException("SalesEndsAt must be after SalesStartsAt.");

            if (entity.SalesEndsAt.HasValue && parentEvent.EndsAt != default &&
                entity.SalesEndsAt > parentEvent.EndsAt)
                throw new InvalidOperationException("Ticket sales cannot end after the concert ends.");

            await _ticketTypeRepository.UpdateAsync(entity);
            return Map(entity);
        }

        // Xóa hoặc vô hiệu cấu hình theo rule của domain; không xử lý nghiệp vụ ngoài phạm vi EventAPI.

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

        // Quantity is organizer-owned configuration. It may change after zones are mapped,
        // but never below the capacity already assigned to those zones.
        public async Task<InventoryOperationResultDTO> ReserveInventoryAsync(int ticketTypeId, int quantity)
        {
            var entity = await _ticketTypeRepository.GetByIdAsync(ticketTypeId);
            if (entity == null)
                return Fail(ticketTypeId, 0, "TicketType not found.");

            // Business gates the caller cannot bypass: even a well-behaved Booking
            // service must not be able to reserve against a type that isn't currently
            // sellable. The atomic UPDATE below re-checks Status/quantity anyway, but
            // failing fast here gives a precise Reason instead of a generic "no rows".
            if (entity.Status != "Active")
                return Fail(ticketTypeId, entity.Quantity - entity.SoldQuantity, $"TicketType is '{entity.Status}', not on sale.");

            var ev = await _eventRepository.GetByIdAsync(entity.EventId);
            if (ev == null || EventStatus.Normalize(ev.Status) != EventStatus.Published)
                return Fail(ticketTypeId, entity.Quantity - entity.SoldQuantity, "Parent concert is not Published.");

            var now = DateTime.UtcNow;
            if (entity.SalesStartsAt.HasValue && now < entity.SalesStartsAt.Value)
                return Fail(ticketTypeId, entity.Quantity - entity.SoldQuantity, "Sales window has not started yet.");
            if (entity.SalesEndsAt.HasValue && now > entity.SalesEndsAt.Value)
                return Fail(ticketTypeId, entity.Quantity - entity.SoldQuantity, "Sales window has ended.");

            var reserved = await _ticketTypeRepository.TryReserveAsync(ticketTypeId, quantity);

            // Re-read after the atomic UPDATE so AvailableQuantity reflects the actual
            // committed state — including when another concurrent caller won the race.
            var reloaded = await _ticketTypeRepository.GetByIdAsync(ticketTypeId);
            var available = reloaded == null ? 0 : reloaded.Quantity - reloaded.SoldQuantity;

            return new InventoryOperationResultDTO
            {
                Success = reserved,
                TicketTypeId = ticketTypeId,
                AvailableQuantity = available,
                Reason = reserved ? null : "Not enough inventory available."
            };
        }

        public async Task<InventoryOperationResultDTO> ReleaseInventoryAsync(int ticketTypeId, int quantity)
        {
            var released = await _ticketTypeRepository.ReleaseAsync(ticketTypeId, quantity);
            var reloaded = await _ticketTypeRepository.GetByIdAsync(ticketTypeId);

            return new InventoryOperationResultDTO
            {
                Success = released,
                TicketTypeId = ticketTypeId,
                AvailableQuantity = reloaded == null ? 0 : reloaded.Quantity - reloaded.SoldQuantity,
                Reason = released ? null : "TicketType not found."
            };
        }

        private static InventoryOperationResultDTO Fail(int ticketTypeId, int available, string reason) => new()
        {
            Success = false,
            TicketTypeId = ticketTypeId,
            AvailableQuantity = available,
            Reason = reason
        };

        private async Task<int> GetMappedCapacityAsync(int eventId, int ticketTypeId)
        {
            var map = await _seatingRepository.GetByEventIdAsync(eventId, includeSeats: true);
            if (map == null) return 0;

            return map.SeatZones
                .Where(z => z.TicketTypeId == ticketTypeId)
                .Sum(z => SeatZoneType.IsSeated(z.ZoneType) && z.Seats.Count > 0
                    ? z.Seats.Count
                    : z.Capacity);
        }

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
