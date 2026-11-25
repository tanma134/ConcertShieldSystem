using TicketAPI.Domain;
using TicketAPI.DTOs;
using TicketAPI.Models;
using TicketAPI.Repositories;
using static TicketAPI.DTOs.AdminTicketDtos;

namespace TicketAPI.Services
{
    public class TicketService : ITicketService
    {
        private readonly ITicketRepository _ticketRepository;

        public TicketService(ITicketRepository ticketRepository)
        {
            _ticketRepository = ticketRepository;
        }

        public async Task<List<MyTicketResponseDto>> GetMyTicketsAsync(int userId)
        {
            var tickets = await _ticketRepository.GetMyTicketsAsync(userId);

            return tickets.Select(ticket => new MyTicketResponseDto
            {
                TicketId = ticket.TicketId,
                TicketCode = ticket.TicketCode,
                OrderId = ticket.OrderId,
                EventId = ticket.EventId,
                TicketTypeId = ticket.TicketTypeId,
                TicketTypeName = ticket.TicketTypeName,
                SeatId = ticket.SeatId,
                OwnerName = ticket.OwnerName,
                Status = ticket.Status,
                CheckedInAt = ticket.CheckedInAt,

                EventName = ticket.Order.EventName,
                PosterUrl = ticket.Order.PosterUrl,
                StartsAt = ticket.Order.StartsAt,
                OrderDate = ticket.Order.OrderDate,
                OrderStatus = ticket.Order.Status,

                UnitPrice = ticket.Order.OrderDetails
                    .FirstOrDefault(detail =>
                        detail.TicketTypeId == ticket.TicketTypeId)
                    ?.UnitPrice
            }).ToList();
        }

        public async Task<MyTicketResponseDto?> GetMyTicketDetailsAsync(
            int ticketId,
            int userId)
        {
            var ticket = await _ticketRepository.GetMyTicketByIdAsync(ticketId, userId);

            if (ticket == null)
                return null;

            return new MyTicketResponseDto
            {
                TicketId = ticket.TicketId,
                TicketCode = ticket.TicketCode,
                OrderId = ticket.OrderId,
                EventId = ticket.EventId,
                TicketTypeId = ticket.TicketTypeId,
                TicketTypeName = ticket.TicketTypeName,
                SeatId = ticket.SeatId,
                OwnerName = ticket.OwnerName,
                Status = ticket.Status,
                CheckedInAt = ticket.CheckedInAt,

                EventName = ticket.Order.EventName,
                PosterUrl = ticket.Order.PosterUrl,
                StartsAt = ticket.Order.StartsAt,
                OrderDate = ticket.Order.OrderDate,
                OrderStatus = ticket.Order.Status,

                UnitPrice = ticket.Order.OrderDetails
                    .FirstOrDefault(detail =>
                        detail.TicketTypeId == ticket.TicketTypeId)
                    ?.UnitPrice
            };
        }

        public async Task RevokeSingleAsync(
            int ticketId,
            int adminUserId,
            string reason)
        {
            if (ticketId <= 0)
                throw new ArgumentException("Invalid ticket ID.");

            if (adminUserId <= 0)
                throw new ArgumentException("Invalid admin ID.");

            var normalizedReason = reason?.Trim();

            if (string.IsNullOrWhiteSpace(normalizedReason))
                throw new ArgumentException("Please provide a reason for revoking the ticket.");

            if (normalizedReason.Length > 500)
                throw new ArgumentException("The revocation reason cannot exceed 500 characters.");

            var ticket = await _ticketRepository.GetTicketForAdminRevokeAsync(ticketId);

            if (ticket == null)
                throw new KeyNotFoundException("Ticket not found.");

            if (ticket.CheckedInAt.HasValue ||
                ticket.TicketQrTokens.Any(token => token.IsUsed))
            {
                throw new InvalidOperationException("A checked-in ticket cannot be revoked.");
            }

            if (ticket.Status != TicketStatuses.Active)
            {
                throw new InvalidOperationException(
                    "Only Active tickets can be revoked.");
            }

            await _ticketRepository.RevokeTicketAsync(
                ticketId,
                adminUserId,
                normalizedReason);
        }

        public async Task<AdminTicketPageDto> GetAdminTicketsAsync(
    AdminTicketQueryDto query)
        {
            query.Page = Math.Max(query.Page, 1);
            query.PageSize = Math.Clamp(query.PageSize, 1, 100);

            var (tickets, totalCount) =
                await _ticketRepository.GetAdminTicketsAsync(query);

            return new AdminTicketPageDto
            {
                Items = tickets.Select(MapTicket).ToList(),
                Page = query.Page,
                PageSize = query.PageSize,
                TotalCount = totalCount
            };
        }

        public async Task<MyTicketResponseDto?> GetAdminTicketDetailsAsync(int ticketId)
        {
            if (ticketId <= 0)
                throw new ArgumentException("Ticket ID is invalid.");

            var ticket = await _ticketRepository.GetAdminTicketByIdAsync(ticketId);

            return ticket == null ? null : MapTicket(ticket);
        }

        public async Task<MyTicketResponseDto> ReissueTicketAsync(
            int ticketId,
            int adminUserId,
            string reason)
        {
            if (ticketId <= 0)
                throw new ArgumentException("Ticket ID is invalid.");

            if (adminUserId <= 0)
                throw new ArgumentException("Admin ID is invalid.");

            var normalizedReason = reason?.Trim();

            if (string.IsNullOrWhiteSpace(normalizedReason))
                throw new ArgumentException("Enter a reason for re-issuing the ticket.");

            if (normalizedReason.Length > 500)
                throw new ArgumentException("The reason cannot exceed 500 characters.");

            var replacement = await _ticketRepository.ReissueTicketAsync(
                ticketId,
                adminUserId,
                normalizedReason);

            return MapTicket(replacement);
        }

        private static MyTicketResponseDto MapTicket(Ticket ticket)
        {
            return new MyTicketResponseDto
            {
                TicketId = ticket.TicketId,
                TicketCode = ticket.TicketCode,
                OrderId = ticket.OrderId,
                EventId = ticket.EventId,
                TicketTypeId = ticket.TicketTypeId,
                TicketTypeName = ticket.TicketTypeName,
                SeatId = ticket.SeatId,
                OwnerName = ticket.OwnerName,
                Status = ticket.Status,
                CheckedInAt = ticket.CheckedInAt,
                EventName = ticket.Order.EventName,
                PosterUrl = ticket.Order.PosterUrl,
                StartsAt = ticket.Order.StartsAt,
                OrderDate = ticket.Order.OrderDate,
                OrderStatus = ticket.Order.Status,
                UnitPrice = ticket.Order.OrderDetails
                    .FirstOrDefault(detail =>
                        detail.TicketTypeId == ticket.TicketTypeId)
                    ?.UnitPrice,
                HasOwner = ticket.OwnerUserId.HasValue || !string.IsNullOrWhiteSpace(ticket.OwnerName),
                HasQrToken = ticket.TicketQrTokens.Any(token => !token.IsRevoked)
            };
        }
    }
}
