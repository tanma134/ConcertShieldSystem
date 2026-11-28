using EventAPI.Common;
using EventAPI.DTOs;
using EventAPI.Models;
using EventAPI.Repositories;
using System.Text.Json;

namespace EventAPI.Services
{
    // Owns concert seating layouts (either no chart for General Admission, or one SeatMap mixing Seated and Standing zones).
    public class SeatingService : ISeatingService
    {
        private readonly ISeatingRepository _seatingRepository;
        private readonly IEventRepository _eventRepository;
        private readonly ITicketTypeRepository _ticketTypeRepository;
        private readonly IEventAccessService _eventAccessService;
        private readonly ILogger<SeatingService> _logger;

        // Guardrails so a typo (rows = 10000) can't try to insert millions of rows.
        private const int MaxSeatsPerZone = 20_000;
        private const int MaxCapacityPerZone = 100_000;
        private const int MaxSeatsPerEvent = 100_000;

        public SeatingService(
            ISeatingRepository seatingRepository,
            IEventRepository eventRepository,
            ITicketTypeRepository ticketTypeRepository,
            IEventAccessService eventAccessService,
            ILogger<SeatingService> logger)
        {
            _seatingRepository = seatingRepository;
            _eventRepository = eventRepository;
            _ticketTypeRepository = ticketTypeRepository;
            _eventAccessService = eventAccessService;
            _logger = logger;
        }

        public async Task<SeatingChartResponseDTO?> GetByEventIdAsync(int eventId, int? callerId, bool isAdmin)
        {
            await _eventAccessService.EnsureVisibleAsync(eventId, callerId, isAdmin);
            var map = await _seatingRepository.GetByEventIdAsync(eventId, includeSeats: true);
            return map == null ? null : MapChart(map, includeSeats: true);
        }

        public async Task<SeatingChartPreviewDTO?> GetPreviewAsync(int eventId, int? callerId, bool isAdmin)
        {
            await _eventAccessService.EnsureVisibleAsync(eventId, callerId, isAdmin);
            var map = await _seatingRepository.GetByEventIdAsync(eventId, includeSeats: true);
            if (map == null) return null;

            var ticketTypes = await _ticketTypeRepository.GetByEventIdAsync(eventId);
            var byId = ticketTypes.ToDictionary(t => t.TicketTypeId);

            var zones = map.SeatZones.Select(z =>
            {
                byId.TryGetValue(z.TicketTypeId, out var tt);
                var seated = SeatZoneType.IsSeated(z.ZoneType);

                var effectiveCapacity = seated && z.Seats.Count > 0
                    ? z.Seats.Count
                    : z.Capacity;

                return new ZonePreviewDTO
                {
                    SeatZoneId = z.SeatZoneId,
                    ZoneName = z.ZoneName,
                    ZoneType = SeatZoneType.Normalize(z.ZoneType),
                    TicketTypeName = tt?.TypeName,
                    Price = tt?.Price,
                    Capacity = effectiveCapacity,
                    TotalSeats = seated ? z.Seats.Count : 0,
                    AvailableSeats = seated
                        ? z.Seats.Count(s => s.Status == "Available")
                        // Standing zones have no per-seat rows, so availability comes
                        // from what TicketAPI has sold against the ticket type.
                        : Math.Max(effectiveCapacity - (tt?.SoldQuantity ?? 0), 0)
                };
            }).ToList();

            return new SeatingChartPreviewDTO
            {
                SeatMapId = map.SeatMapId,
                Name = map.Name,
                TotalZones = zones.Count,
                SeatedZones = zones.Count(z => z.ZoneType == SeatZoneType.Seated),
                StandingZones = zones.Count(z => z.ZoneType == SeatZoneType.Standing),
                TotalCapacity = zones.Sum(z => z.Capacity),
                TotalSeats = zones.Sum(z => z.TotalSeats),
                AvailableSeats = zones.Sum(z => z.AvailableSeats),
                Zones = zones
            };
        }

        public async Task<SeatZoneResponseDTO> GetZoneSeatsAsync(int seatZoneId, int? callerId, bool isAdmin, bool availableOnly = false)
        {
            var zone = await _seatingRepository.GetZoneByIdAsync(seatZoneId, includeSeats: true)
                ?? throw new KeyNotFoundException($"Seat zone {seatZoneId} not found.");

            // A guessed zone id must not bypass event ownership: resolve the parent
            // event through the zone's SeatMap before returning anything.
            await _eventAccessService.EnsureVisibleAsync(zone.SeatMap.EventId, callerId, isAdmin);

            if (SeatZoneType.IsStanding(zone.ZoneType))
                throw new InvalidOperationException(
                    $"'{zone.ZoneName}' is a standing zone — there are no individual seats to pick. " +
                    "Buy by quantity against its ticket type instead.");

            var dto = MapZone(zone, includeSeats: true);

            if (availableOnly)
                dto.Seats = dto.Seats.Where(s => s.Status == "Available").ToList();

            return dto;
        }

        public async Task<SeatingChartResponseDTO> BuildAsync(
            int eventId, BuildSeatingChartDTO dto, int callerId, bool isAdmin)
        {
            var ev = await GetEditableEventAsync(eventId, callerId, isAdmin);

            if (dto.Zones == null || dto.Zones.Count == 0)
                throw new InvalidOperationException(
                    "At least one zone is required. For a concert with no layout at all, " +
                    "skip this endpoint and leave HasSeatingChart = false.");

            var ticketTypes = await _ticketTypeRepository.GetByEventIdAsync(eventId);
            if (ticketTypes.Count == 0)
                throw new InvalidOperationException(
                    "Create the ticket types first — every zone must be linked to one.");

            var validTicketTypeIds = ticketTypes.Select(t => t.TicketTypeId).ToHashSet();
            var errors = ValidateZones(dto.Zones, validTicketTypeIds);

            foreach (var tt in ticketTypes)
            {
                var requestedCap = dto.Zones
                    .Where(z => z.TicketTypeId == tt.TicketTypeId)
                    .Sum(DtoCapacity);
                if (requestedCap > tt.Quantity)
                {
                    errors.Add($"Layout allocates {requestedCap} seats to '{tt.TypeName}', which exceeds its declared quota ({tt.Quantity}).");
                }
            }

            if (errors.Count > 0)
                throw new ArgumentException(string.Join(" | ", errors));

            // Replacing an existing layout is destructive, so refuse once anything is taken.
            var existing = await _seatingRepository.GetByEventIdAsync(eventId, includeSeats: true);
            if (existing != null)
            {
                await EnsureNothingSoldAsync(existing, ticketTypes,
                    "Cannot rebuild the layout");

                await _seatingRepository.DeleteSeatMapAsync(existing.SeatMapId, callerId);
            }

            var seatMap = await _seatingRepository.CreateSeatMapAsync(new SeatMap
            {
                EventId = eventId,
                Name = dto.Name,
                LayoutJson = dto.LayoutJson,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            foreach (var zoneDto in dto.Zones)
                await CreateZoneInternalAsync(seatMap.SeatMapId, zoneDto);

            // Having a layout implies the concert is no longer pure general admission.
            if (!ev.HasSeatingChart)
            {
                ev.HasSeatingChart = true;
            }
            ev.SeatingMode = dto.Zones.All(z => SeatZoneType.IsStanding(z.ZoneType))
                ? SeatingMode.StandingZones
                : SeatingMode.ReservedSeating;
            ev.UpdatedBy = callerId;
            await _eventRepository.UpdateAsync(ev);

            var reloaded = await _seatingRepository.GetByEventIdAsync(eventId, includeSeats: true);
            await SynchronizeTicketQuantitiesAsync(ticketTypes, reloaded!);

            _logger.LogInformation(
                "Layout {SeatMapId} built for event {EventId}: {Zones} zone(s), capacity {Capacity}",
                seatMap.SeatMapId, eventId, dto.Zones.Count, reloaded!.SeatZones.Sum(z => z.Capacity));

            return MapChart(reloaded, includeSeats: true);
        }

        public async Task<SeatZoneResponseDTO> AddZoneAsync(
            int eventId, CreateSeatZoneDTO dto, int callerId, bool isAdmin)
        {
            var ev = await GetEditableEventAsync(eventId, callerId, isAdmin);

            var map = await _seatingRepository.GetByEventIdAsync(eventId, includeSeats: false)
                ?? throw new InvalidOperationException(
                    "This concert has no layout yet. Build one first via POST api/seating/event/{eventId}.");

            var ticketTypes = await _ticketTypeRepository.GetByEventIdAsync(eventId);
            var validTicketTypeIds = ticketTypes.Select(t => t.TicketTypeId).ToHashSet();

            var errors = ValidateZones(new List<CreateSeatZoneDTO> { dto }, validTicketTypeIds);

            if (map.SeatZones.Any(z => string.Equals(z.ZoneName, dto.ZoneName, StringComparison.OrdinalIgnoreCase)))
                errors.Add($"A zone named '{dto.ZoneName}' already exists on this layout.");

            var newBox = ParseBounds(dto.ShapeJson);
            if (newBox is { } nb)
            {
                foreach (var existingZone in map.SeatZones)
                {
                    var existingBox = ParseBounds(existingZone.ShapeJson);
                    if (existingBox is { } eb && nb.Intersects(eb))
                    {
                        errors.Add($"Zone '{dto.ZoneName}' overlaps existing zone '{existingZone.ZoneName}'.");
                    }
                }
            }

            var targetTicket = ticketTypes.FirstOrDefault(t => t.TicketTypeId == dto.TicketTypeId);
            if (targetTicket != null)
            {
                var currentAllocated = map.SeatZones
                    .Where(z => z.TicketTypeId == dto.TicketTypeId)
                    .Sum(ZoneCapacity);
                var newZoneCap = DtoCapacity(dto);
                if (currentAllocated + newZoneCap > targetTicket.Quantity)
                {
                    errors.Add($"Cannot allocate {newZoneCap} seats to '{targetTicket.TypeName}'. Total zone capacity ({currentAllocated + newZoneCap}) would exceed declared quota ({targetTicket.Quantity}).");
                }
            }

            if (errors.Count > 0)
                throw new ArgumentException(string.Join(" | ", errors));

            var zone = await CreateZoneInternalAsync(map.SeatMapId, dto);

            if (SeatZoneType.IsSeated(dto.ZoneType) && ev.SeatingMode != SeatingMode.ReservedSeating)
            {
                ev.SeatingMode = SeatingMode.ReservedSeating;
                ev.UpdatedBy = callerId;
                await _eventRepository.UpdateAsync(ev);
            }

            var reloaded = await _seatingRepository.GetZoneByIdAsync(zone.SeatZoneId, includeSeats: true);
            var updatedMap = await _seatingRepository.GetByEventIdAsync(eventId, includeSeats: true);
            await SynchronizeTicketQuantitiesAsync(ticketTypes, updatedMap!);
            return MapZone(reloaded!, includeSeats: true);
        }

        public async Task<SeatZoneResponseDTO> UpdateZoneAsync(
            int seatZoneId, UpdateSeatZoneDTO dto, int callerId, bool isAdmin)
        {
            var zone = await _seatingRepository.GetZoneByIdAsync(seatZoneId, includeSeats: true)
                ?? throw new KeyNotFoundException($"Seat zone {seatZoneId} not found.");

            var eventId = zone.SeatMap.EventId;
            await GetEditableEventAsync(eventId, callerId, isAdmin);

            var ticketTypes = await _ticketTypeRepository.GetByEventIdAsync(eventId);

            if (dto.ZoneName != null) zone.ZoneName = dto.ZoneName;
            if (dto.ShapeJson != null)
            {
                var updatedBox = ParseBounds(dto.ShapeJson);
                if (updatedBox is { } ub)
                {
                    var existingMap = await _seatingRepository.GetByEventIdAsync(eventId, includeSeats: false);
                    foreach (var otherZone in existingMap!.SeatZones.Where(z => z.SeatZoneId != seatZoneId))
                    {
                        var otherBox = ParseBounds(otherZone.ShapeJson);
                        if (otherBox is { } ob && ub.Intersects(ob))
                        {
                            throw new ArgumentException($"Updated shape for zone '{zone.ZoneName}' overlaps zone '{otherZone.ZoneName}'.");
                        }
                    }
                }
                zone.ShapeJson = dto.ShapeJson;
            }

            if (dto.ZoneName != null)
            {
                var existingMap = await _seatingRepository.GetByEventIdAsync(eventId, includeSeats: false);
                if (existingMap!.SeatZones.Any(z => z.SeatZoneId != seatZoneId &&
                    string.Equals(z.ZoneName, dto.ZoneName.Trim(), StringComparison.OrdinalIgnoreCase)))
                    throw new ArgumentException($"A zone named '{dto.ZoneName}' already exists on this layout.");
                zone.ZoneName = dto.ZoneName.Trim();
            }

            if (dto.TicketTypeId.HasValue && dto.TicketTypeId.Value != zone.TicketTypeId)
            {
                if (!ticketTypes.Any(t => t.TicketTypeId == dto.TicketTypeId.Value))
                    throw new ArgumentException($"TicketTypeId {dto.TicketTypeId} does not belong to this concert.");

                await EnsureZoneNotSoldAsync(zone, ticketTypes,
                    "Cannot change the ticket type");

                zone.TicketTypeId = dto.TicketTypeId.Value;
            }

            if (dto.Capacity.HasValue)
            {
                if (SeatZoneType.IsSeated(zone.ZoneType))
                    throw new InvalidOperationException(
                        "Capacity is derived from the generated seats for a Seated zone. " +
                        "To resize it, delete the zone and add it again with new Rows/SeatsPerRow.");

                if (dto.Capacity.Value <= 0)
                    throw new ArgumentException("Capacity must be greater than 0.");

                if (dto.Capacity.Value > MaxCapacityPerZone)
                    throw new ArgumentException($"Capacity cannot exceed {MaxCapacityPerZone}.");

                var sold = ticketTypes.FirstOrDefault(t => t.TicketTypeId == zone.TicketTypeId)?.SoldQuantity ?? 0;
                if (dto.Capacity.Value < sold)
                    throw new InvalidOperationException(
                        $"Cannot shrink this zone to {dto.Capacity.Value}: {sold} ticket(s) have already been sold against it.");

                zone.Capacity = dto.Capacity.Value;
            }

            if (dto.Rows.HasValue || dto.SeatsPerRow.HasValue || dto.RowLabelPrefix != null)
            {
                if (!SeatZoneType.IsSeated(zone.ZoneType))
                    throw new InvalidOperationException("Rows and SeatsPerRow only apply to a Seated zone.");

                await EnsureZoneNotSoldAsync(zone, ticketTypes, "Cannot resize this zone");
                var currentRows = zone.Seats.Select(s => s.RowLabel).Distinct().Count();
                var currentSeatsPerRow = zone.Seats.GroupBy(s => s.RowLabel)
                    .Select(g => g.Count()).DefaultIfEmpty(0).Max();
                var rows = dto.Rows ?? currentRows;
                var seatsPerRow = dto.SeatsPerRow ?? currentSeatsPerRow;
                if (rows <= 0 || seatsPerRow <= 0)
                    throw new ArgumentException("Rows and SeatsPerRow must be greater than 0.");
                if ((long)rows * seatsPerRow > MaxSeatsPerZone)
                    throw new ArgumentException($"A zone cannot exceed {MaxSeatsPerZone} seats.");

                var resizeDto = new CreateSeatZoneDTO
                {
                    Rows = rows,
                    SeatsPerRow = seatsPerRow,
                    RowLabelPrefix = dto.RowLabelPrefix ?? zone.Seats
                        .OrderBy(s => s.YCoordinate).Select(s => s.RowLabel).FirstOrDefault() ?? "A"
                };
                zone.Capacity = rows * seatsPerRow;
                await _seatingRepository.ReplaceSeatsAsync(
                    zone.SeatZoneId, GenerateSeats(zone.SeatZoneId, resizeDto), zone.Capacity);
            }

            await _seatingRepository.UpdateZoneAsync(zone);

            var reloaded = await _seatingRepository.GetZoneByIdAsync(seatZoneId, includeSeats: true);
            var updatedMap = await _seatingRepository.GetByEventIdAsync(eventId, includeSeats: true);
            await SynchronizeTicketQuantitiesAsync(ticketTypes, updatedMap!);
            return MapZone(reloaded!, includeSeats: true);
        }

        public async Task DeleteZoneAsync(int seatZoneId, int callerId, bool isAdmin)
        {
            var zone = await _seatingRepository.GetZoneByIdAsync(seatZoneId, includeSeats: true)
                ?? throw new KeyNotFoundException($"Seat zone {seatZoneId} not found.");

            var eventId = zone.SeatMap.EventId;
            await GetEditableEventAsync(eventId, callerId, isAdmin);

            var ticketTypes = await _ticketTypeRepository.GetByEventIdAsync(eventId);
            await EnsureZoneNotSoldAsync(zone, ticketTypes, "Cannot delete this zone");

            await _seatingRepository.DeleteZoneAsync(seatZoneId);

            var remaining = await _seatingRepository.GetByEventIdAsync(eventId, includeSeats: false);
            var ev = await _eventRepository.GetByIdAsync(eventId);
            if (ev != null && remaining != null)
            {
                ev.SeatingMode = remaining.SeatZones.Any(z => SeatZoneType.IsSeated(z.ZoneType))
                    ? SeatingMode.ReservedSeating
                    : SeatingMode.StandingZones;
                ev.UpdatedBy = callerId;
                await _eventRepository.UpdateAsync(ev);
            }
            if (remaining != null)
                await SynchronizeTicketQuantitiesAsync(ticketTypes, remaining);
        }

        public async Task<SeatingChartResponseDTO> UpdateLayoutAsync(int eventId, string layoutJson, int callerId, bool isAdmin)
        {
            await GetEditableEventAsync(eventId, callerId, isAdmin);
            var map = await _seatingRepository.GetByEventIdAsync(eventId, includeSeats: false)
                ?? throw new KeyNotFoundException($"No seating chart found for event {eventId}.");

            map.LayoutJson = layoutJson;
            map.UpdatedAt = DateTime.UtcNow;
            await _seatingRepository.UpdateSeatMapAsync(map);

            var reloaded = await _seatingRepository.GetByEventIdAsync(eventId, includeSeats: true);
            return MapChart(reloaded!, includeSeats: true);
        }

        public async Task DeleteAsync(int eventId, int callerId, bool isAdmin)
        {
            var ev = await GetEditableEventAsync(eventId, callerId, isAdmin);

            var map = await _seatingRepository.GetByEventIdAsync(eventId, includeSeats: true)
                ?? throw new KeyNotFoundException("This concert has no layout.");

            var ticketTypes = await _ticketTypeRepository.GetByEventIdAsync(eventId);
            await EnsureNothingSoldAsync(map, ticketTypes, "Cannot delete the layout");

            await _seatingRepository.DeleteSeatMapAsync(map.SeatMapId, callerId);

            // Back to pure general admission — quantities become manual again and are
            // left exactly as the layout last set them.
            ev.HasSeatingChart = false;
            ev.SeatingMode = SeatingMode.GeneralAdmission;
            ev.UpdatedBy = callerId;
            await _eventRepository.UpdateAsync(ev);
        }

        private async Task<SeatZone> CreateZoneInternalAsync(int seatMapId, CreateSeatZoneDTO dto)
        {
            var zoneType = SeatZoneType.Normalize(dto.ZoneType);
            var isSeated = zoneType == SeatZoneType.Seated;

            var capacity = isSeated
                ? dto.Rows * dto.SeatsPerRow
                : dto.Capacity!.Value;

            var zone = await _seatingRepository.CreateZoneAsync(new SeatZone
            {
                SeatMapId = seatMapId,
                TicketTypeId = dto.TicketTypeId,
                ZoneName = dto.ZoneName,
                ShapeJson = dto.ShapeJson,
                ZoneType = zoneType,
                Capacity = capacity
            });

            // Standing zones deliberately get no Seat rows — there's nothing to pick.
            if (isSeated)
                await _seatingRepository.AddSeatsAsync(GenerateSeats(zone.SeatZoneId, dto));

            return zone;
        }

        private static List<string> ValidateZones(
            List<CreateSeatZoneDTO> zones, HashSet<int> validTicketTypeIds)
        {
            var errors = new List<string>();
            long runningSeats = 0;

            foreach (var zone in zones)
            {
                var label = $"Zone '{zone.ZoneName}'";

                if (string.IsNullOrWhiteSpace(zone.ZoneName))
                    errors.Add("Every zone needs a name.");

                if (!validTicketTypeIds.Contains(zone.TicketTypeId))
                    errors.Add($"{label}: TicketTypeId {zone.TicketTypeId} does not belong to this concert.");

                if (!SeatZoneType.IsValid(zone.ZoneType))
                    errors.Add($"{label}: ZoneType must be 'Seated' or 'Standing'.");

                if (SeatZoneType.IsStanding(zone.ZoneType))
                {
                    if (!zone.Capacity.HasValue || zone.Capacity.Value <= 0)
                        errors.Add($"{label}: a Standing zone requires Capacity greater than 0.");
                    else if (zone.Capacity.Value > MaxCapacityPerZone)
                        errors.Add($"{label}: Capacity {zone.Capacity} exceeds the {MaxCapacityPerZone} limit.");
                }
                else
                {
                    if (zone.Rows <= 0)
                        errors.Add($"{label}: Rows must be greater than 0 for a Seated zone.");

                    if (zone.SeatsPerRow <= 0)
                        errors.Add($"{label}: SeatsPerRow must be greater than 0 for a Seated zone.");

                    var zoneSeats = (long)Math.Max(zone.Rows, 0) * Math.Max(zone.SeatsPerRow, 0);

                    if (zoneSeats > MaxSeatsPerZone)
                        errors.Add($"{label}: {zoneSeats} seats exceeds the {MaxSeatsPerZone} per-zone limit.");

                    runningSeats += zoneSeats;
                }
            }

            if (zones.Count > 1 &&
                zones.Select(z => (z.ZoneName ?? string.Empty).Trim().ToLowerInvariant()).Distinct().Count() != zones.Count)
            {
                errors.Add("Zone names must be unique within a layout.");
            }

            foreach (var zone in zones)
            {
                var label = string.IsNullOrWhiteSpace(zone.ZoneName) ? "A zone" : $"Zone '{zone.ZoneName}'";
                if (!string.IsNullOrWhiteSpace(zone.ShapeJson))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(zone.ShapeJson);
                        var root = doc.RootElement;
                        var hasWidth = root.TryGetProperty("width", out var w) || root.TryGetProperty("w", out w);
                        var hasHeight = root.TryGetProperty("height", out var h) || root.TryGetProperty("h", out h);
                        if (root.TryGetProperty("x", out _) && root.TryGetProperty("y", out _) && hasWidth && hasHeight)
                        {
                            if (w.GetDouble() <= 0 || h.GetDouble() <= 0)
                                errors.Add($"{label}: Rectangle width and height must be greater than 0.");
                        }
                        else if (root.TryGetProperty("points", out var points) && points.ValueKind == JsonValueKind.Array)
                        {
                            if (points.GetArrayLength() < 3)
                                errors.Add($"{label}: Polygon must have at least 3 vertices.");
                        }
                        else
                        {
                            errors.Add($"{label}: ShapeJson must represent either a rectangle (x, y, width/w, height/h) or a polygon (points array).");
                        }
                    }
                    catch (JsonException)
                    {
                        errors.Add($"{label}: Invalid ShapeJson format.");
                    }
                }
            }

            var boxes = zones.Select(ParseBounds).ToList();
            for (var i = 0; i < boxes.Count; i++)
                for (var j = i + 1; j < boxes.Count; j++)
                    if (boxes[i] is { } a && boxes[j] is { } b && a.Intersects(b))
                        errors.Add($"Zone '{zones[i].ZoneName}' overlaps zone '{zones[j].ZoneName}'.");

            if (runningSeats > MaxSeatsPerEvent)
                errors.Add($"Total seats ({runningSeats}) exceeds the {MaxSeatsPerEvent} per-event limit.");

            return errors;
        }

        public readonly record struct Bounds(double Left, double Top, double Right, double Bottom)
        {
            public bool Intersects(Bounds other) => Left < other.Right && Right > other.Left && Top < other.Bottom && Bottom > other.Top;
        }

        public static Bounds? ParseBounds(string? shapeJson)
        {
            if (string.IsNullOrWhiteSpace(shapeJson)) return null;
            try
            {
                using var doc = JsonDocument.Parse(shapeJson);
                var root = doc.RootElement;
                var hasWidth = root.TryGetProperty("width", out var w) || root.TryGetProperty("w", out w);
                var hasHeight = root.TryGetProperty("height", out var h) || root.TryGetProperty("h", out h);
                if (root.TryGetProperty("x", out var x) && root.TryGetProperty("y", out var y) && hasWidth && hasHeight)
                {
                    var width = w.GetDouble();
                    var height = h.GetDouble();
                    if (width <= 0 || height <= 0) return null;
                    return new Bounds(x.GetDouble(), y.GetDouble(), x.GetDouble() + width, y.GetDouble() + height);
                }
                if (root.TryGetProperty("points", out var points) && points.ValueKind == JsonValueKind.Array)
                {
                    var values = points.EnumerateArray().Select(p => (p.GetProperty("x").GetDouble(), p.GetProperty("y").GetDouble())).ToList();
                    return values.Count >= 3 ? new Bounds(values.Min(p => p.Item1), values.Min(p => p.Item2), values.Max(p => p.Item1), values.Max(p => p.Item2)) : null;
                }
            }
            catch (JsonException) { return null; }
            return null;
        }

        private static Bounds? ParseBounds(CreateSeatZoneDTO zone) => ParseBounds(zone.ShapeJson);

        private static int DtoCapacity(CreateSeatZoneDTO zone)
        {
            return SeatZoneType.IsSeated(zone.ZoneType)
                ? Math.Max(zone.Rows, 0) * Math.Max(zone.SeatsPerRow, 0)
                : Math.Max(zone.Capacity ?? 0, 0);
        }

        private static int ZoneCapacity(SeatZone zone)
        {
            return SeatZoneType.IsSeated(zone.ZoneType) && zone.Seats.Count > 0
                ? zone.Seats.Count
                : zone.Capacity;
        }

        private static Task SynchronizeTicketQuantitiesAsync(List<TicketType> ticketTypes, SeatMap map)
        {
            // TicketType.Quantity is the organizer's declared quota configured in Step 2.
            // Multiple zones can allocate against this quota (e.g. 2 zones of 250 for a 500 quota).
            // We do NOT overwrite ticketType.Quantity with zone capacity, preserving the organizer's plan.
            return Task.CompletedTask;
        }

        private async Task EnsureZoneNotSoldAsync(SeatZone zone, List<TicketType> ticketTypes, string action)
        {
            // Tickets sold is the source of truth for "this zone is in use". Seat rows carry
            // a status column, but nothing in the system ever writes it, so counting it
            // always returned zero and a zone with sold tickets could be rebuilt or deleted
            // while its issued seats were still attached to live tickets.
            var ticketType = ticketTypes.FirstOrDefault(t => t.TicketTypeId == zone.TicketTypeId);
            var sold = ticketType?.SoldQuantity ?? 0;

            if (SeatZoneType.IsSeated(zone.ZoneType))
            {
                // A seated zone can also be occupied by a held seat, which is not yet sold.
                var occupied = await _seatingRepository.CountOccupiedSeatsAsync(zone.SeatZoneId);
                var inUse = Math.Max(sold, occupied);
                if (inUse > 0)
                    throw new InvalidOperationException(
                        $"{action}: {inUse} seat(s) in '{zone.ZoneName}' are already held, reserved or sold.");
            }
            else
            {
                if (sold > 0)
                    throw new InvalidOperationException(
                        $"{action}: {sold} ticket(s) have already been sold for the standing zone '{zone.ZoneName}'.");
            }
        }

        private async Task EnsureNothingSoldAsync(SeatMap map, List<TicketType> ticketTypes, string action)
        {
            foreach (var zone in map.SeatZones)
                await EnsureZoneNotSoldAsync(zone, ticketTypes, action);
        }

        private static List<Seat> GenerateSeats(int seatZoneId, CreateSeatZoneDTO dto)
        {
            var seats = new List<Seat>(dto.Rows * dto.SeatsPerRow);
            var startIndex = RowLabelToIndex(string.IsNullOrWhiteSpace(dto.RowLabelPrefix) ? "A" : dto.RowLabelPrefix);

            for (int r = 0; r < dto.Rows; r++)
            {
                var rowLabel = IndexToRowLabel(startIndex + r);

                for (int n = 1; n <= dto.SeatsPerRow; n++)
                {
                    seats.Add(new Seat
                    {
                        SeatZoneId = seatZoneId,
                        RowLabel = rowLabel,
                        SeatNumber = n.ToString(),
                        XCoordinate = n,
                        YCoordinate = r + 1,
                        Status = "Available",
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }

            var seen = new HashSet<(int?, int?)>();
            foreach (var s in seats)
            {
                if (!seen.Add((s.XCoordinate, s.YCoordinate)))
                    throw new InvalidOperationException($"Duplicate seat coordinate ({s.XCoordinate}, {s.YCoordinate}) generated in zone.");
            }

            return seats;
        }

        private static int RowLabelToIndex(string label)
        {
            int index = 0;
            foreach (var c in label.Trim().ToUpperInvariant())
            {
                if (c < 'A' || c > 'Z') return 0;
                index = index * 26 + (c - 'A' + 1);
            }
            return index - 1;
        }

        private static string IndexToRowLabel(int index)
        {
            var label = string.Empty;
            index = Math.Max(index, 0);

            do
            {
                label = (char)('A' + index % 26) + label;
                index = index / 26 - 1;
            } while (index >= 0);

            return label;
        }

        // Ownership and status gate: layouts are only editable in Draft/Rejected (unless Admin overrides).
        private async Task<Event> GetEditableEventAsync(int eventId, int callerId, bool isAdmin)
        {
            var ev = await _eventRepository.GetByIdAsync(eventId)
                ?? throw new KeyNotFoundException($"Event {eventId} not found.");

            if (!isAdmin && ev.OrganizerId != callerId)
                throw new UnauthorizedAccessException("You do not own this concert.");

            var status = EventStatus.Normalize(ev.Status);
            if (!isAdmin && !EventStatus.Editable.Contains(status))
                throw new InvalidOperationException(
                    $"The layout can only be configured while the concert is Draft or Rejected. Current status: '{status}'.");

            return ev;
        }

        internal static SeatingChartResponseDTO MapChart(SeatMap m, bool includeSeats) => new()
        {
            SeatMapId = m.SeatMapId,
            EventId = m.EventId,
            Name = m.Name,
            LayoutJson = m.LayoutJson,
            SeatingMode = m.SeatZones.All(z => SeatZoneType.IsStanding(z.ZoneType))
                ? SeatingMode.StandingZones
                : SeatingMode.ReservedSeating,
            CreatedAt = m.CreatedAt,
            UpdatedAt = m.UpdatedAt,
            Zones = m.SeatZones.OrderBy(z => z.ZoneName).Select(z => MapZone(z, includeSeats)).ToList()
        };

        internal static SeatZoneResponseDTO MapZone(SeatZone z, bool includeSeats)
        {
            var seated = SeatZoneType.IsSeated(z.ZoneType);
            // DB-first databases created by the older code left Capacity = 0 for
            // numbered zones. Their actual capacity is the number of Seat rows.
            // Expose one consistent value to the organizer, admin and validator.
            var effectiveCapacity = seated && z.Seats.Count > 0
                ? z.Seats.Count
                : z.Capacity;

            return new SeatZoneResponseDTO
            {
                SeatZoneId = z.SeatZoneId,
                SeatMapId = z.SeatMapId,
                TicketTypeId = z.TicketTypeId,
                ZoneName = z.ZoneName,
                ShapeJson = z.ShapeJson,
                ZoneType = SeatZoneType.Normalize(z.ZoneType),
                Capacity = effectiveCapacity,
                TotalSeats = seated ? effectiveCapacity : 0,
                AvailableSeats = seated ? z.Seats.Count(s => s.Status == "Available") : z.Capacity,
                Seats = seated && includeSeats
                    ? z.Seats
                        .OrderBy(s => s.YCoordinate).ThenBy(s => s.XCoordinate)
                        .Select(s => new SeatResponseDTO
                        {
                            SeatId = s.SeatId,
                            RowLabel = s.RowLabel,
                            SeatNumber = s.SeatNumber,
                            XCoordinate = s.XCoordinate,
                            YCoordinate = s.YCoordinate,
                            Status = s.Status
                        }).ToList()
                    : new List<SeatResponseDTO>()
            };
        }
    }
}
