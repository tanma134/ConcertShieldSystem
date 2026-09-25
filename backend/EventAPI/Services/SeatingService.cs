using EventAPI.Common;
using EventAPI.DTOs;
using EventAPI.Models;
using EventAPI.Repositories;

namespace EventAPI.Services
{
    // EventAPI owns the seating configuration only. TicketType.Quantity is the declared
    // quota; zones consume that quota but never rewrite it. Runtime availability belongs
    // to the booking/inventory service.
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

        // =====================================================================
        // Reads
        // =====================================================================

        // Lấy cấu hình theo sự kiện; chỉ đọc dữ liệu, không thay đổi trạng thái.

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
                Zones = zones
            };
        }

        public async Task<SeatZoneResponseDTO> GetZoneSeatsAsync(int seatZoneId, int? callerId, bool isAdmin)
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

            return MapZone(zone, includeSeats: true);
        }

        // =====================================================================
        // Build / replace the whole layout
        // =====================================================================

        // Tạo lại sơ đồ ghế trong một luồng nghiệp vụ; kiểm tra quyền, quota vé và sinh ghế trước khi lưu.

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

            if (errors.Count > 0)
                throw new ArgumentException(string.Join(" | ", errors));

            // Validate before deleting/replacing anything so an invalid layout cannot
            // destroy the organizer's current chart.
            ValidateTicketTypeCapacities(
                ticketTypes,
                dto.Zones.Select(z => (z.TicketTypeId, DtoCapacity(z))));

            // Replacing an existing layout is destructive, so refuse once anything is taken.
            var existing = await _seatingRepository.GetByEventIdAsync(eventId, includeSeats: true);
            if (existing != null)
            {
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

            _logger.LogInformation(
                "Layout {SeatMapId} built for event {EventId}: {Zones} zone(s), capacity {Capacity}",
                seatMap.SeatMapId, eventId, dto.Zones.Count, reloaded!.SeatZones.Sum(z => z.Capacity));

            return MapChart(reloaded, includeSeats: true);
        }

        // =====================================================================
        // Zone-level operations
        // =====================================================================

        // Thêm zone mới và chặn tổng capacity vượt Quantity của TicketType trước khi ghi DB.

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

            if (errors.Count > 0)
                throw new ArgumentException(string.Join(" | ", errors));

            ValidateTicketTypeCapacities(
                ticketTypes,
                map.SeatZones.Select(z => (z.TicketTypeId, ZoneCapacity(z)))
                    .Append((dto.TicketTypeId, DtoCapacity(dto))));

            var zone = await CreateZoneInternalAsync(map.SeatMapId, dto);

            if (SeatZoneType.IsSeated(dto.ZoneType) && ev.SeatingMode != SeatingMode.ReservedSeating)
            {
                ev.SeatingMode = SeatingMode.ReservedSeating;
                ev.UpdatedBy = callerId;
                await _eventRepository.UpdateAsync(ev);
            }

            var reloaded = await _seatingRepository.GetZoneByIdAsync(zone.SeatZoneId, includeSeats: true);
            return MapZone(reloaded!, includeSeats: true);
        }

        // Cập nhật zone; nếu đổi kích thước Seated thì kiểm tra quota rồi sinh lại physical seat tương ứng.

        public async Task<SeatZoneResponseDTO> UpdateZoneAsync(
            int seatZoneId, UpdateSeatZoneDTO dto, int callerId, bool isAdmin)
        {
            var zone = await _seatingRepository.GetZoneByIdAsync(seatZoneId, includeSeats: true)
                ?? throw new KeyNotFoundException($"Seat zone {seatZoneId} not found.");

            var eventId = zone.SeatMap.EventId;
            await GetEditableEventAsync(eventId, callerId, isAdmin);

            var ticketTypes = await _ticketTypeRepository.GetByEventIdAsync(eventId);
            var currentMap = await _seatingRepository.GetByEventIdAsync(eventId, includeSeats: true)
                ?? throw new InvalidOperationException("The seating layout no longer exists.");

            var proposedTicketTypeId = dto.TicketTypeId ?? zone.TicketTypeId;
            var proposedCapacity = ZoneCapacity(zone);
            if (SeatZoneType.IsStanding(zone.ZoneType) && dto.Capacity.HasValue)
                proposedCapacity = dto.Capacity.Value;
            if (SeatZoneType.IsSeated(zone.ZoneType) && (dto.Rows.HasValue || dto.SeatsPerRow.HasValue))
            {
                var currentRowsForValidation = zone.Seats.Select(s => s.RowLabel).Distinct().Count();
                var currentSeatsPerRowForValidation = zone.Seats.GroupBy(s => s.RowLabel)
                    .Select(g => g.Count()).DefaultIfEmpty(0).Max();
                proposedCapacity = (dto.Rows ?? currentRowsForValidation) *
                    (dto.SeatsPerRow ?? currentSeatsPerRowForValidation);
            }

            ValidateTicketTypeCapacities(
                ticketTypes,
                currentMap.SeatZones
                    .Where(z => z.SeatZoneId != seatZoneId)
                    .Select(z => (z.TicketTypeId, ZoneCapacity(z)))
                    .Append((proposedTicketTypeId, proposedCapacity)));

            if (dto.ZoneName != null) zone.ZoneName = dto.ZoneName;
            if (dto.ShapeJson != null) zone.ShapeJson = dto.ShapeJson;

            if (dto.ZoneName != null)
            {
                var existingMap = await _seatingRepository.GetByEventIdAsync(eventId, includeSeats: false);
                if (existingMap!.SeatZones.Any(z => z.SeatZoneId != seatZoneId &&
                    string.Equals(z.ZoneName, dto.ZoneName.Trim(), StringComparison.OrdinalIgnoreCase)))
                    throw new ArgumentException($"A zone named '{dto.ZoneName}' already exists on this layout.");
                zone.ZoneName = dto.ZoneName.Trim();
            }

            // ---- Re-link to a different ticket type ----
            if (dto.TicketTypeId.HasValue && dto.TicketTypeId.Value != zone.TicketTypeId)
            {
                if (!ticketTypes.Any(t => t.TicketTypeId == dto.TicketTypeId.Value))
                    throw new ArgumentException($"TicketTypeId {dto.TicketTypeId} does not belong to this concert.");

                zone.TicketTypeId = dto.TicketTypeId.Value;
            }

            // ---- Resize a standing zone ----
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

                zone.Capacity = dto.Capacity.Value;
            }

            // ---- Resize/regenerate a numbered grid ----
            if (dto.Rows.HasValue || dto.SeatsPerRow.HasValue || dto.RowLabelPrefix != null)
            {
                if (!SeatZoneType.IsSeated(zone.ZoneType))
                    throw new InvalidOperationException("Rows and SeatsPerRow only apply to a Seated zone.");

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
            return MapZone(reloaded!, includeSeats: true);
        }

        // Xóa zone khỏi sơ đồ khi event còn cho phép chỉnh sửa.

        public async Task DeleteZoneAsync(int seatZoneId, int callerId, bool isAdmin)
        {
            var zone = await _seatingRepository.GetZoneByIdAsync(seatZoneId, includeSeats: true)
                ?? throw new KeyNotFoundException($"Seat zone {seatZoneId} not found.");

            var eventId = zone.SeatMap.EventId;
            await GetEditableEventAsync(eventId, callerId, isAdmin);

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
        }

        // Xóa hoặc vô hiệu cấu hình theo rule của domain; không xử lý nghiệp vụ ngoài phạm vi EventAPI.

        public async Task DeleteAsync(int eventId, int callerId, bool isAdmin)
        {
            var ev = await GetEditableEventAsync(eventId, callerId, isAdmin);

            var map = await _seatingRepository.GetByEventIdAsync(eventId, includeSeats: true)
                ?? throw new KeyNotFoundException("This concert has no layout.");

            await _seatingRepository.DeleteSeatMapAsync(map.SeatMapId, callerId);

            // Back to pure general admission — quantities become manual again and are
            // left exactly as the layout last set them.
            ev.HasSeatingChart = false;
            ev.SeatingMode = SeatingMode.GeneralAdmission;
            ev.UpdatedBy = callerId;
            await _eventRepository.UpdateAsync(ev);
        }

        // =====================================================================
        // Internals
        // =====================================================================

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

            if (runningSeats > MaxSeatsPerEvent)
                errors.Add($"Total seats ({runningSeats}) exceeds the {MaxSeatsPerEvent} per-event limit.");

            return errors;
        }

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

        // Enforces the configuration invariant before persistence: all zones mapped to a
        // ticket class may consume at most that class's declared Quantity. Unused quota is
        // valid and remains available for later zones; the seating chart never changes Quantity.
        private static void ValidateTicketTypeCapacities(
            List<TicketType> ticketTypes,
            IEnumerable<(int TicketTypeId, int Capacity)> zoneCapacities)
        {
            var totals = zoneCapacities
                .GroupBy(x => x.TicketTypeId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Capacity));

            foreach (var ticketType in ticketTypes)
            {
                var capacity = totals.GetValueOrDefault(ticketType.TicketTypeId, 0);
                if (capacity > ticketType.Quantity)
                    throw new InvalidOperationException(
                        $"Zone capacity {capacity} exceeds Quantity {ticketType.Quantity} of ticket type '{ticketType.TypeName}'.");
            }
        }

        // Generates the physical seat definitions for a Seated zone. Row labels continue
        // after Z as AA, AB... so large layouts stay deterministic and readable.
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
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }

            return seats;
        }

        // Converts spreadsheet-style row labels to a zero-based index.
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

        // Converts a zero-based row index back to spreadsheet-style labels.
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

        // Central ownership/status gate for layout mutations. Pending/Published layouts
        // stay immutable so Admin reviews the same configuration that was submitted.
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

        // ---- Mapping ----

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
                Seats = seated && includeSeats
                    ? z.Seats
                        .OrderBy(s => s.YCoordinate).ThenBy(s => s.XCoordinate)
                        .Select(s => new SeatResponseDTO
                        {
                            SeatId = s.SeatId,
                            RowLabel = s.RowLabel,
                            SeatNumber = s.SeatNumber,
                            XCoordinate = s.XCoordinate,
                            YCoordinate = s.YCoordinate
                        }).ToList()
                    : new List<SeatResponseDTO>()
            };
        }
    }
}
