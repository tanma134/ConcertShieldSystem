using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentAPI.DTOs;
using System.Security.Claims;
using TicketAPI.API;
using TicketAPI.DTOs;
using TicketAPI.Services;

namespace TicketAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly ILogger<OrdersController> _logger;
        private readonly IPaymentAPIClient _paymentApiClient;

        public OrdersController(IOrderService orderService, ILogger<OrdersController> logger, IPaymentAPIClient paymentApiClient)
        {
            _orderService = orderService;
            _logger = logger;
            _paymentApiClient = paymentApiClient;
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ", errors = ModelState });

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized(new { success = false, message = "Token không hợp lệ!" });

            TimeZoneInfo vietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

            DateTime nowVn = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vietnamTimeZone);

            try
            {
                int orderId = await _orderService.Add(dto, userId);

                var order = await _orderService.GetOrderByOrderIdForPayment(orderId);

                if (order == null) return NotFound("Order not found");

                if (order.FinalAmount <= 0) return BadRequest("Invalid amount");

                CancellationToken cancellationToken = default;

                var paymentUrl = await _paymentApiClient.CreateVnPayPaymentAsync(order, cancellationToken);

                return Ok(new
                {
                    success = true,
                    message = "Order created. Continue to payment.",
                    orderId,
                    paymentUrl.PaymentUrl
                });

            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi tạo đơn hàng cho user {UserId}", userId);

                return BadRequest(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        [HttpGet("events/{eventId}/booked-seats")]
        public async Task<IActionResult> GetBookedSeatIds(int eventId)
        {
            if (eventId <= 0)
                return BadRequest(new { success = false, message = "EventId không hợp lệ." });

            var seatIds = await _orderService.GetBookedSeatIdsAsync(eventId);

            return Ok(new
            {
                success = true,
                data = seatIds
            });
        }

        [HttpGet("{orderId}")]
        public async Task<IActionResult> GetOrderById(int orderId)
        {
            var userIdClaim =
                User.FindFirst(ClaimTypes.NameIdentifier) ??
                User.FindFirst("sub");

            if (userIdClaim == null ||
                !int.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Token không hợp lệ."
                });
            }

            var order = await _orderService.GetOrderByOrderId(orderId, userId);

            if (order == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Không tìm thấy đơn hàng."
                });
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    order.OrderId,
                    order.EventId,
                    order.EventName,
                    order.PosterUrl,
                    order.OrderDate,
                    order.TotalAmount,
                    order.DiscountAmount,
                    order.FinalAmount,
                    order.PaymentMethod,
                    order.Status,
                    order.ExpiresAt,
                    orderDetails = order.OrderDetails.Select(detail => new
                    {
                        detail.OrderDetailId,
                        detail.TicketTypeId,
                        detail.TicketTypeName,
                        detail.Quantity,
                        detail.UnitPrice,
                        detail.SeatIds
                    })
                }
            });
        }

        [HttpPut("{orderId}/confirm-payment")]
        public async Task<IActionResult> ConfirmPayment(
            int orderId,
            [FromBody] ConfirmOrderPaymentRequestDTO request,
            [FromHeader(Name = "X-Internal-Api-Key")] string apiKey,
            [FromServices] IConfiguration configuration)
        {
            var expectedKey = configuration["InternalApiKey"];

            if (string.IsNullOrWhiteSpace(expectedKey) ||
                !string.Equals(apiKey, expectedKey, StringComparison.Ordinal))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Invalid service credentials."
                });
            }

            if (orderId <= 0 ||
                request.Amount <= 0 ||
                string.IsNullOrWhiteSpace(request.TransactionRef))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Invalid payment confirmation."
                });
            }

            try
            {
                await _orderService.ConfirmPaymentAsync(
                    orderId,
                    request.Amount,
                    request.TransactionRef);

                return Ok(new
                {
                    success = true,
                    message = "Order payment confirmed."
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("tickets/{ticketId:int}/qr/rotate")]
        public async Task<IActionResult> RotateTicketQr(int ticketId)
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                        ?? User.FindFirst("sub");

            if (claim == null || !int.TryParse(claim.Value, out var userId))
                return Unauthorized();

            try
            {
                var result = await _orderService.RotateTicketQrAsync(ticketId, userId);

                return Ok(new
                {
                    success = true,
                    data = result
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { success = false, message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("my-orders")]
        public async Task<IActionResult> GetMyOrderHistory()
        {
            var customerIdValue =
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                User.FindFirst("sub")?.Value;

            if (!int.TryParse(customerIdValue, out var customerId))
            {
                return Unauthorized();
            }

            var orders = await _orderService.GetOrderHistoryAsync(customerId);

            return Ok(new
            {
                success = true,
                data = orders
            });
        }
    }
}