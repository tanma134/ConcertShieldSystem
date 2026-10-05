using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TicketAPI.API;
using TicketAPI.DTOs;
using TicketAPI.Models;
using TicketAPI.Repositories;
using TicketAPI.Services;
using static TicketAPI.DTOs.ConfirmEventSaleDto;

namespace TicketAPI.Services
{
    public class OrderService : IOrderService
    {
        private readonly TicketDbContext _context;
        private readonly IEventApiClient _eventApiClient;
        private readonly IHoldService _holdService;
        private readonly IEmailService _emailService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOrderRepository _orderRepository;
        private readonly ITicketRepository _ticketRepository;
        private readonly ILogger<HoldService> _logger;

        public OrderService(TicketDbContext context, IEventApiClient eventApiClient, IHoldService holdService, IHttpClientFactory httpClientFactory, IOrderRepository orderRepository, ILogger<HoldService> logger, IEmailService emailService, ITicketRepository ticketRepository)
        {
            _context = context;
            _eventApiClient = eventApiClient;
            _holdService = holdService;
            _httpClientFactory = httpClientFactory;
            _orderRepository = orderRepository;
            _logger = logger;
            _emailService = emailService;
            _ticketRepository = ticketRepository;
        }

        public async Task<int> Add(CreateOrderDTO dto, int customerId)
        {
            if (string.IsNullOrWhiteSpace(dto.HoldId))
                throw new ArgumentException("HoldId không hợp lệ.");

            var hold = await _holdService.GetHoldDetailsAsync(
                dto.HoldId,
                customerId);

            if (hold.EventId != dto.EventId)
                throw new ArgumentException("Hold không thuộc sự kiện này.");

            await ValidateRedisHoldAsync(dto, customerId);

            var eventInfo = await _eventApiClient.GetEventByIdAsync(dto.EventId);
            if (eventInfo == null) throw new Exception("Không tìm thấy sự kiện");
            if (eventInfo.Status != "Published") throw new Exception($"Sự kiện không mở bán. (Trạng thái: {eventInfo.Status})");

            var newOrder = new Order
            {
                FullName = dto.FullName,
                Email = dto.Email,
                Phone = dto.Phone,
                CustomerId = customerId,
                EventId = dto.EventId,
                EventName = eventInfo.Title,
                PosterUrl = eventInfo.PosterUrl,

                StartsAt = eventInfo.StartsAt,
                EndsAt = eventInfo.EndsAt,

                HoldToken = hold.HoldId,
                ExpiresAt = hold.ExpiresAtUtc,
                Status = "Pending",

                OrderDate = DateTime.UtcNow,

                IsDeleted = false,

                OrderDetails = new List<OrderDetail>(),
                OrderAttendees = new List<OrderAttendee>()
            };

            long totalAmount = 0;
            foreach (var detailDto in dto.OrderDetails)
            {
                var ticketType = eventInfo.TicketTypes.FirstOrDefault(t => t.TicketTypeId == detailDto.TicketTypeId);
                if (ticketType == null) throw new Exception($"Loại vé ID {detailDto.TicketTypeId} không tồn tại.");

                if (detailDto.Quantity < ticketType.MinPerOrder) throw new Exception($"Vé {ticketType.TypeName} yêu cầu ít nhất {ticketType.MinPerOrder} vé.");
                if (detailDto.Quantity > ticketType.MaxPerOrder) throw new Exception($"Vé {ticketType.TypeName} tối đa {ticketType.MaxPerOrder} vé.");

                string? seatIdsJson = null;

                if (dto.IsReservedSeating)
                {
                    var seatIds = detailDto.SeatIds;

                    if (seatIds == null ||
                        seatIds.Count != detailDto.Quantity ||
                        seatIds.Any(id => id <= 0))
                    {
                        throw new Exception("Danh sách ghế không hợp lệ hoặc thiếu SeatId.");
                    }

                    seatIdsJson = JsonSerializer.Serialize(seatIds);
                }

                var orderDetail = new OrderDetail
                {
                    TicketTypeId = detailDto.TicketTypeId,
                    TicketTypeName = ticketType.TypeName,
                    Quantity = detailDto.Quantity,
                    UnitPrice = ticketType.Price,
                    SeatIds = seatIdsJson
                };

                newOrder.OrderDetails.Add(orderDetail);
                totalAmount += orderDetail.UnitPrice * orderDetail.Quantity;
            }

            newOrder.TotalAmount = totalAmount;
            newOrder.FinalAmount = totalAmount;
            newOrder.DiscountAmount = 0;

            if (dto.Attendees != null && dto.Attendees.Any())
            {
                foreach (var att in dto.Attendees)
                {
                    newOrder.OrderAttendees.Add(new OrderAttendee
                    {
                        FullName = att.FullName,
                        CitizenId = att.CitizenId,
                        Phone = att.Phone,
                        TicketTypeId = att.TicketTypeId,
                        SeatId = att.SeatId,
                        IsPrimaryBuyer = att.IsPrimaryBuyer
                    });
                }
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _orderRepository.AddCompleteOrderAsync(newOrder);

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return newOrder.OrderId;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                var realError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;

                _logger.LogError(ex, "Lỗi tạo đơn hàng chi tiết: {RealError}", realError);

                throw new Exception("Lỗi hệ thống khi tạo đơn hàng: " + realError);
            }
        }
        private async Task ValidateRedisHoldAsync(CreateOrderDTO dto, int userId)
        {
            if (dto.IsReservedSeating)
            {
                foreach (var detail in dto.OrderDetails)
                {
                    if (detail.SeatIds == null || !detail.SeatIds.Any())
                    {
                        throw new Exception($"Vui lòng chọn ghế cho loại vé {detail.TicketTypeId}.");
                    }
                    foreach (var seatId in detail.SeatIds)
                    {
                        var holdInfo = await _holdService.GetHoldInfoAsync(dto.EventId, seatId);
                        if (holdInfo == null || holdInfo.UserId != userId)
                        {
                            throw new Exception($"Ghế {seatId} đã hết hạn giữ hoặc không phải do bạn chọn.");
                        }
                    }
                }
            }
            else
            {
                foreach (var detail in dto.OrderDetails)
                {
                    var holdInfo = await _holdService.GetTicketHoldInfoAsync(detail.TicketTypeId, userId);

                    if (holdInfo == null || holdInfo.Quantity < detail.Quantity)
                    {
                        throw new Exception($"Số lượng vé bạn giữ cho loại vé {detail.TicketTypeId} không đủ hoặc đã hết hạn.");
                    }
                }
            }
        }

        public async Task<List<int>> GetBookedSeatIdsAsync(int eventId)
        {
            var seatIdJsonList = await _context.OrderDetails
                .AsNoTracking()
                .Where(detail =>
                    detail.Order.EventId == eventId &&
                    detail.Order.Status == "Paid" &&
                    !detail.Order.IsDeleted &&
                    detail.SeatIds != null)
                .Select(detail => detail.SeatIds!)
                .ToListAsync();

            return seatIdJsonList
                .SelectMany(json =>
                    JsonSerializer.Deserialize<List<int>>(json) ?? new List<int>())
                .Distinct()
                .ToList();
        }

        public async Task<CreateVnPayPaymentRequestDto> GetOrderByOrderIdForPayment(int id)
        {
            try
            {
                var order = await _orderRepository.GetOrderByOrderId(id);
                if (order == null)
                {
                    throw new Exception("Not found order");
                }
                var createVnPayPaymentRequestDto = new CreateVnPayPaymentRequestDto
                {
                    FinalAmount = order.FinalAmount,
                    OrderId = order.OrderId,
                    ClientIp = "127.0.0.1"
                };
                return createVnPayPaymentRequestDto;
            }
            catch (Exception ex)
            {
                throw new Exception($"{ex.Message}");
            }
        }

        public async Task<Order?> GetOrderByOrderId(int orderId, int customerId)
        {
            var order = await _orderRepository.GetOrderByOrderId(orderId);

            if (order == null || order.CustomerId != customerId)
                return null;

            return order;
        }

        public async Task ConfirmPaymentAsync(int orderId, long amount, string transactionRef)
        {
            await using var dbTransaction =
                await _context.Database.BeginTransactionAsync();

            var order = await _orderRepository.GetOrderByOrderId(orderId);

            if (order == null)
                throw new KeyNotFoundException("Order not found.");

            if (order.Status == "Paid")
                return;

            if (order.Status != "Pending")
                throw new InvalidOperationException(
                    $"Cannot confirm payment for order with status '{order.Status}'.");

            if (order.FinalAmount != amount)
                throw new InvalidOperationException(
                    "Payment amount does not match the order amount.");

            var attendees = order.OrderAttendees
                .OrderBy(a => a.AttendeeId)
                .ToList();

            var details = order.OrderDetails.ToList();
            var expectedTicketCount = details.Sum(d => d.Quantity);

            if (attendees.Count != expectedTicketCount)
                throw new InvalidOperationException(
                    "The attendee count does not match the order ticket quantity.");

            if (details.GroupBy(d => d.TicketTypeId).Any(g => g.Count() > 1))
                throw new InvalidOperationException(
                    "The order contains duplicate details for a ticket type.");

            var saleRequest = new ConfirmEventSaleRequestDto
            {
                OrderId = order.OrderId,
                Items = details.Select(detail => new ConfirmEventSaleItemDto
                {
                    TicketTypeId = detail.TicketTypeId,
                    Quantity = detail.Quantity
                }).ToList()
            };

            var saleRecorded = await _eventApiClient.ConfirmPaidOrderSaleAsync(order.EventId, saleRequest);

            if (!saleRecorded)
            {
                throw new InvalidOperationException(
                    "EventAPI không cập nhật được số lượng vé đã bán.");
            }

            var tickets = new List<Ticket>();

            foreach (var detail in details)
            {
                var attendeesForType = attendees
                    .Where(a => a.TicketTypeId == detail.TicketTypeId)
                    .ToList();

                if (attendeesForType.Count != detail.Quantity)
                {
                    throw new InvalidOperationException(
                        $"Attendee count does not match quantity for ticket type {detail.TicketTypeId}.");
                }

                var orderSeatIds = string.IsNullOrWhiteSpace(detail.SeatIds)
                    ? new List<int>()
                    : JsonSerializer.Deserialize<List<int>>(detail.SeatIds)
                        ?? new List<int>();

                var attendeeSeatIds = attendeesForType
                    .Where(a => a.SeatId.HasValue)
                    .Select(a => a.SeatId!.Value)
                    .ToList();

                if (orderSeatIds.Count > 0)
                {
                    if (orderSeatIds.Count != detail.Quantity ||
                        attendeeSeatIds.Count != detail.Quantity ||
                        !orderSeatIds.ToHashSet().SetEquals(attendeeSeatIds))
                    {
                        throw new InvalidOperationException(
                            $"Attendee seats do not match the held seats for ticket type {detail.TicketTypeId}.");
                    }
                }
                else if (attendeeSeatIds.Count > 0)
                {
                    throw new InvalidOperationException(
                        $"Attendees have seat IDs, but the order has no held seats for ticket type {detail.TicketTypeId}.");
                }

                foreach (var attendee in attendeesForType)
                {
                    tickets.Add(new Ticket
                    {
                        TicketCode = Guid.NewGuid(),
                        OrderId = order.OrderId,
                        EventId = order.EventId,
                        TicketTypeId = detail.TicketTypeId,
                        TicketTypeName = detail.TicketTypeName,
                        SeatId = attendee.SeatId,
                        OwnerUserId = order.CustomerId,
                        OwnerName = attendee.FullName,
                        Status = "Active"
                    });
                }
            }

            order.Status = "Paid";
            order.PaymentMethod = "VNPay";
            order.PaymentGatewayRef = transactionRef;

            await _ticketRepository.AddRangeAsync(tickets);
            await _context.SaveChangesAsync();

            var now = DateTime.UtcNow;

            var qrTokens = tickets.Select(ticket => new TicketQrToken
            {
                TicketId = ticket.TicketId,
                QrToken = GenerateQrToken(),
                IssuedAt = now,
                ExpiresAt = now.AddSeconds(60),
                IsRevoked = false,
                RevokedAt = null,
                IsUsed = false,
                UsedAt = null
            }).ToList();

            await _ticketRepository.AddQrTokensAsync(qrTokens);
            await _context.SaveChangesAsync();

            await dbTransaction.CommitAsync();

            try
            {
                await _emailService.SendPaymentSuccessAsync(order, tickets);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Gửi email xác nhận thanh toán thất bại cho Order {OrderId}",
                    order.OrderId);
            }
        }

        private static string GenerateQrToken()
        {
            return Convert.ToBase64String(
                    System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');
        }

        public async Task<TicketQrResponseDto> RotateTicketQrAsync(int ticketId, int userId)
        {
            var ticket = await _ticketRepository.GetTicketForQrRotationAsync(ticketId, userId);

            if (ticket == null)
                throw new KeyNotFoundException("Không tìm thấy vé.");

            if (ticket.Order.Status != "Paid" ||
                ticket.Status != "Active" ||
                ticket.CheckedInAt != null)
            {
                throw new InvalidOperationException(
                    "Vé chưa thanh toán, không còn hoạt động hoặc đã check-in.");
            }

            var now = DateTime.UtcNow;

            var currentToken = ticket.TicketQrTokens
                .Where(qr => !qr.IsRevoked)
                .OrderByDescending(qr => qr.IssuedAt)
                .FirstOrDefault();

            var newToken = GenerateQrToken();
            var expiresAt = now.AddSeconds(60);

            if (currentToken == null)
            {
                currentToken = new TicketQrToken
                {
                    TicketId = ticket.TicketId,
                    QrToken = newToken,
                    IssuedAt = now,
                    ExpiresAt = expiresAt,
                    IsRevoked = false,
                    IsUsed = false
                };

                await _ticketRepository.AddQrTokensAsync(new[] { currentToken });
            }
            else
            {
                currentToken.QrToken = newToken;
                currentToken.IssuedAt = now;
                currentToken.ExpiresAt = expiresAt;
                currentToken.IsUsed = false;
                currentToken.UsedAt = null;
                currentToken.RevokedAt = null;
            }

            await _context.SaveChangesAsync();

            return new TicketQrResponseDto
            {
                TicketId = ticket.TicketId,
                QrToken = newToken,
                ExpiresAtUtc = expiresAt
            };
        }

        public async Task<List<OrderHistoryItemDto>> GetOrderHistoryAsync(int customerId)
        {
            var orders = await _orderRepository.GetOrdersByCustomerIdAsync(customerId);

            return orders.Select(order => new OrderHistoryItemDto
            {
                OrderId = order.OrderId,
                EventId = order.EventId,
                EventName = order.EventName,
                PosterUrl = order.PosterUrl,
                OrderDate = order.OrderDate,
                StartsAt = order.StartsAt,
                TotalAmount = order.TotalAmount,
                DiscountAmount = order.DiscountAmount ?? 0,
                FinalAmount = order.FinalAmount,
                PaymentMethod = order.PaymentMethod,
                Status = order.Status,
                ExpiresAt = order.ExpiresAt,
                TicketCount = order.OrderDetails?.Sum(detail => detail.Quantity) ?? 0
            }).ToList();
        }
    }
}