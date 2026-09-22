using EventAPI.Common;
using EventAPI.DTOs;
using EventAPI.Models;
using EventAPI.Repositories;

namespace EventAPI.Services
{
    /// <summary>
    /// Dynamic pricing rules layered on top of a TicketType's base price — e.g. an
    /// Early Bird discount that expires at a fixed time, or a price step-up once a
    /// quantity threshold sells out.
    ///
    /// EventAPI only owns the CONFIGURATION of these rules. Resolving them into an
    /// actual sale price at checkout time (which rule wins when several are active,
    /// applying it to the order total, etc.) is TicketAPI/order-flow responsibility —
    /// keeping that logic here would duplicate state across services.
    /// </summary>
    public class PricingRuleService : IPricingRuleService
    {
        private static readonly string[] TimeBasedRuleTypes = { "EarlyBird", "LastMinute", "TimeBased" };
        private const string QuantityBasedRuleType = "QuantityBased";

        private readonly IPricingRuleRepository _pricingRuleRepository;
        private readonly ITicketTypeRepository _ticketTypeRepository;
        private readonly IEventRepository _eventRepository;
        private readonly IEventAccessService _eventAccessService;

        public PricingRuleService(
            IPricingRuleRepository pricingRuleRepository,
            ITicketTypeRepository ticketTypeRepository,
            IEventRepository eventRepository,
            IEventAccessService eventAccessService)
        {
            _pricingRuleRepository = pricingRuleRepository;
            _ticketTypeRepository = ticketTypeRepository;
            _eventRepository = eventRepository;
            _eventAccessService = eventAccessService;
        }

        public async Task<List<PricingRuleResponseDTO>> GetByTicketTypeIdAsync(int ticketTypeId, int? callerId, bool isAdmin)
        {
            var ticketType = await _ticketTypeRepository.GetByIdAsync(ticketTypeId)
                ?? throw new KeyNotFoundException($"Ticket type {ticketTypeId} not found.");

            // Resolve to the parent event before returning anything — a guessed
            // ticketTypeId must not leak pricing rules for an event the caller
            // can't otherwise see.
            await _eventAccessService.EnsureVisibleAsync(ticketType.EventId, callerId, isAdmin);

            var items = await _pricingRuleRepository.GetByTicketTypeIdAsync(ticketTypeId);
            return items.Select(Map).ToList();
        }

        public async Task<PricingRuleResponseDTO> CreateAsync(
            int ticketTypeId, CreatePricingRuleDTO dto, int callerId, bool isAdmin)
        {
            var ticketType = await GetEditableTicketTypeAsync(ticketTypeId, callerId, isAdmin);

            var existing = await _pricingRuleRepository.GetByTicketTypeIdAsync(ticketTypeId);
            if (existing.Any(r => string.Equals(r.RuleName, dto.RuleName, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException(
                    $"A pricing rule named '{dto.RuleName}' already exists for ticket type '{ticketType.TypeName}'.");

            ValidateRuleShape(dto.RuleType, dto.AdjustedPrice, dto.DiscountPercent, dto.TriggerFrom, dto.TriggerTo, dto.QuantityThreshold);

            var entity = new PricingRule
            {
                TicketTypeId = ticketTypeId,
                RuleName = dto.RuleName,
                RuleType = dto.RuleType,
                AdjustedPrice = dto.AdjustedPrice,
                DiscountPercent = dto.DiscountPercent,
                TriggerFrom = dto.TriggerFrom,
                TriggerTo = dto.TriggerTo,
                QuantityThreshold = dto.QuantityThreshold,
                Priority = dto.Priority,
                IsActive = dto.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            var created = await _pricingRuleRepository.CreateAsync(entity);
            return Map(created);
        }

        public async Task<PricingRuleResponseDTO> UpdateAsync(
            int pricingRuleId, UpdatePricingRuleDTO dto, int callerId, bool isAdmin)
        {
            var entity = await _pricingRuleRepository.GetByIdAsync(pricingRuleId)
                ?? throw new KeyNotFoundException($"Pricing rule {pricingRuleId} not found.");

            await GetEditableTicketTypeAsync(entity.TicketTypeId, callerId, isAdmin);

            if (dto.RuleName != null) entity.RuleName = dto.RuleName;
            if (dto.RuleType != null) entity.RuleType = dto.RuleType;
            if (dto.AdjustedPrice.HasValue) entity.AdjustedPrice = dto.AdjustedPrice;
            if (dto.DiscountPercent.HasValue) entity.DiscountPercent = dto.DiscountPercent;
            if (dto.TriggerFrom.HasValue) entity.TriggerFrom = dto.TriggerFrom;
            if (dto.TriggerTo.HasValue) entity.TriggerTo = dto.TriggerTo;
            if (dto.QuantityThreshold.HasValue) entity.QuantityThreshold = dto.QuantityThreshold;
            if (dto.Priority.HasValue) entity.Priority = dto.Priority.Value;
            if (dto.IsActive.HasValue) entity.IsActive = dto.IsActive.Value;

            ValidateRuleShape(entity.RuleType, entity.AdjustedPrice, entity.DiscountPercent,
                entity.TriggerFrom, entity.TriggerTo, entity.QuantityThreshold);

            await _pricingRuleRepository.UpdateAsync(entity);
            return Map(entity);
        }

        public async Task DeleteAsync(int pricingRuleId, int callerId, bool isAdmin)
        {
            var entity = await _pricingRuleRepository.GetByIdAsync(pricingRuleId)
                ?? throw new KeyNotFoundException($"Pricing rule {pricingRuleId} not found.");

            await GetEditableTicketTypeAsync(entity.TicketTypeId, callerId, isAdmin);
            await _pricingRuleRepository.DeleteAsync(pricingRuleId);
        }

        /// <summary>
        /// Cross-checks a rule's shape against its declared RuleType so a saved rule
        /// can never be ambiguous about when or how it applies:
        ///   - EarlyBird / LastMinute / TimeBased -> needs at least one trigger time.
        ///   - QuantityBased -> needs a positive QuantityThreshold.
        ///   - Every rule needs a price effect: AdjustedPrice and/or DiscountPercent.
        /// FluentValidation (CreatePricingRuleValidator) already covers most of this
        /// on Create; it is re-checked here because Update can clear one field
        /// without the request going back through the validator.
        /// </summary>
        private static void ValidateRuleShape(
            string ruleType, long? adjustedPrice, decimal? discountPercent,
            DateTime? triggerFrom, DateTime? triggerTo, int? quantityThreshold)
        {
            if (!adjustedPrice.HasValue && !discountPercent.HasValue)
                throw new InvalidOperationException("Either AdjustedPrice or DiscountPercent must be set.");

            if (adjustedPrice.HasValue && adjustedPrice.Value < 0)
                throw new InvalidOperationException("AdjustedPrice cannot be negative.");

            if (discountPercent.HasValue && (discountPercent.Value < 0 || discountPercent.Value > 100))
                throw new InvalidOperationException("DiscountPercent must be between 0 and 100.");

            if (triggerFrom.HasValue && triggerTo.HasValue && triggerTo.Value <= triggerFrom.Value)
                throw new InvalidOperationException("TriggerTo must be after TriggerFrom.");

            if (TimeBasedRuleTypes.Contains(ruleType) && !triggerFrom.HasValue && !triggerTo.HasValue)
                throw new InvalidOperationException($"Rule type '{ruleType}' needs at least one of TriggerFrom/TriggerTo.");

            if (ruleType == QuantityBasedRuleType && (!quantityThreshold.HasValue || quantityThreshold.Value <= 0))
                throw new InvalidOperationException($"Rule type '{QuantityBasedRuleType}' needs a QuantityThreshold greater than 0.");
        }

        /// <summary>
        /// Ownership + status gate, reached through the ticket type's parent event.
        /// Pricing rules are only configurable while the concert is Draft or Rejected
        /// (an Admin may override) — the same rule already applied to ticket types
        /// and refund policies.
        /// </summary>
        private async Task<TicketType> GetEditableTicketTypeAsync(int ticketTypeId, int callerId, bool isAdmin)
        {
            var ticketType = await _ticketTypeRepository.GetByIdAsync(ticketTypeId)
                ?? throw new KeyNotFoundException($"Ticket type {ticketTypeId} not found.");

            var ev = await _eventRepository.GetByIdAsync(ticketType.EventId)
                ?? throw new KeyNotFoundException($"Event {ticketType.EventId} not found.");

            if (!isAdmin && ev.OrganizerId != callerId)
                throw new UnauthorizedAccessException("You do not own this concert.");

            var status = EventStatus.Normalize(ev.Status);
            if (!isAdmin && !EventStatus.Editable.Contains(status))
                throw new InvalidOperationException(
                    $"Pricing rules can only be changed while the concert is Draft or Rejected. Current status: '{status}'.");

            return ticketType;
        }

        private static PricingRuleResponseDTO Map(PricingRule r) => new()
        {
            PricingRuleId = r.PricingRuleId,
            TicketTypeId = r.TicketTypeId,
            RuleName = r.RuleName,
            RuleType = r.RuleType,
            AdjustedPrice = r.AdjustedPrice,
            DiscountPercent = r.DiscountPercent,
            TriggerFrom = r.TriggerFrom,
            TriggerTo = r.TriggerTo,
            QuantityThreshold = r.QuantityThreshold,
            Priority = r.Priority,
            IsActive = r.IsActive,
            CreatedAt = r.CreatedAt
        };
    }
}
