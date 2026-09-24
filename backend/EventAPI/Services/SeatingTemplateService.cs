using System.Text.Json;
using EventAPI.Common;
using EventAPI.DTOs;
using EventAPI.Models;
using EventAPI.Repositories;

namespace EventAPI.Services
{
    /// <summary>
    /// Reusable venue layouts (UC_26.2 Apply Seating Template / UC_26.3 Save Seating
    /// Chart as Template), so an organizer running several similar concerts doesn't
    /// have to redraw the same zones every time.
    ///
    /// Applying a template never generates seats itself — it turns the template's
    /// zones into a BuildSeatingChartDTO and delegates to ISeatingService.BuildAsync,
    /// so seat generation, capacity sync and the "can't rebuild once sold" guard all
    /// stay owned by SeatingService in exactly one place.
    /// </summary>
    public class SeatingTemplateService : ISeatingTemplateService
    {
        private readonly ISeatingTemplateRepository _templateRepository;
        private readonly IEventRepository _eventRepository;
        private readonly ISeatingRepository _seatingRepository;
        private readonly ITicketTypeRepository _ticketTypeRepository;
        private readonly ISeatingService _seatingService;

        public SeatingTemplateService(
            ISeatingTemplateRepository templateRepository,
            IEventRepository eventRepository,
            ISeatingRepository seatingRepository,
            ITicketTypeRepository ticketTypeRepository,
            ISeatingService seatingService)
        {
            _templateRepository = templateRepository;
            _eventRepository = eventRepository;
            _seatingRepository = seatingRepository;
            _ticketTypeRepository = ticketTypeRepository;
            _seatingService = seatingService;
        }

        public async Task<List<SeatingTemplateListDTO>> GetVisibleAsync(int callerId, bool isAdmin = false)
        {
            var items = await _templateRepository.GetVisibleToAsync(callerId, isAdmin);
            return items.Select(t => MapToList(t, callerId)).ToList();
        }

        public async Task<SeatingTemplateResponseDTO> GetByIdAsync(int id, int callerId, bool isAdmin)
        {
            var entity = await GetVisibleEntityAsync(id, callerId, isAdmin);
            return MapToResponse(entity);
        }

        public async Task<SeatingTemplateResponseDTO> SaveFromEventAsync(
            SaveSeatingTemplateDTO dto, int callerId, bool isAdmin)
        {
            var ev = await _eventRepository.GetByIdAsync(dto.EventId)
                ?? throw new KeyNotFoundException($"Event {dto.EventId} not found.");

            if (!isAdmin && ev.OrganizerId != callerId)
                throw new UnauthorizedAccessException("You do not own this concert.");

            var map = await _seatingRepository.GetByEventIdAsync(dto.EventId, includeSeats: true)
                ?? throw new InvalidOperationException(
                    "This concert has no seating chart yet. Build a layout first, then save it as a template.");

            if (map.SeatZones.Count == 0)
                throw new InvalidOperationException("The current layout has no zones to save.");

            var zones = map.SeatZones.Select(ToTemplateZone).ToList();

            var entity = new SeatingTemplate
            {
                OrganizerId = callerId,
                Name = dto.Name,
                Description = dto.Description,
                IsPublic = isAdmin && dto.IsPublic, // only an Admin publishing a starter template can mark it public
                LayoutJson = map.LayoutJson,
                ZonesJson = JsonSerializer.Serialize(zones),
                CreatedBy = callerId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var created = await _templateRepository.CreateAsync(entity);
            return MapToResponse(created);
        }

        public async Task<SeatingTemplateResponseDTO> CreateAsync(
            CreateSeatingTemplateDTO dto, int callerId, bool isAdmin)
        {
            if (dto.Zones == null || dto.Zones.Count == 0)
                throw new InvalidOperationException("At least one zone is required.");

            foreach (var z in dto.Zones)
                ValidateTemplateZone(z);

            var entity = new SeatingTemplate
            {
                OrganizerId = callerId,
                Name = dto.Name,
                Description = dto.Description,
                IsPublic = isAdmin && dto.IsPublic,
                LayoutJson = dto.LayoutJson,
                ZonesJson = JsonSerializer.Serialize(dto.Zones),
                CreatedBy = callerId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var created = await _templateRepository.CreateAsync(entity);
            return MapToResponse(created);
        }

        public async Task<SeatingTemplateResponseDTO> UpdateAsync(
            int id, UpdateSeatingTemplateDTO dto, int callerId, bool isAdmin)
        {
            var entity = await GetOwnedEntityAsync(id, callerId, isAdmin);

            if (dto.Name != null) entity.Name = dto.Name;
            if (dto.Description != null) entity.Description = dto.Description;
            if (dto.IsPublic.HasValue && isAdmin) entity.IsPublic = dto.IsPublic.Value;

            await _templateRepository.UpdateAsync(entity);
            return MapToResponse(entity);
        }

        public async Task DeleteAsync(int id, int callerId, bool isAdmin)
        {
            var entity = await GetOwnedEntityAsync(id, callerId, isAdmin);
            await _templateRepository.SoftDeleteAsync(entity.SeatingTemplateId);
        }

        public async Task<SeatingChartResponseDTO> ApplyToEventAsync(
            int templateId, int eventId, ApplySeatingTemplateDTO dto, int callerId, bool isAdmin)
        {
            var template = await GetVisibleEntityAsync(templateId, callerId, isAdmin);
            var zones = DeserializeZones(template.ZonesJson);

            if (dto.ZoneMappings == null || dto.ZoneMappings.Count != zones.Count)
                throw new InvalidOperationException(
                    $"This template has {zones.Count} zone(s) — map every zone to a ticket type before applying it.");

            var ticketTypes = await _ticketTypeRepository.GetByEventIdAsync(eventId);
            var validTicketTypeIds = ticketTypes.Select(t => t.TicketTypeId).ToHashSet();

            var seenIndexes = new HashSet<int>();
            var buildZones = new List<CreateSeatZoneDTO>();

            foreach (var mapping in dto.ZoneMappings.OrderBy(m => m.ZoneIndex))
            {
                if (mapping.ZoneIndex < 0 || mapping.ZoneIndex >= zones.Count)
                    throw new ArgumentException($"Invalid ZoneIndex {mapping.ZoneIndex}.");

                if (!seenIndexes.Add(mapping.ZoneIndex))
                    throw new ArgumentException($"ZoneIndex {mapping.ZoneIndex} was mapped more than once.");

                if (!validTicketTypeIds.Contains(mapping.TicketTypeId))
                    throw new ArgumentException(
                        $"TicketTypeId {mapping.TicketTypeId} does not belong to event {eventId}. " +
                        "Create the ticket type first — every zone must be linked to one.");

                var z = zones[mapping.ZoneIndex];
                buildZones.Add(new CreateSeatZoneDTO
                {
                    TicketTypeId = mapping.TicketTypeId,
                    ZoneName = mapping.ZoneName?.Trim() ?? z.ZoneName,
                    ShapeJson = mapping.ShapeJson ?? z.ShapeJson,
                    ZoneType = mapping.ZoneType ?? z.ZoneType,
                    Rows = mapping.Rows ?? z.Rows,
                    SeatsPerRow = mapping.SeatsPerRow ?? z.SeatsPerRow,
                    RowLabelPrefix = mapping.RowLabelPrefix ?? z.RowLabelPrefix,
                    Capacity = mapping.Capacity ?? z.Capacity
                });
            }

            var buildDto = new BuildSeatingChartDTO
            {
                Name = template.Name,
                LayoutJson = template.LayoutJson,
                Zones = buildZones
            };

            // Delegates to SeatingService — reuses seat generation, capacity sync and
            // the "cannot rebuild once anything is sold" guard instead of duplicating them.
            return await _seatingService.BuildAsync(eventId, buildDto, callerId, isAdmin);
        }

        // ---- mapping helpers ----

        /// <summary>
        /// Seats are always generated as a rectangular grid (see
        /// SeatingService.GenerateSeats), so Rows / SeatsPerRow / the first row label
        /// can be derived straight back from the seats that already exist.
        /// </summary>
        private static SeatingTemplateZoneDTO ToTemplateZone(SeatZone z)
        {
            if (!SeatZoneType.IsSeated(z.ZoneType))
            {
                return new SeatingTemplateZoneDTO
                {
                    ZoneName = z.ZoneName,
                    ZoneType = SeatZoneType.Standing,
                    ShapeJson = z.ShapeJson,
                    Capacity = z.Capacity
                };
            }

            var rows = z.Seats.Select(s => s.RowLabel).Distinct().Count();
            var seatsPerRow = z.Seats.Count > 0
                ? z.Seats.GroupBy(s => s.RowLabel).Max(g => g.Count())
                : 0;
            var firstRowLabel = z.Seats
                .OrderBy(s => s.YCoordinate)
                .Select(s => s.RowLabel)
                .FirstOrDefault() ?? "A";

            return new SeatingTemplateZoneDTO
            {
                ZoneName = z.ZoneName,
                ZoneType = SeatZoneType.Seated,
                ShapeJson = z.ShapeJson,
                Rows = Math.Max(rows, 1),
                SeatsPerRow = Math.Max(seatsPerRow, 1),
                RowLabelPrefix = firstRowLabel
            };
        }

        private static void ValidateTemplateZone(SeatingTemplateZoneDTO z)
        {
            if (!SeatZoneType.IsValid(z.ZoneType))
                throw new ArgumentException($"Zone '{z.ZoneName}': ZoneType must be 'Seated' or 'Standing'.");

            if (SeatZoneType.IsStanding(z.ZoneType) && (!z.Capacity.HasValue || z.Capacity <= 0))
                throw new ArgumentException($"Standing zone '{z.ZoneName}' needs a capacity greater than 0.");

            if (SeatZoneType.IsSeated(z.ZoneType) && (z.Rows <= 0 || z.SeatsPerRow <= 0))
                throw new ArgumentException($"Seated zone '{z.ZoneName}' needs Rows and SeatsPerRow greater than 0.");
        }

        // ---- ownership / visibility ----

        private async Task<SeatingTemplate> GetOwnedEntityAsync(int id, int callerId, bool isAdmin)
        {
            var entity = await _templateRepository.GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"Seating template {id} not found.");

            if (!isAdmin && entity.OrganizerId != callerId)
                throw new UnauthorizedAccessException("You do not own this template.");

            return entity;
        }

        /// <summary>Readable by its owner, an Admin, or anyone when it is public.</summary>
        private async Task<SeatingTemplate> GetVisibleEntityAsync(int id, int callerId, bool isAdmin)
        {
            var entity = await _templateRepository.GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"Seating template {id} not found.");

            if (!isAdmin && !entity.IsPublic && entity.OrganizerId != callerId)
                throw new UnauthorizedAccessException("You do not have access to this template.");

            return entity;
        }

        // ---- mapping / (de)serialization ----

        private static List<SeatingTemplateZoneDTO> DeserializeZones(string zonesJson) =>
            JsonSerializer.Deserialize<List<SeatingTemplateZoneDTO>>(zonesJson) ?? new List<SeatingTemplateZoneDTO>();

        private static int EstimateCapacity(List<SeatingTemplateZoneDTO> zones) =>
            zones.Sum(z => SeatZoneType.IsSeated(z.ZoneType) ? z.Rows * z.SeatsPerRow : (z.Capacity ?? 0));

        private static SeatingTemplateListDTO MapToList(SeatingTemplate t, int callerId)
        {
            var zones = DeserializeZones(t.ZonesJson);
            return new SeatingTemplateListDTO
            {
                SeatingTemplateId = t.SeatingTemplateId,
                Name = t.Name,
                Description = t.Description,
                IsPublic = t.IsPublic,
                IsMine = t.OrganizerId == callerId,
                TotalZones = zones.Count,
                EstimatedCapacity = EstimateCapacity(zones),
                CreatedAt = t.CreatedAt
            };
        }

        private static SeatingTemplateResponseDTO MapToResponse(SeatingTemplate t)
        {
            var zones = DeserializeZones(t.ZonesJson);
            return new SeatingTemplateResponseDTO
            {
                SeatingTemplateId = t.SeatingTemplateId,
                OrganizerId = t.OrganizerId,
                Name = t.Name,
                Description = t.Description,
                IsPublic = t.IsPublic,
                LayoutJson = t.LayoutJson,
                Zones = zones,
                EstimatedCapacity = EstimateCapacity(zones),
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            };
        }
    }
}
