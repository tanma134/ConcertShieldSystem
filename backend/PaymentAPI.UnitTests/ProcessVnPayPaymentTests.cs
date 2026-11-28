using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Moq;
using PaymentAPI.API;
using PaymentAPI.Controllers;
using PaymentAPI.DTOs;
using PaymentAPI.Models;
using PaymentAPI.Repositories;
using PaymentAPI.Services;
using Xunit;

namespace PaymentAPI.Tests;

public class ProcessVnPayPaymentTests
{
    private const string Secret = "TEST_SECRET";
    private const int OrderId = 123;
    private const long VnPayAmount = 40_000_000; // 400,000 VND x 100

    // ---------- VnPayService.HandleVNPayReturn ----------

    [Fact]
    public async Task HandleVNPayReturn_ValidSuccessfulPayment_UpdatesTransactionAsSuccess()
    {
        var paymentTransactions = new Mock<IPaymentTransactionService>();
        paymentTransactions
            .Setup(s => s.UpdatePaymentTransactionAsync(
                OrderId,
                VnPayAmount,
                "987654321",
                "Success",
                It.IsAny<string?>()))
            .ReturnsAsync(true);

        var service = CreateVnPayService(paymentTransactions.Object);
        var callback = CreateSignedCallback();

        var result = await service.HandleVNPayReturn(
            callback.Query,
            callback.RawQuery);

        Assert.Equal("Payment successful", result);

        paymentTransactions.Verify(s => s.UpdatePaymentTransactionAsync(
            OrderId,
            VnPayAmount,
            "987654321",
            "Success",
            It.Is<string?>(payload =>
                !string.IsNullOrWhiteSpace(payload) &&
                payload.Contains("vnp_ResponseCode"))),
            Times.Once);
    }

    [Theory]
    [InlineData("24", "02")]
    [InlineData("05", "05")]
    [InlineData("00", "01")]
    public async Task HandleVNPayReturn_UnsuccessfulPayment_UpdatesTransactionAsFailed(
        string responseCode,
        string transactionStatus)
    {
        var paymentTransactions = new Mock<IPaymentTransactionService>();
        paymentTransactions
            .Setup(s => s.UpdatePaymentTransactionAsync(
                OrderId,
                VnPayAmount,
                "987654321",
                "Failed",
                It.IsAny<string?>()))
            .ReturnsAsync(true);

        var service = CreateVnPayService(paymentTransactions.Object);
        var callback = CreateSignedCallback(
            responseCode: responseCode,
            transactionStatus: transactionStatus);

        var result = await service.HandleVNPayReturn(
            callback.Query,
            callback.RawQuery);

        Assert.Equal("Payment failed or cancelled", result);

        paymentTransactions.Verify(s => s.UpdatePaymentTransactionAsync(
            OrderId,
            VnPayAmount,
            "987654321",
            "Failed",
            It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleVNPayReturn_InvalidSignature_DoesNotUpdateTransaction()
    {
        var paymentTransactions = new Mock<IPaymentTransactionService>();
        var service = CreateVnPayService(paymentTransactions.Object);
        var callback = CreateSignedCallback();

        var tamperedQuery = new QueryCollection(
            new Dictionary<string, StringValues>
            {
                ["vnp_Amount"] = VnPayAmount.ToString(),
                ["vnp_ResponseCode"] = "00",
                ["vnp_TransactionNo"] = "987654321",
                ["vnp_TransactionStatus"] = "00",
                ["vnp_TxnRef"] = OrderId.ToString(),
                ["vnp_SecureHash"] = "invalid-signature"
            });

        var result = await service.HandleVNPayReturn(
            tamperedQuery,
            callback.RawQuery);

        Assert.Equal("Invalid signature", result);

        paymentTransactions.Verify(s => s.UpdatePaymentTransactionAsync(
            It.IsAny<int>(),
            It.IsAny<long>(),
            It.IsAny<string?>(),
            It.IsAny<string>(),
            It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleVNPayReturn_NonNumericAmount_ReturnsInvalidAmount()
    {
        var paymentTransactions = new Mock<IPaymentTransactionService>();
        var service = CreateVnPayService(paymentTransactions.Object);
        var callback = CreateSignedCallback(amount: "not-a-number");

        var result = await service.HandleVNPayReturn(
            callback.Query,
            callback.RawQuery);

        Assert.Equal("Invalid amount", result);

        paymentTransactions.Verify(s => s.UpdatePaymentTransactionAsync(
            It.IsAny<int>(),
            It.IsAny<long>(),
            It.IsAny<string?>(),
            It.IsAny<string>(),
            It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleVNPayReturn_NonNumericOrderId_ReturnsInvalidOrderId()
    {
        var paymentTransactions = new Mock<IPaymentTransactionService>();
        var service = CreateVnPayService(paymentTransactions.Object);
        var callback = CreateSignedCallback(orderId: "bad-order-id");

        var result = await service.HandleVNPayReturn(
            callback.Query,
            callback.RawQuery);

        Assert.Equal("Invalid order id", result);

        paymentTransactions.Verify(s => s.UpdatePaymentTransactionAsync(
            It.IsAny<int>(),
            It.IsAny<long>(),
            It.IsAny<string?>(),
            It.IsAny<string>(),
            It.IsAny<string?>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleVNPayReturn_TransactionNotUpdated_ReturnsMismatchMessage()
    {
        var paymentTransactions = new Mock<IPaymentTransactionService>();
        paymentTransactions
            .Setup(s => s.UpdatePaymentTransactionAsync(
                OrderId,
                VnPayAmount,
                "987654321",
                "Success",
                It.IsAny<string?>()))
            .ReturnsAsync(false);

        var service = CreateVnPayService(paymentTransactions.Object);
        var callback = CreateSignedCallback();

        var result = await service.HandleVNPayReturn(
            callback.Query,
            callback.RawQuery);

        Assert.Equal(
            "Payment transaction not found or amount does not match",
            result);
    }

    // ---------- VnPayController.VnPayReturn ----------

    [Fact]
    public async Task VnPayReturn_SuccessfulPayment_ConfirmsOrderAndRedirectsSuccess()
    {
        var vnPay = new Mock<IVnPayService>();
        vnPay.Setup(s => s.HandleVNPayReturn(
                It.IsAny<IQueryCollection>(),
                It.IsAny<string>()))
            .ReturnsAsync("Payment successful");

        var ticketApi = new Mock<ITicketApiClient>();
        ticketApi.Setup(c => c.ConfirmOrderPaymentWithReasonAsync(
                OrderId,
                It.Is<ConfirmOrderPaymentRequestDTO>(request =>
                    request.Amount == 400_000 &&
                    request.TransactionRef == "987654321")))
            .ReturnsAsync((true, (string?)null));

        var controller = CreateController(vnPay.Object, ticketApi.Object);
        SetCallbackQuery(controller, VnPayAmount.ToString());

        var result = await controller.VnPayReturn();

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Contains("status=success", redirect.Url);
        Assert.Contains($"orderId={OrderId}", redirect.Url);

        ticketApi.Verify(c => c.ConfirmOrderPaymentWithReasonAsync(
            OrderId,
            It.Is<ConfirmOrderPaymentRequestDTO>(request =>
                request.Amount == 400_000 &&
                request.TransactionRef == "987654321")),
            Times.Once);
    }

    [Fact]
    public async Task VnPayReturn_InvalidSignature_RedirectsWithInvalidStatus()
    {
        var vnPay = new Mock<IVnPayService>();
        vnPay.Setup(s => s.HandleVNPayReturn(
                It.IsAny<IQueryCollection>(),
                It.IsAny<string>()))
            .ReturnsAsync("Invalid signature");

        var ticketApi = new Mock<ITicketApiClient>();
        var controller = CreateController(vnPay.Object, ticketApi.Object);
        SetCallbackQuery(controller, VnPayAmount.ToString());

        var result = await controller.VnPayReturn();

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Contains("status=invalid", redirect.Url);

        ticketApi.Verify(c => c.ConfirmOrderPaymentWithReasonAsync(
            It.IsAny<int>(),
            It.IsAny<ConfirmOrderPaymentRequestDTO>()),
            Times.Never);
    }

    [Fact]
    public async Task VnPayReturn_InvalidVnPayAmount_RedirectsPendingWithoutConfirmingOrder()
    {
        var vnPay = new Mock<IVnPayService>();
        vnPay.Setup(s => s.HandleVNPayReturn(
                It.IsAny<IQueryCollection>(),
                It.IsAny<string>()))
            .ReturnsAsync("Payment successful");

        var ticketApi = new Mock<ITicketApiClient>();
        var controller = CreateController(vnPay.Object, ticketApi.Object);
        SetCallbackQuery(controller, "40000001"); // Not divisible by 100.

        var result = await controller.VnPayReturn();

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Contains("status=pending", redirect.Url);

        ticketApi.Verify(c => c.ConfirmOrderPaymentWithReasonAsync(
            It.IsAny<int>(),
            It.IsAny<ConfirmOrderPaymentRequestDTO>()),
            Times.Never);
    }

    [Fact]
    public async Task VnPayReturn_TicketApiDoesNotConfirmOrder_RedirectsPending()
    {
        var vnPay = new Mock<IVnPayService>();
        vnPay.Setup(s => s.HandleVNPayReturn(
                It.IsAny<IQueryCollection>(),
                It.IsAny<string>()))
            .ReturnsAsync("Payment successful");

        var ticketApi = new Mock<ITicketApiClient>();
        ticketApi.Setup(c => c.ConfirmOrderPaymentWithReasonAsync(
                OrderId,
                It.IsAny<ConfirmOrderPaymentRequestDTO>()))
            .ReturnsAsync((false, "Order confirmation failed."));

        var controller = CreateController(vnPay.Object, ticketApi.Object);
        SetCallbackQuery(controller, VnPayAmount.ToString());

        var result = await controller.VnPayReturn();

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Contains("status=pending", redirect.Url);
        Assert.Contains("reason=Order", redirect.Url);
    }

    // ---------- VnPayController.VnPayIpn ----------

    [Fact]
    public async Task VnPayIpn_SuccessfulPayment_ConfirmsOrderAndReturnsSuccessCode()
    {
        var vnPay = new Mock<IVnPayService>();
        vnPay.Setup(s => s.HandleVNPayReturn(
                It.IsAny<IQueryCollection>(),
                It.IsAny<string>()))
            .ReturnsAsync("Payment successful");

        var ticketApi = new Mock<ITicketApiClient>();
        ticketApi.Setup(c => c.ConfirmOrderPaymentAsync(
                OrderId,
                It.Is<ConfirmOrderPaymentRequestDTO>(request =>
                    request.Amount == 400_000 &&
                    request.TransactionRef == "987654321")))
            .ReturnsAsync(true);

        var controller = CreateController(vnPay.Object, ticketApi.Object);
        SetCallbackQuery(controller, VnPayAmount.ToString());

        var result = await controller.VnPayIpn();

        AssertRspCode(result, "00");

        ticketApi.Verify(c => c.ConfirmOrderPaymentAsync(
            OrderId,
            It.Is<ConfirmOrderPaymentRequestDTO>(request =>
                request.Amount == 400_000 &&
                request.TransactionRef == "987654321")),
            Times.Once);
    }

    [Fact]
    public async Task VnPayIpn_InvalidSignature_ReturnsCode97()
    {
        var vnPay = new Mock<IVnPayService>();
        vnPay.Setup(s => s.HandleVNPayReturn(
                It.IsAny<IQueryCollection>(),
                It.IsAny<string>()))
            .ReturnsAsync("Invalid signature");

        var controller = CreateController(
            vnPay.Object,
            Mock.Of<ITicketApiClient>());
        SetCallbackQuery(controller, VnPayAmount.ToString());

        var result = await controller.VnPayIpn();

        AssertRspCode(result, "97");
    }

    [Fact]
    public async Task VnPayIpn_InvalidAmount_ReturnsCode04WithoutConfirmingOrder()
    {
        var vnPay = new Mock<IVnPayService>();
        vnPay.Setup(s => s.HandleVNPayReturn(
                It.IsAny<IQueryCollection>(),
                It.IsAny<string>()))
            .ReturnsAsync("Payment successful");

        var ticketApi = new Mock<ITicketApiClient>();
        var controller = CreateController(vnPay.Object, ticketApi.Object);
        SetCallbackQuery(controller, "40000001");

        var result = await controller.VnPayIpn();

        AssertRspCode(result, "04");

        ticketApi.Verify(c => c.ConfirmOrderPaymentAsync(
            It.IsAny<int>(),
            It.IsAny<ConfirmOrderPaymentRequestDTO>()),
            Times.Never);
    }

    [Fact]
    public async Task VnPayIpn_TicketApiConfirmationFails_ReturnsCode99()
    {
        var vnPay = new Mock<IVnPayService>();
        vnPay.Setup(s => s.HandleVNPayReturn(
                It.IsAny<IQueryCollection>(),
                It.IsAny<string>()))
            .ReturnsAsync("Payment successful");

        var ticketApi = new Mock<ITicketApiClient>();
        ticketApi.Setup(c => c.ConfirmOrderPaymentAsync(
                OrderId,
                It.IsAny<ConfirmOrderPaymentRequestDTO>()))
            .ReturnsAsync(false);

        var controller = CreateController(vnPay.Object, ticketApi.Object);
        SetCallbackQuery(controller, VnPayAmount.ToString());

        var result = await controller.VnPayIpn();

        AssertRspCode(result, "99");
    }

    [Fact]
    public async Task VnPayIpn_FailedOrCancelledPaymentAcknowledgesCallback()
    {
        var vnPay = new Mock<IVnPayService>();
        vnPay.Setup(s => s.HandleVNPayReturn(
                It.IsAny<IQueryCollection>(),
                It.IsAny<string>()))
            .ReturnsAsync("Payment failed or cancelled");

        var ticketApi = new Mock<ITicketApiClient>();
        var controller = CreateController(vnPay.Object, ticketApi.Object);
        SetCallbackQuery(controller, VnPayAmount.ToString());

        var result = await controller.VnPayIpn();

        AssertRspCode(result, "00");

        ticketApi.Verify(c => c.ConfirmOrderPaymentAsync(
            It.IsAny<int>(),
            It.IsAny<ConfirmOrderPaymentRequestDTO>()),
            Times.Never);
    }

    // ---------- Helpers ----------

    private static VnPayService CreateVnPayService(
        IPaymentTransactionService paymentTransactions)
    {
        var options = Options.Create(new VnPayConfig
        {
            TmnCode = "TEST",
            HashSecret = Secret,
            BaseUrl = "https://sandbox.vnpayment.vn/payment",
            ReturnUrl = "https://localhost/payment/return"
        });

        return new VnPayService(
            options,
            Mock.Of<IHttpClientFactory>(),
            Mock.Of<IConfiguration>(),
            paymentTransactions);
    }

    private static (
        IQueryCollection Query,
        string RawQuery)
        CreateSignedCallback(
            string responseCode = "00",
            string transactionStatus = "00",
            string amount = "40000000",
            string orderId = "123")
    {
        var parameters = new Dictionary<string, string>
        {
            ["vnp_Amount"] = amount,
            ["vnp_ResponseCode"] = responseCode,
            ["vnp_TransactionNo"] = "987654321",
            ["vnp_TransactionStatus"] = transactionStatus,
            ["vnp_TxnRef"] = orderId
        };

        var unsignedQuery = string.Join(
            "&",
            parameters
                .Select(p => $"{p.Key}={p.Value}")
                .OrderBy(p => p, StringComparer.Ordinal));

        var signature = ComputeHmacSha512(Secret, unsignedQuery);
        var rawQuery = $"?{unsignedQuery}&vnp_SecureHash={signature}";

        var queryValues = parameters.ToDictionary(
            p => p.Key,
            p => new StringValues(p.Value));

        queryValues["vnp_SecureHash"] = new StringValues(signature);

        return (new QueryCollection(queryValues), rawQuery);
    }

    private static string ComputeHmacSha512(string secret, string data)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secret));
        var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));

        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static VnPayController CreateController(
        IVnPayService vnPay,
        ITicketApiClient ticketApi)
    {
        var controller = new VnPayController(
            vnPay,
            Mock.Of<IPaymentTransactionService>(),
            ticketApi,
            Mock.Of<IPaymentTransactionRepository>(),
            Microsoft.Extensions.Logging.Abstractions
                .NullLogger<VnPayController>.Instance,
            Mock.Of<PaymentAPI.Services.IAuditLogClient>());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        return controller;
    }

    private static void SetCallbackQuery(
        VnPayController controller,
        string amount)
    {
        var values = new Dictionary<string, StringValues>
        {
            ["vnp_TxnRef"] = OrderId.ToString(),
            ["vnp_Amount"] = amount,
            ["vnp_TransactionNo"] = "987654321",
            ["vnp_ResponseCode"] = "00",
            ["vnp_TransactionStatus"] = "00"
        };

        controller.ControllerContext.HttpContext.Request.Query =
            new QueryCollection(values);

        controller.ControllerContext.HttpContext.Request.QueryString =
            new QueryString(
                $"?vnp_TxnRef={OrderId}&vnp_Amount={amount}" +
                "&vnp_TransactionNo=987654321&vnp_ResponseCode=00" +
                "&vnp_TransactionStatus=00");
    }

    private static void AssertRspCode(
        IActionResult actionResult,
        string expectedCode)
    {
        var ok = Assert.IsType<OkObjectResult>(actionResult);

        var actualCode = ok.Value!
            .GetType()
            .GetProperty("RspCode")!
            .GetValue(ok.Value)!
            .ToString();

        Assert.Equal(expectedCode, actualCode);
    }
}