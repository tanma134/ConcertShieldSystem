using EventAPI.Common;
using EventAPI.Data;
using EventAPI.DTOs;
using EventAPI.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EventAPI.Services
{
    public class EventSubmissionValidator : IEventSubmissionValidator
    {
        private readonly IEventRepository _eventRepository;
        private readonly ITicketTypeRepository _ticketTypeRepository;
        private readonly IRefundPolicyRepository _refundPolicyRepository;
        private readonly ISeatingRepository _seatingRepository;
        private readonly EventDbContext _context;
        private readonly IConfiguration _configuration;

        public EventSubmissionValidator(
            IEventRepository eventRepository,
            ITicketTypeRepository ticketTypeRepository,
            IRefundPolicyRepository refundPolicyRepository,
            ISeatingRepository seatingRepository,
            EventDbContext context,
            IConfiguration configuration)
        {
            _eventRepository = eventRepository;
            _ticketTypeRepository = ticketTypeRepository;
            _refundPolicyRepository = refundPolicyRepository;
            _seatingRepository = seatingRepository;
            _context = context;
            _configuration = configuration;
        }

        public async Task<SubmitValidationResultDTO> ValidateAsync(int eventId)
        {
            var result = new SubmitValidationResultDTO();

            var ev = await _eventRepository.GetByIdAsync(eventId, includeChildren: true)
                ?? throw new KeyNotFoundException($"Event {eventId} not found.");

            // Whether a poster/banner is mandatory is configurable — some teams want
            // to submit without artwork during a demo.
            bool requirePoster = _configuration.GetValue("Publication:RequirePoster", true);
            bool requireBanner = _configuration.GetValue("Publication:RequireBanner", false);

            if (string.IsNullOrWhiteSpace(ev.Title))
                result.Errors.Add("Title is required.");

            if (string.IsNullOrWhiteSpace(ev.Description) && string.IsNullOrWhiteSpace(ev.ShortDescription))
                result.Errors.Add("A description is required (fill in Description or at least ShortDescription).");
            else if (string.IsNullOrWhiteSpace(ev.Description))
                result.Warnings.Add("Only ShortDescription is set — a full Description gives buyers more detail.");

            if (string.IsNullOrWhiteSpace(ev.Slug))
                result.Errors.Add("Slug is missing.");
            else
            {
                var slugTaken = await _context.Events.AsNoTracking()
                    .AnyAsync(e => e.Slug == ev.Slug && e.EventId != eventId && !e.IsDeleted);

                if (slugTaken)
                    result.Errors.Add($"Slug '{ev.Slug}' is already used by another concert.");
            }

            if (string.IsNullOrWhiteSpace(ev.LocationName))
                result.Errors.Add("Venue name (LocationName) is required.");

            if (string.IsNullOrWhiteSpace(ev.Address))
                result.Errors.Add("Venue address is required.");

            if (string.IsNullOrWhiteSpace(ev.City))
                result.Errors.Add("City is required.");

            if (ev.StartsAt == default)
                result.Errors.Add("Start time (StartsAt) is required.");

            if (ev.EndsAt == default)
                result.Errors.Add("End time (EndsAt) is required.");

            if (ev.StartsAt != default && ev.EndsAt != default && ev.EndsAt <= ev.StartsAt)
                result.Errors.Add("EndsAt must be after StartsAt.");

            if (ev.StartsAt != default && ev.StartsAt <= DateTime.UtcNow)
                result.Errors.Add("StartsAt must be in the future.");

            if (requirePoster && string.IsNullOrWhiteSpace(ev.PosterUrl))
                result.Errors.Add("A poster image is required before submitting.");

            if (requireBanner && string.IsNullOrWhiteSpace(ev.BannerUrl))
                result.Errors.Add("A banner image is required before submitting.");

            if (!requireBanner && string.IsNullOrWhiteSpace(ev.BannerUrl))
                result.Warnings.Add("No banner image uploaded — the concert detail page will fall back to the poster.");

            var ticketTypes = await _ticketTypeRepository.GetByEventIdAsync(eventId);
            var sellableTicketTypes = ticketTypes.Where(t => t.Status == "Active").ToList();

            if (ticketTypes.Count == 0)
            {
                result.Errors.Add("At least one ticket type is required.");
            }
            else if (sellableTicketTypes.Count == 0)
            {
                result.Errors.Add("At least one ticket type must have Status = 'Active'.");
            }

            foreach (var t in ticketTypes)
            {
                var label = $"Ticket type '{t.TypeName}'";

                if (string.IsNullOrWhiteSpace(t.TypeName))
                    result.Errors.Add("Every ticket type needs a name.");

                if (t.Quantity <= 0)
                    result.Errors.Add($"{label}: quantity must be greater than 0.");

                if (t.Price < 0)
                    result.Errors.Add($"{label}: price cannot be negative.");

                if (t.OriginalPrice.HasValue && t.OriginalPrice.Value < t.Price)
                    result.Errors.Add($"{label}: OriginalPrice cannot be lower than Price.");

                if (t.MinPerOrder <= 0)
                    result.Errors.Add($"{label}: MinPerOrder must be greater than 0.");

                if (t.MaxPerOrder < t.MinPerOrder)
                    result.Errors.Add($"{label}: MaxPerOrder must be greater than or equal to MinPerOrder.");

                if (ev.MaxTicketsPerAccount.HasValue && t.MaxPerOrder > ev.MaxTicketsPerAccount.Value)
                    result.Errors.Add($"{label}: MaxPerOrder ({t.MaxPerOrder}) cannot exceed event MaxTicketsPerAccount ({ev.MaxTicketsPerAccount.Value}).");

                if (t.SalesStartsAt.HasValue && t.SalesEndsAt.HasValue && t.SalesEndsAt <= t.SalesStartsAt)
                    result.Errors.Add($"{label}: SalesEndsAt must be after SalesStartsAt.");

                if (t.SalesEndsAt.HasValue && ev.EndsAt != default && t.SalesEndsAt > ev.EndsAt)
                    result.Errors.Add($"{label}: ticket sales cannot end after the concert ends.");

                if (!t.SalesStartsAt.HasValue)
                    result.Warnings.Add($"{label}: no sales start set — sales open when the concert is published and eligible for sales.");
                if (!t.SalesEndsAt.HasValue)
                    result.Warnings.Add($"{label}: no sales end set — sales close 5 hours before the concert starts.");

                if (t.SalesStartsAt.HasValue && TicketSalesWindow.EndsAt(t.SalesEndsAt, ev.StartsAt) <= t.SalesStartsAt)
                    result.Errors.Add($"{label}: sales start must be before sales end (defaults to 5 hours before the concert).");
            }

            // Re-validate rules against current schedules since event or ticket dates may have changed.
            var ticketIds = ticketTypes.Select(t => t.TicketTypeId).ToList();
            var activePricing = await _context.PricingRules.AsNoTracking()
                .Where(r => ticketIds.Contains(r.TicketTypeId) && r.IsActive).ToListAsync();
            foreach (var rule in activePricing)
            {
                var ticket = ticketTypes.First(t => t.TicketTypeId == rule.TicketTypeId);
                try
                {
                    PricingScheduleRules.Validate(rule.RuleType, rule.TriggerFrom, rule.TriggerTo,
                        ticket.SalesStartsAt, ticket.SalesEndsAt, ev.StartsAt, DateTime.UtcNow, false);
                    if (rule.RuleType != "QuantityBased" && rule.TriggerTo <= DateTime.UtcNow)
                        result.Errors.Add($"Pricing rule '{rule.RuleName}' has expired; disable it before submitting.");
                }
                catch (InvalidOperationException error) { result.Errors.Add($"Pricing rule '{rule.RuleName}': {error.Message}"); }
            }

            // Per-account limits must be satisfiable by at least one ticket type.
            if (ev.MinTicketsPerAccount.HasValue && ev.MaxTicketsPerAccount.HasValue &&
                ev.MinTicketsPerAccount > ev.MaxTicketsPerAccount)
            {
                result.Errors.Add("MinTicketsPerAccount cannot be greater than MaxTicketsPerAccount.");
            }

            var refundPolicies = await _refundPolicyRepository.GetByEventIdAsync(eventId);
            // No active policies means customer-requested ticket refunds are not offered.
            foreach (var policy in refundPolicies)
            {
                if (policy.RefundPercent < 0 || policy.RefundPercent > 100)
                    result.Errors.Add($"Refund policy '{policy.PolicyName}': RefundPercent must be between 0 and 100.");

                if (policy.DeadlineBeforeEventHours < 0)
                    result.Errors.Add($"Refund policy '{policy.PolicyName}': DeadlineBeforeEventHours cannot be negative.");

                var salesStartsAt = ticketTypes
                    .Where(t => t.SalesStartsAt.HasValue)
                    .Select(t => t.SalesStartsAt)
                    .Min();
                var warning = RefundPolicyRules.GetSalesLeadWarning(
                    salesStartsAt, ev.StartsAt, policy.DeadlineBeforeEventHours);
                if (warning != null && !result.Warnings.Contains(warning))
                    result.Warnings.Add(warning);
            }

            var seatMap = await _seatingRepository.GetByEventIdAsync(eventId, includeSeats: true);

            if (ev.HasSeatingChart)
            {
                if (seatMap == null)
                {
                    result.Errors.Add("HasSeatingChart is true but no layout has been built.");
                }
                else if (seatMap.SeatZones.Count == 0)
                {
                    result.Errors.Add("The layout has no zones.");
                }
                else
                {
                    var validTicketTypeIds = ticketTypes.Select(t => t.TicketTypeId).ToHashSet();

                    foreach (var zone in seatMap.SeatZones)
                    {
                        if (!SeatZoneType.IsValid(zone.ZoneType))
                            result.Errors.Add($"Zone '{zone.ZoneName}': ZoneType must be 'Seated' or 'Standing'.");

                        if (!validTicketTypeIds.Contains(zone.TicketTypeId))
                            result.Errors.Add($"Zone '{zone.ZoneName}' is linked to a ticket type that no longer exists.");

                        if (SeatZoneType.IsSeated(zone.ZoneType))
                        {
                            if (zone.Seats.Count == 0)
                                result.Errors.Add($"Seated zone '{zone.ZoneName}' has no seats.");
                        }
                        else if (zone.Capacity <= 0)
                        {
                            result.Errors.Add($"Standing zone '{zone.ZoneName}' needs a capacity greater than 0.");
                        }
                    }

                    // Ticket quantity is the declared quota. Zones may be built in
                    // several passes, but at submission their total capacity must match.
                    foreach (var t in ticketTypes)
                    {
                        var zones = seatMap.SeatZones.Where(z => z.TicketTypeId == t.TicketTypeId).ToList();

                        if (zones.Count == 0)
                        {
                            result.Errors.Add(
                                $"Ticket type '{t.TypeName}' is not placed in any zone. " +
                                "Every ticket type needs at least one Seated or Standing zone.");
                            continue;
                        }

                        var capacity = zones.Sum(z =>
                            SeatZoneType.IsSeated(z.ZoneType) ? z.Seats.Count : z.Capacity);

                        // Should not happen once the layout has been saved, but a stale
                        // quantity would let TicketAPI oversell, so fail loudly.
                        if (capacity != t.Quantity)
                        {
                            result.Errors.Add(
                                $"Ticket type '{t.TypeName}': quantity is {t.Quantity} but its zones hold {capacity}. " +
                                "Adjust the zone capacity or ticket quantity so they match.");
                        }
                    }

                    var seatedZones = seatMap.SeatZones.Count(z => SeatZoneType.IsSeated(z.ZoneType));
                    var standingZones = seatMap.SeatZones.Count - seatedZones;

                    if (seatedZones > 0 && standingZones > 0)
                        result.Warnings.Add(
                            $"Mixed layout: {seatedZones} seated zone(s) and {standingZones} standing zone(s). " +
                            "Buyers pick a seat in the seated zones and buy by quantity in the standing zones.");
                }
            }
            else if (seatMap != null)
            {
                result.Warnings.Add(
                    "A layout exists but HasSeatingChart is false — it will be ignored and the concert sold as general admission.");
            }

            var requiredTypes = _configuration.GetSection("Compliance:RequiredTypes").Get<string[]>() ?? new[] { "EventPermit", "SafetyPlan" };
            var uploadedTypes = await _context.Set<EventAPI.Models.ComplianceDocument>().AsNoTracking()
                .Where(x => x.EventId == eventId && x.Version == ev.ComplianceVersion).Select(x => x.DocumentType).ToListAsync();
            foreach (var missing in requiredTypes.Except(uploadedTypes)) result.Errors.Add("Compliance document required: " + missing);
            return result;
        }
    }
}
