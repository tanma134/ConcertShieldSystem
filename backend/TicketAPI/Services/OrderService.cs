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
        private readonly IPaymentAPIClient _paymentApiClient;
        private readonly IStatusHistoryService _statusHistoryService;

        public OrderService(TicketDbContext context, IEventApiClient eventApiClient,
            IHoldService holdService, IHttpClientFactory httpClientFactory,
            IOrderRepository orderRepository, ILogger<HoldService> logger,
            IEmailService emailService, ITicketRepository ticketRepository,
            IPaymentAPIClient paymentApiClient, IStatusHistoryService statusHistoryService)
        {
            _context = context;
            _eventApiClient = eventApiClient;
            _holdService = holdService;
            _httpClientFactory = httpClientFactory;
            _orderRepository = orderRepository;
            _logger = logger;
            _emailService = emailService;
            _ticketRepository = ticketRepository;
            _paymentApiClient = paymentApiClient; _statusHistoryService = statusHistoryService;
        }

        public async Task<int> Add(CreateOrderDTO dto, int customerId)
        {
            if (string.IsNullOrWhiteSpace(dto.HoldId))
                throw new ArgumentException("Invalid hold ID.");

            var existingOrderId = await _orderRepository.GetExistingOrderIdByHoldAsync(dto.HoldId, customerId);

            if (existingOrderId.HasValue) return existingOrderId.Value;

            var hold = await _holdService.GetHoldDetailsAsync(dto.HoldId, customerId);

            if (hold.EventId != dto.EventId)
                throw new ArgumentException("The hold does not belong to this event.");

            await ValidateRedisHoldAsync(dto, customerId);

            var eventInfo = await _eventApiClient.GetEventByIdAsync(dto.EventId);
            if (eventInfo == null) throw new Exception("Event not found.");
            if (eventInfo.Status != "Published") throw new Exception($"The event is not open for sale. (Status: {eventInfo.Status})");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (_context.Database.IsRelational())
                {
                    await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(9823, {customerId})");
                }

                var requestedQuantity = dto.OrderDetails.Sum(x => x.Quantity);
                if (eventInfo.MinTicketsPerAccount is > 0 && requestedQuantity < eventInfo.MinTicketsPerAccount.Value)
                {
                    throw new InvalidOperationException($"The requested ticket quantity ({requestedQuantity}) is below the event minimum ({eventInfo.MinTicketsPerAccount.Value}).");
                }

                if (eventInfo.MaxTicketsPerAccount is > 0)
                {
                    var existingQuantity =
                        await _orderRepository.GetCustomerEventTicketQuantityAsync(
                            customerId,
                            dto.EventId);

                    if (existingQuantity + requestedQuantity > eventInfo.MaxTicketsPerAccount.Value)
                    {
                        throw new InvalidOperationException(
                            $"You have already ordered {existingQuantity} ticket(s). The event limit is " +
                            $"{eventInfo.MaxTicketsPerAccount.Value} ticket(s) per account.");
                    }
                }

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
                    if (ticketType == null) throw new Exception($"Ticket type ID {detailDto.TicketTypeId} does not exist.");

                    var nowUtc = DateTime.UtcNow;

                    if (ticketType.Status != "Active" ||
                        (ticketType.SalesStartsAt.HasValue &&
                         ticketType.SalesStartsAt.Value > nowUtc) ||
                        (ticketType.SalesEndsAt ?? eventInfo.StartsAt.AddHours(-5)) <= nowUtc)
                    {
                        throw new InvalidOperationException(
                            $"Ticket type {ticketType.TypeName} is not currently on sale.");
                    }

                    if (detailDto.Quantity < ticketType.MinPerOrder)
                        throw new InvalidOperationException(
                            $"The minimum quantity for ticket type {ticketType.TypeName} is {ticketType.MinPerOrder}.");
                    if (detailDto.Quantity > ticketType.MaxPerOrder)
                        throw new InvalidOperationException(
                            $"The maximum quantity for ticket type {ticketType.TypeName} is {ticketType.MaxPerOrder}.");

                    string? seatIdsJson = null;

                    if (dto.IsReservedSeating)
                    {
                        var seatIds = detailDto.SeatIds;

                        if (seatIds == null ||
                            seatIds.Count != detailDto.Quantity ||
                            seatIds.Any(id => id <= 0))
                        {
                            throw new Exception("The seat list is invalid or a SeatId is missing.");
                        }

                        seatIdsJson = JsonSerializer.Serialize(seatIds);
                    }

                    var orderDetail = new OrderDetail
                    {
                        TicketTypeId = detailDto.TicketTypeId,
                        TicketTypeName = ticketType.TypeName,
                        Quantity = detailDto.Quantity,
                        UnitPrice = ticketType.EffectivePrice ?? ticketType.Price,
                        SeatIds = seatIdsJson
                    };

                    newOrder.OrderDetails.Add(orderDetail);
                    totalAmount += orderDetail.UnitPrice * orderDetail.Quantity;
                }

                newOrder.TotalAmount = totalAmount;
                newOrder.FinalAmount = totalAmount;
                newOrder.DiscountAmount = 0;

                if (!string.IsNullOrWhiteSpace(dto.VoucherCode))
                {
                    var voucherResult = await _paymentApiClient.ApplyVoucherAsync(
                        new ApplyVoucherRequestDto
                        {
                            Code = dto.VoucherCode.Trim(),
                            EventId = dto.EventId,
                            OrderAmount = totalAmount
                        });

                    if (!voucherResult.IsValid)
                    {
                        throw new ArgumentException(
                            voucherResult.Message ?? "Invalid voucher.");
                    }

                    newOrder.DiscountAmount = voucherResult.DiscountAmount;
                    newOrder.FinalAmount = voucherResult.FinalAmount;
                    newOrder.VoucherId = voucherResult.VoucherId;
                }

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

                await _orderRepository.AddCompleteOrderAsync(newOrder);

                await _context.SaveChangesAsync();

                await _statusHistoryService.RecordAsync(new StatusHistory
                {
                    TargetType = "Order",
                    TargetId = newOrder.OrderId,
                    EventId = newOrder.EventId,
                    OrderId = newOrder.OrderId,
                    HoldId = newOrder.HoldToken,
                    CustomerId = customerId,
                    Action = "OrderCreated",
                    OldStatus = null,
                    NewStatus = "Pending",
                    Quantity = newOrder.OrderDetails.Sum(detail => detail.Quantity),
                    ActorUserId = customerId,
                    ActorRole = "Customer",
                    Metadata = "{}"
                });

                await transaction.CommitAsync();

                return newOrder.OrderId;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                var realError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;

                _logger.LogError(ex, "Failed to create order details: {RealError}", realError);

                throw new Exception("A system error occurred while creating the order: " + realError);
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
                        throw new Exception($"Please select seats for ticket type {detail.TicketTypeId}.");
                    }
                    foreach (var seatId in detail.SeatIds)
                    {
                        var holdInfo = await _holdService.GetHoldInfoAsync(dto.EventId, seatId);
                        if (holdInfo == null || holdInfo.UserId != userId)
                        {
                            throw new Exception($"Seat {seatId} has expired or was not selected by you.");
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
                        throw new Exception($"The number of held tickets for ticket type {detail.TicketTypeId} is insufficient or the hold has expired.");
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

        public async Task<Order?> GetOrderByOrderId(int orderId)
        {
            return await _orderRepository.GetOrderByOrderId(orderId);
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
                    "EventAPI could not update the number of tickets sold.");
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
                        Status = "Active",
                        OrderAttendeeId = attendee.AttendeeId
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

            var historyItems = new List<StatusHistory>
            {
                new()
                {
                    TargetType = "Order",
                    TargetId = order.OrderId,
                    EventId = order.EventId,
                    OrderId = order.OrderId,
                    HoldId = order.HoldToken,
                    CustomerId = order.CustomerId,
                    Action = "PaymentSucceeded",
                    OldStatus = "Pending",
                    NewStatus = "Paid",
                    Quantity = details.Sum(detail => detail.Quantity),
                    ActorRole = "System",
                    Metadata = "{}"
                }
            };

            foreach (var detail in details)
            {
                historyItems.Add(new StatusHistory
                {
                    TargetType = "TicketTypeInventory",
                    TargetId = detail.TicketTypeId,
                    EventId = order.EventId,
                    OrderId = order.OrderId,
                    TicketTypeId = detail.TicketTypeId,
                    HoldId = order.HoldToken,
                    CustomerId = order.CustomerId,
                    Action = "InventorySold",
                    OldStatus = "Held",
                    NewStatus = "Sold",
                    Quantity = detail.Quantity,
                    ActorRole = "System",
                    Metadata = "{}"
                });
            }

            foreach (var ticket in tickets)
            {
                historyItems.Add(new StatusHistory
                {
                    TargetType = "Ticket",
                    TargetId = ticket.TicketId,
                    EventId = ticket.EventId,
                    OrderId = ticket.OrderId,
                    TicketId = ticket.TicketId,
                    TicketTypeId = ticket.TicketTypeId,
                    SeatId = ticket.SeatId,
                    HoldId = order.HoldToken,
                    CustomerId = ticket.OwnerUserId,
                    Action = "TicketIssued",
                    OldStatus = null,
                    NewStatus = "Active",
                    ActorRole = "System",
                    Metadata = "{}"
                });

                if (ticket.SeatId.HasValue)
                {
                    historyItems.Add(new StatusHistory
                    {
                        TargetType = "Seat",
                        TargetId = ticket.SeatId.Value,
                        EventId = ticket.EventId,
                        OrderId = ticket.OrderId,
                        TicketId = ticket.TicketId,
                        TicketTypeId = ticket.TicketTypeId,
                        SeatId = ticket.SeatId.Value,
                        HoldId = order.HoldToken,
                        CustomerId = ticket.OwnerUserId,
                        Action = "SeatSold",
                        OldStatus = "Held",
                        NewStatus = "Sold",
                        ActorRole = "System",
                        Metadata = "{}"
                    });
                }
            }

            await _statusHistoryService.RecordRangeAsync(historyItems);

            await dbTransaction.CommitAsync();

            if (!string.IsNullOrWhiteSpace(order.HoldToken))
            {
                try
                {
                    await _holdService.CancelHoldSessionAsync(order.HoldToken, order.CustomerId);
                }
                catch (KeyNotFoundException)
                {
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Order {OrderId} was paid, but hold {HoldId} could not be cleaned up",
                        order.OrderId,
                        order.HoldToken);
                }
            }

            try
            {
                await _emailService.SendPaymentSuccessAsync(order, tickets);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to send the payment confirmation email for order {OrderId}",
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
                throw new KeyNotFoundException("Ticket not found.");

            if (ticket.Order.Status != "Paid" ||
                ticket.Status != "Active" ||
                ticket.CheckedInAt != null)
            {
                throw new InvalidOperationException(
                    "The ticket is unpaid, inactive, or has already been checked in.");
            }

            var now = DateTime.UtcNow;

            var currentToken = ticket.TicketQrTokens
                .Where(qr => !qr.IsRevoked)
                .OrderByDescending(qr => qr.IssuedAt)
                .FirstOrDefault();

            var tokenRandom = GenerateQrToken();
            var codeStr = ticket.TicketCode != Guid.Empty ? ticket.TicketCode.ToString() : ticket.TicketId.ToString();
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var newToken = $"{codeStr}:{timestamp}:{tokenRandom}";
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
                HoldId = order.HoldToken,
                TicketCount = order.OrderDetails?.Sum(detail => detail.Quantity) ?? 0
            }).ToList();
        }

        public async Task<OrderVoucherInfoDto?> GetOrderVoucherInfoAsync(int orderId)
        {
            var order = await _orderRepository.GetOrderByOrderId(orderId);

            if (order == null)
                return null;

            return new OrderVoucherInfoDto
            {
                VoucherId = order.VoucherId,
                UserId = order.CustomerId,
                DiscountAmount = order.DiscountAmount ?? 0
            };
        }

        public async Task<OrderReconciliationInfoDto?> GetOrderReconciliationInfoAsync(int orderId)
        {
            var order = await _orderRepository.GetOrderByOrderId(orderId);

            if (order == null || order.IsDeleted)
                return null;

            var tickets = order.Tickets
                .Where(ticket => !ticket.IsDeleted)
                .ToList();

            return new OrderReconciliationInfoDto
            {
                OrderId = order.OrderId,
                EventId = order.EventId,
                Status = order.Status,
                FinalAmount = order.FinalAmount,
                PaymentGatewayRef = order.PaymentGatewayRef,
                ExpectedTicketCount = order.OrderDetails.Sum(detail => detail.Quantity),
                IssuedTicketCount = tickets.Count,
                TicketsWithoutQrCount = tickets.Count(
                    ticket => !ticket.TicketQrTokens.Any())
            };
        }

        public async Task RepairPaidOrderArtifactsAsync(int orderId, long amount, string transactionRef)
        {
            if (orderId <= 0)
                throw new ArgumentException("Invalid order ID.");

            if (amount <= 0)
                throw new ArgumentException("Invalid payment amount.");

            if (string.IsNullOrWhiteSpace(transactionRef))
                throw new ArgumentException("Invalid transaction reference.");

            await using var dbTransaction =
                await _context.Database.BeginTransactionAsync();

            var order = await _orderRepository.GetOrderByOrderId(orderId);

            if (order == null || order.IsDeleted)
                throw new KeyNotFoundException("Order not found.");

            if (!string.Equals(order.Status, "Paid", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Tickets can only be recovered for a paid order.");
            }

            if (order.FinalAmount != amount)
                throw new InvalidOperationException(
                    "The payment amount does not match the order's final amount.");

            if (string.IsNullOrWhiteSpace(order.PaymentGatewayRef) ||
                !string.Equals(
                    order.PaymentGatewayRef,
                    transactionRef,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The transaction reference does not match the order.");
            }

            var details = order.OrderDetails.ToList();

            var attendees = order.OrderAttendees
                .OrderBy(attendee => attendee.AttendeeId)
                .ToList();

            var expectedTicketCount = details.Sum(detail => detail.Quantity);

            var existingTickets = order.Tickets
                .Where(ticket => !ticket.IsDeleted)
                .ToList();

            if (attendees.Count != expectedTicketCount)
            {
                throw new InvalidOperationException(
                    "The attendee count does not match the ticket quantity in the order.");
            }

            if (attendees.Any(attendee => !attendee.TicketTypeId.HasValue))
            {
                throw new InvalidOperationException(
                    "An attendee has not been assigned a ticket type.");
            }

            if (existingTickets.Count > expectedTicketCount)
            {
                throw new InvalidOperationException(
                    "The order already has more tickets than expected.");
            }

            // Nếu đang thiếu ticket nhưng ticket cũ chưa liên kết attendee,
            // không thể xác định an toàn attendee nào còn thiếu.
            if (existingTickets.Count < expectedTicketCount &&
                existingTickets.Any(ticket => !ticket.OrderAttendeeId.HasValue))
            {
                throw new InvalidOperationException(
                    "Missing tickets cannot be issued because an existing ticket is not linked to an attendee.");
            }

            var attendeeById = attendees.ToDictionary(
                attendee => attendee.AttendeeId);

            var linkedAttendeeIds = existingTickets
                .Where(ticket => ticket.OrderAttendeeId.HasValue)
                .Select(ticket => ticket.OrderAttendeeId!.Value)
                .ToList();

            if (linkedAttendeeIds.Count != linkedAttendeeIds.Distinct().Count())
            {
                throw new InvalidOperationException(
                    "An attendee is linked to multiple tickets.");
            }

            foreach (var ticket in existingTickets
                         .Where(ticket => ticket.OrderAttendeeId.HasValue))
            {
                var attendeeId = ticket.OrderAttendeeId!.Value;

                if (!attendeeById.TryGetValue(attendeeId, out var attendee))
                {
                    throw new InvalidOperationException(
                        $"Ticket {ticket.TicketId} is linked to an attendee who does not belong to this order.");
                }

                if (attendee.TicketTypeId != ticket.TicketTypeId)
                {
                    throw new InvalidOperationException(
                        $"Ticket {ticket.TicketId} does not match the attendee's ticket type.");
                }
            }

            var issuedAttendeeIds = linkedAttendeeIds.ToHashSet();

            // Đủ ticket rồi thì không tạo ticket mới.
            // Nếu thiếu ticket, chỉ lấy attendee chưa được liên kết với ticket.
            var attendeeIdsToIssue = existingTickets.Count == expectedTicketCount
                ? new HashSet<int>()
                : attendees
                    .Where(attendee =>
                        !issuedAttendeeIds.Contains(attendee.AttendeeId))
                    .Select(attendee => attendee.AttendeeId)
                    .ToHashSet();

            if (existingTickets.Count + attendeeIdsToIssue.Count != expectedTicketCount)
            {
                throw new InvalidOperationException(
                    "The number of attendees requiring tickets does not match the order's ticket quantity.");
            }

            var createdTickets = new List<Ticket>();

            foreach (var detail in details)
            {
                var attendeesForType = attendees
                    .Where(attendee =>
                        attendee.TicketTypeId == detail.TicketTypeId)
                    .ToList();

                if (attendeesForType.Count != detail.Quantity)
                {
                    throw new InvalidOperationException(
                        $"The attendee count does not match ticket type {detail.TicketTypeId}.");
                }

                var orderSeatIds = string.IsNullOrWhiteSpace(detail.SeatIds)
                    ? new List<int>()
                    : JsonSerializer.Deserialize<List<int>>(detail.SeatIds)
                        ?? new List<int>();

                var attendeeSeatIds = attendeesForType
                    .Where(attendee => attendee.SeatId.HasValue)
                    .Select(attendee => attendee.SeatId!.Value)
                    .ToList();

                if (orderSeatIds.Count > 0 &&
                    (orderSeatIds.Count != detail.Quantity ||
                     attendeeSeatIds.Count != detail.Quantity ||
                     !orderSeatIds.ToHashSet().SetEquals(attendeeSeatIds)))
                {
                    throw new InvalidOperationException(
                        $"The seat list for ticket type {detail.TicketTypeId} does not match the attendees.");
                }

                if (orderSeatIds.Count == 0 && attendeeSeatIds.Count > 0)
                {
                    throw new InvalidOperationException(
                        $"An attendee has a SeatId, but the order has no saved seat list for ticket type {detail.TicketTypeId}.");
                }

                var attendeesToIssueForType = attendeesForType
                    .Where(attendee =>
                        attendeeIdsToIssue.Contains(attendee.AttendeeId))
                    .ToList();

                foreach (var attendee in attendeesToIssueForType)
                {
                    createdTickets.Add(new Ticket
                    {
                        TicketCode = Guid.NewGuid(),
                        OrderId = order.OrderId,
                        OrderAttendeeId = attendee.AttendeeId,
                        EventId = order.EventId,
                        TicketTypeId = detail.TicketTypeId,
                        TicketTypeName = detail.TicketTypeName,
                        SeatId = attendee.SeatId,
                        OwnerUserId = order.CustomerId,
                        OwnerName = attendee.FullName,
                        Status = "Active",
                        IsDeleted = false
                    });
                }
            }

            if (createdTickets.Count > 0)
            {
                await _ticketRepository.AddRangeAsync(createdTickets);
                await _context.SaveChangesAsync();
            }

            var allTickets = existingTickets
                .Concat(createdTickets)
                .ToList();

            var now = DateTime.UtcNow;

            var ticketsMissingQr = allTickets
                .Where(ticket => !ticket.TicketQrTokens.Any())
                .ToList();

            var qrTokens = ticketsMissingQr
                .Select(ticket => new TicketQrToken
                {
                    TicketId = ticket.TicketId,
                    QrToken = GenerateQrToken(),
                    IssuedAt = now,
                    ExpiresAt = now.AddSeconds(60),
                    IsRevoked = false,
                    IsUsed = false
                })
                .ToList();

            if (qrTokens.Count > 0)
            {
                await _ticketRepository.AddQrTokensAsync(qrTokens);
                await _context.SaveChangesAsync();
            }

            if (createdTickets.Count > 0)
            {
                var historyItems = createdTickets
                    .Select(ticket => new StatusHistory
                    {
                        TargetType = "Ticket",
                        TargetId = ticket.TicketId,
                        EventId = ticket.EventId,
                        OrderId = ticket.OrderId,
                        TicketId = ticket.TicketId,
                        TicketTypeId = ticket.TicketTypeId,
                        SeatId = ticket.SeatId,
                        CustomerId = ticket.OwnerUserId,
                        Action = "TicketIssued",
                        OldStatus = null,
                        NewStatus = "Active",
                        ActorRole = "System",
                        Metadata = "{\"source\":\"PaymentReconciliationRepair\"}"
                    })
                    .ToList();

                await _statusHistoryService.RecordRangeAsync(historyItems);
            }

            await dbTransaction.CommitAsync();
        }

        public async Task<OrganizerOrderPageDto> GetOrganizerEventOrdersAsync(
            int eventId,
            string? bearerToken,
            OrganizerOrderQueryDto query)
        {
            if (eventId <= 0)
                throw new ArgumentException("Invalid event ID.");

            if (query.FromDate.HasValue &&
                query.ToDate.HasValue &&
                query.FromDate.Value > query.ToDate.Value)
            {
                throw new ArgumentException(
                    "FromDate must be earlier than or equal to ToDate.");
            }

            var eventInfo = await _eventApiClient.GetOwnedEventAsync(eventId, bearerToken);

            if (eventInfo == null)
                throw new KeyNotFoundException("Event not found.");

            var page = Math.Max(query.Page, 1);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);

            DateTime? fromDate = query.FromDate?.ToDateTime(TimeOnly.MinValue);

            DateTime? toDateExclusive = query.ToDate?
                .AddDays(1)
                .ToDateTime(TimeOnly.MinValue);

            var (orders, totalCount) = await _orderRepository.GetEventOrdersAsync(
                eventId,
                query.Search,
                query.Status,
                fromDate,
                toDateExclusive,
                page,
                pageSize);

            return new OrganizerOrderPageDto
            {
                EventId = eventInfo.EventId,
                EventName = eventInfo.Title,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                Items = orders.Select(order => new OrganizerOrderItemDto
                {
                    OrderId = order.OrderId,
                    EventId = order.EventId,
                    EventName = order.EventName ?? eventInfo.Title,
                    CustomerId = order.CustomerId,
                    CustomerName = order.FullName,
                    CustomerEmail = order.Email,
                    OrderDate = order.OrderDate,
                    TicketCount = order.OrderDetails.Sum(detail => detail.Quantity),
                    TotalAmount = order.TotalAmount,
                    DiscountAmount = order.DiscountAmount ?? 0,
                    FinalAmount = order.FinalAmount,
                    PaymentMethod = order.PaymentMethod,
                    Status = order.Status
                }).ToList()
            };
        }

        public async Task ExpirePendingOrdersAsync()
        {
            var now = DateTime.UtcNow;
            var expiredOrders =
                await _orderRepository.GetExpiredPendingOrdersAsync(now);

            foreach (var order in expiredOrders)
            {
                var changed = await _orderRepository.TryMarkOrderExpiredAsync(order.OrderId, now);

                if (!changed)
                    continue;

                await _paymentApiClient.CancelPendingPaymentsAsync(order.OrderId);

                await _statusHistoryService.RecordAsync(new StatusHistory
                {
                    TargetType = "Order",
                    TargetId = order.OrderId,
                    EventId = order.EventId,
                    OrderId = order.OrderId,
                    HoldId = order.HoldToken,
                    CustomerId = order.CustomerId,
                    Action = "OrderExpired",
                    OldStatus = "Pending",
                    NewStatus = "Expired",
                    ActorRole = "System",
                    Metadata = "{}"
                });
            }
        }

        public async Task CancelPendingOrderAsync(int orderId)
        {
            if (orderId <= 0)
                throw new ArgumentOutOfRangeException(nameof(orderId));

            var order = await _orderRepository.GetOrderByOrderId(orderId);

            if (order == null || order.IsDeleted)
                throw new KeyNotFoundException($"Order #{orderId} was not found.");

            if (string.Equals(order.Status, "Cancelled", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(order.Status, "Expired", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!string.Equals(order.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Only a Pending order can be cancelled. Current status: {order.Status}.");
            }

            if (!string.IsNullOrWhiteSpace(order.HoldToken))
            {
                try
                {
                    await _holdService.CancelHoldSessionAsync(
                        order.HoldToken,
                        order.CustomerId);
                }
                catch (KeyNotFoundException)
                {
                }
            }

            order.Status = "Cancelled";
            await _orderRepository.UpdateAsync(order);

            await _statusHistoryService.RecordAsync(new StatusHistory
            {
                TargetType = "Order",
                TargetId = order.OrderId,
                EventId = order.EventId,
                OrderId = order.OrderId,
                HoldId = order.HoldToken,
                CustomerId = order.CustomerId,
                Action = "OrderCancelled",
                OldStatus = "Pending",
                NewStatus = "Cancelled",
                ActorRole = "System",
                Reason = "Customer cancelled payment on VNPay.",
                Metadata = "{}"
            });
        }
    }
}
