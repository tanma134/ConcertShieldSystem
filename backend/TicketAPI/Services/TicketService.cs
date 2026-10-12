using TicketAPI.DTOs;
using TicketAPI.Repositories;

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
    }
}
