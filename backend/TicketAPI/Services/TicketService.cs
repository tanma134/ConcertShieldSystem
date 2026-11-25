using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TicketAPI.API;
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
        private readonly IEventApiClient? _eventApiClient;

        public TicketService(ITicketRepository ticketRepository, IEventApiClient? eventApiClient = null)
        {
            _ticketRepository = ticketRepository;
            _eventApiClient = eventApiClient;
        }

        private static string ResolveGateName(Ticket ticket, List<EventGateDto>? gates)
        {
            if (!string.IsNullOrWhiteSpace(ticket.CheckedInGate))
                return ticket.CheckedInGate;

            if (!string.IsNullOrWhiteSpace(ticket.GateId))
            {
                if (gates != null && gates.Count > 0)
                {
                    var matchById = gates.FirstOrDefault(g =>
                        g.GateId.ToString() == ticket.GateId ||
                        $"GATE_{g.GateId}".Equals(ticket.GateId, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(g.GateName, ticket.GateId, StringComparison.OrdinalIgnoreCase));
                    if (matchById != null)
                        return matchById.GateName;
                }
                return ticket.GateId;
            }

            if (gates != null && gates.Count > 0)
            {
                var matchingGate = gates.FirstOrDefault(g =>
                    (g.AllowedTicketTypeList != null && g.AllowedTicketTypeList.Any(t => string.Equals(t, ticket.TicketTypeName, StringComparison.OrdinalIgnoreCase))) ||
                    (!string.IsNullOrWhiteSpace(g.AllowedTicketTypes) && g.AllowedTicketTypes
                        .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Any(t => string.Equals(t, ticket.TicketTypeName, StringComparison.OrdinalIgnoreCase))));

                if (matchingGate != null)
                    return matchingGate.GateName;

                var generalGate = gates.FirstOrDefault(g => g.AllowedTicketTypeList == null || g.AllowedTicketTypeList.Count == 0);
                if (generalGate != null)
                    return generalGate.GateName;

                return gates[0].GateName;
            }

            return "Main Gate";
        }

        private static List<string> ResolvePermittedGates(Ticket ticket, List<EventGateDto>? gates)
        {
            if (gates == null || gates.Count == 0)
                return new List<string> { "Main Gate" };

            var matched = gates.Where(g =>
                g.AllowedTicketTypeList == null ||
                g.AllowedTicketTypeList.Count == 0 ||
                g.AllowedTicketTypeList.Any(t => string.Equals(t, ticket.TicketTypeName, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(g.AllowedTicketTypes) && g.AllowedTicketTypes
                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Any(t => string.Equals(t, ticket.TicketTypeName, StringComparison.OrdinalIgnoreCase)))
            ).Select(g => g.GateName).Distinct().ToList();

            if (matched.Count > 0)
                return matched;

            return gates.Select(g => g.GateName).Distinct().ToList();
        }

        public async Task<List<MyTicketResponseDto>> GetMyTicketsAsync(int userId)
        {
            var tickets = await _ticketRepository.GetMyTicketsAsync(userId);
            var eventIds = tickets.Select(t => t.EventId).Distinct().ToList();
            var gateMap = new Dictionary<int, List<EventGateDto>>();

            foreach (var eid in eventIds)
            {
                try
                {
                    var gates = _eventApiClient == null
                        ? new List<EventGateDto>()
                        : await _eventApiClient.GetEventGatesAsync(eid);
                    gateMap[eid] = gates;
                }
                catch
                {
                    gateMap[eid] = new List<EventGateDto>();
                }
            }

            return tickets.Select(ticket =>
            {
                var gates = gateMap.TryGetValue(ticket.EventId, out var gList) ? gList : null;
                var gateName = ResolveGateName(ticket, gates);
                var permittedGates = ResolvePermittedGates(ticket, gates);

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
                    GateName = gateName,
                    GateId = gateName,
                    CheckedInGate = ticket.CheckedInGate,
                    PermittedGates = permittedGates
                };
            }).ToList();
        }

        public async Task<MyTicketResponseDto?> GetMyTicketDetailsAsync(
            int ticketId,
            int userId)
        {
            var ticket = await _ticketRepository.GetMyTicketByIdAsync(ticketId, userId);

            if (ticket == null)
                return null;

            List<EventGateDto>? gates = null;
            try
            {
                if (_eventApiClient != null)
                    gates = await _eventApiClient.GetEventGatesAsync(ticket.EventId);
            }
            catch
            {
                // Fallback gracefully
            }

            var gateName = ResolveGateName(ticket, gates);
            var permittedGates = ResolvePermittedGates(ticket, gates);

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
                GateName = gateName,
                GateId = gateName,
                CheckedInGate = ticket.CheckedInGate,
                PermittedGates = permittedGates
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
                GateName = ResolveGateName(ticket, null),
                GateId = ResolveGateName(ticket, null),
                HasOwner = ticket.OwnerUserId.HasValue || !string.IsNullOrWhiteSpace(ticket.OwnerName),
                HasQrToken = ticket.TicketQrTokens.Any(token => !token.IsRevoked)
            };
        }
    }
}
