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
                        voucherResult.Message ?? "Voucher không hợp lệ.");
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

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
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
                ExpectedTicketCount = order.OrderDetails.Sum(detail => detail.Quantity),
                IssuedTicketCount = tickets.Count,
                TicketsWithoutQrCount = tickets.Count(
                    ticket => !ticket.TicketQrTokens.Any())
            };
        }

        public async Task RepairPaidOrderArtifactsAsync(
            int orderId,
            long amount,
            string transactionRef)
        {
            if (orderId <= 0)
                throw new ArgumentException("Order ID không hợp lệ.");

            if (amount <= 0)
                throw new ArgumentException("Payment amount không hợp lệ.");

            if (string.IsNullOrWhiteSpace(transactionRef))
                throw new ArgumentException("Transaction reference không hợp lệ.");

            await using var dbTransaction =
                await _context.Database.BeginTransactionAsync();

            var order = await _orderRepository.GetOrderByOrderId(orderId);

            if (order == null || order.IsDeleted)
                throw new KeyNotFoundException("Order không tồn tại.");

            if (!string.Equals(
                    order.Status,
                    "Paid",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Chỉ có thể khôi phục vé cho order đã thanh toán.");
            }

            if (order.FinalAmount != amount)
                throw new InvalidOperationException(
                    "Payment amount không khớp với FinalAmount của order.");

            if (string.IsNullOrWhiteSpace(order.PaymentGatewayRef) ||
                !string.Equals(
                    order.PaymentGatewayRef,
                    transactionRef,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Transaction reference không khớp với order.");
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
                throw new InvalidOperationException(
                    "Số attendee không khớp với số vé trong order.");

            if (existingTickets.Count > expectedTicketCount)
                throw new InvalidOperationException(
                    "Order đã có nhiều ticket hơn số lượng dự kiến.");

            // Hiện Ticket không lưu OrderAttendeeId nên không thể xác định
            // an toàn attendee nào còn thiếu khi chỉ có một phần ticket.
            if (existingTickets.Count > 0 &&
                existingTickets.Count < expectedTicketCount)
            {
                throw new InvalidOperationException(
                    "Order chỉ thiếu một phần ticket. Cần kiểm tra thủ công để tránh tạo trùng hoặc gán sai attendee.");
            }

            var createdTickets = new List<Ticket>();

            if (existingTickets.Count == 0)
            {
                foreach (var detail in details)
                {
                    var attendeesForType = attendees
                        .Where(attendee =>
                            attendee.TicketTypeId == detail.TicketTypeId)
                        .ToList();

                    if (attendeesForType.Count != detail.Quantity)
                    {
                        throw new InvalidOperationException(
                            $"Số attendee không khớp với loại vé {detail.TicketTypeId}.");
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
                            $"Danh sách ghế của loại vé {detail.TicketTypeId} không khớp với attendees.");
                    }

                    if (orderSeatIds.Count == 0 && attendeeSeatIds.Count > 0)
                    {
                        throw new InvalidOperationException(
                            $"Attendee có SeatId nhưng order không lưu danh sách ghế cho loại vé {detail.TicketTypeId}.");
                    }

                    foreach (var attendee in attendeesForType)
                    {
                        createdTickets.Add(new Ticket
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
                            IsDeleted = false
                        });
                    }
                }

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
    }
}