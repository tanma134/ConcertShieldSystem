using TicketAPI.DTOs;
using TicketAPI.Models;
using TicketAPI.Repositories;

namespace TicketAPI.Services
{
    public class StatusHistoryService : IStatusHistoryService
    {
        private readonly IStatusHistoryRepository _repository;

        public StatusHistoryService(IStatusHistoryRepository repository)
        {
            _repository = repository;
        }

        public async Task RecordAsync(StatusHistory history)
        {
            ArgumentNullException.ThrowIfNull(history);

            if (string.IsNullOrWhiteSpace(history.TargetType))
                throw new ArgumentException("TargetType không được để trống.");

            if (history.TargetId <= 0)
                throw new ArgumentException("TargetId không hợp lệ.");

            if (history.EventId <= 0)
                throw new ArgumentException("EventId không hợp lệ.");

            if (string.IsNullOrWhiteSpace(history.Action))
                throw new ArgumentException("Action không được để trống.");

            if (history.Quantity is <= 0)
                throw new ArgumentException("Quantity phải lớn hơn 0.");

            await _repository.AddAsync(history);
        }

        public async Task RecordRangeAsync(IEnumerable<StatusHistory> histories)
        {
            ArgumentNullException.ThrowIfNull(histories);

            var items = histories.ToList();

            if (items.Count == 0)
                return;

            foreach (var history in items)
            {
                ArgumentNullException.ThrowIfNull(history);

                if (string.IsNullOrWhiteSpace(history.TargetType))
                    throw new ArgumentException("TargetType không được để trống.");

                if (history.TargetId <= 0)
                    throw new ArgumentException("TargetId không hợp lệ.");

                if (history.EventId <= 0)
                    throw new ArgumentException("EventId không hợp lệ.");

                if (string.IsNullOrWhiteSpace(history.Action))
                    throw new ArgumentException("Action không được để trống.");

                if (history.Quantity is <= 0)
                    throw new ArgumentException("Quantity phải lớn hơn 0.");
            }

            await _repository.AddRangeAsync(items);
        }

        public async Task<List<StatusHistoryResponseDto>?> GetMyTicketHistoryAsync(
            int ticketId,
            int customerId)
        {
            if (ticketId <= 0)
                throw new ArgumentException("TicketId không hợp lệ.");

            if (customerId <= 0)
                throw new ArgumentException("CustomerId không hợp lệ.");

            var histories = await _repository.GetMyTicketHistoryAsync(
                ticketId,
                customerId);

            if (histories == null)
                return null;

            return histories.Select(MapToDto).ToList();
        }

        public async Task<StatusHistoryPageDto> GetAdminHistoryAsync(
            int? eventId,
            string? targetType,
            DateTime? from,
            DateTime? to,
            int page,
            int pageSize)
        {
            if (eventId is <= 0)
                throw new ArgumentException("EventId không hợp lệ.");

            if (from.HasValue && to.HasValue && from.Value > to.Value)
                throw new ArgumentException("From phải nhỏ hơn hoặc bằng To.");

            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var (histories, totalCount) =
                await _repository.GetAdminHistoryAsync(
                    eventId,
                    targetType,
                    from,
                    to,
                    page,
                    pageSize);

            return new StatusHistoryPageDto
            {
                Items = histories.Select(MapToDto).ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        private static StatusHistoryResponseDto MapToDto(StatusHistory history)
        {
            return new StatusHistoryResponseDto
            {
                StatusHistoryId = history.StatusHistoryId,
                TargetType = history.TargetType,
                TargetId = history.TargetId,
                EventId = history.EventId,
                OrderId = history.OrderId,
                TicketId = history.TicketId,
                TicketTypeId = history.TicketTypeId,
                SeatId = history.SeatId,
                HoldId = history.HoldId,
                Action = history.Action,
                OldStatus = history.OldStatus,
                NewStatus = history.NewStatus,
                Quantity = history.Quantity,
                ActorUserId = history.ActorUserId,
                ActorRole = history.ActorRole,
                Reason = history.Reason,
                CreatedAt = history.CreatedAt,
                Metadata = history.Metadata
            };
        }
    }
}
