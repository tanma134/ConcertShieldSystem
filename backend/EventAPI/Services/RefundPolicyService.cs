using EventAPI.Common;
using EventAPI.DTOs;
using EventAPI.Models;
using EventAPI.Repositories;

namespace EventAPI.Services
{
    public class RefundPolicyService : IRefundPolicyService
    {
        private readonly IRefundPolicyRepository _refundPolicyRepository;
        private readonly IEventRepository _eventRepository;
        private readonly IEventAccessService _eventAccessService;

        public RefundPolicyService(
            IRefundPolicyRepository refundPolicyRepository,
            IEventRepository eventRepository,
            IEventAccessService eventAccessService)
        {
            _refundPolicyRepository = refundPolicyRepository;
            _eventRepository = eventRepository;
            _eventAccessService = eventAccessService;
        }

        public async Task<List<RefundPolicyResponseDTO>> GetByEventIdAsync(int eventId, int? callerId, bool isAdmin)
        {
            await _eventAccessService.EnsureVisibleAsync(eventId, callerId, isAdmin);
            var items = await _refundPolicyRepository.GetByEventIdAsync(eventId);
            return items.Select(Map).ToList();
        }

        public async Task<RefundPolicyResponseDTO> CreateAsync(
            int eventId, CreateRefundPolicyDTO dto, int callerId, bool isAdmin)
        {
            var ev = await GetEditableEventAsync(eventId, callerId, isAdmin);

            var existing = await _refundPolicyRepository.GetByEventIdAsync(eventId);
            if (existing.Any(p => string.Equals(p.PolicyName, dto.PolicyName, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"A refund policy named '{dto.PolicyName}' already exists for this concert.");

            var entity = new RefundPolicy
            {
                EventId = eventId,
                PolicyName = dto.PolicyName,
                Description = dto.Description,
                DeadlineBeforeEventHours = dto.DeadlineBeforeEventHours,
                RefundPercent = dto.RefundPercent,
                RequiresOrganizerApproval = dto.RequiresOrganizerApproval,
                IsActive = dto.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            var created = await _refundPolicyRepository.CreateAsync(entity);
            return Map(created);
        }

        public async Task<RefundPolicyResponseDTO> UpdateAsync(
            int refundPolicyId, UpdateRefundPolicyDTO dto, int callerId, bool isAdmin)
        {
            var entity = await _refundPolicyRepository.GetByIdAsync(refundPolicyId)
                ?? throw new KeyNotFoundException($"Refund policy {refundPolicyId} not found.");

            await GetEditableEventAsync(entity.EventId, callerId, isAdmin);

            if (dto.PolicyName != null) entity.PolicyName = dto.PolicyName;
            if (dto.Description != null) entity.Description = dto.Description;
            if (dto.DeadlineBeforeEventHours.HasValue) entity.DeadlineBeforeEventHours = dto.DeadlineBeforeEventHours.Value;
            if (dto.RefundPercent.HasValue) entity.RefundPercent = dto.RefundPercent.Value;
            if (dto.RequiresOrganizerApproval.HasValue) entity.RequiresOrganizerApproval = dto.RequiresOrganizerApproval.Value;
            if (dto.IsActive.HasValue) entity.IsActive = dto.IsActive.Value;

            if (entity.DeadlineBeforeEventHours <= 0)
                throw new InvalidOperationException("DeadlineBeforeEventHours must be greater than 0.");

            if (entity.RefundPercent < 0 || entity.RefundPercent > 100)
                throw new InvalidOperationException("RefundPercent must be between 0 and 100.");

            await _refundPolicyRepository.UpdateAsync(entity);
            return Map(entity);
        }

        public async Task DeleteAsync(int refundPolicyId, int callerId, bool isAdmin)
        {
            var entity = await _refundPolicyRepository.GetByIdAsync(refundPolicyId)
                ?? throw new KeyNotFoundException($"Refund policy {refundPolicyId} not found.");

            await GetEditableEventAsync(entity.EventId, callerId, isAdmin);
            await _refundPolicyRepository.DeleteAsync(refundPolicyId);
        }

        /// <summary>
        /// Ownership + status gate. Refund terms are part of the contract shown to
        /// buyers, so they're only configurable while the concert is still Draft or
        /// Rejected (an Admin may override).
        /// </summary>
        private async Task<Event> GetEditableEventAsync(int eventId, int callerId, bool isAdmin)
        {
            var ev = await _eventRepository.GetByIdAsync(eventId)
                ?? throw new KeyNotFoundException($"Event {eventId} not found.");

            if (!isAdmin && ev.OrganizerId != callerId)
                throw new UnauthorizedAccessException("You do not own this concert.");

            var status = EventStatus.Normalize(ev.Status);
            if (!isAdmin && !EventStatus.Editable.Contains(status))
                throw new InvalidOperationException(
                    $"Refund policies can only be changed while the concert is Draft or Rejected. Current status: '{status}'.");

            return ev;
        }

        private static RefundPolicyResponseDTO Map(RefundPolicy r) => new()
        {
            RefundPolicyId = r.RefundPolicyId,
            EventId = r.EventId,
            PolicyName = r.PolicyName,
            Description = r.Description,
            DeadlineBeforeEventHours = r.DeadlineBeforeEventHours,
            RefundPercent = r.RefundPercent,
            RequiresOrganizerApproval = r.RequiresOrganizerApproval,
            IsActive = r.IsActive,
            CreatedAt = r.CreatedAt
        };
    }
}
