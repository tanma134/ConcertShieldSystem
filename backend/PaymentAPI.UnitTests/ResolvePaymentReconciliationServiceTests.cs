using Moq;
using PaymentAPI.API;
using PaymentAPI.DTOs;
using PaymentAPI.Models;
using PaymentAPI.Repositories;
using PaymentAPI.Services;
using Xunit;

namespace PaymentAPI.Tests;

public class ResolvePaymentReconciliationServiceTests
{
    private const int TransactionId = 77;
    private const int OrderId = 42;
    private const int AdminUserId = 9;
    private const long Amount = 100_000;
    private const string GatewayReference = "vnp-ref-123";

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task T01_NonPositiveTransactionIdIsRejected(int transactionId)
    {
        var rig = CreateRig();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            rig.Service.ResolveExceptionAsync(
                transactionId,
                CreateRequest("ManualReview"),
                AdminUserId));

        VerifyNoResolutionSaved(rig.Resolutions);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task T02_EmptyResolutionNoteIsRejected(string? note)
    {
        var rig = CreateRig();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            rig.Service.ResolveExceptionAsync(
                TransactionId,
                CreateRequest("ManualReview", note: note),
                AdminUserId));

        VerifyNoResolutionSaved(rig.Resolutions);
    }

    [Fact]
    public async Task T03_UnknownResolutionActionIsRejected()
    {
        var rig = CreateRig();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            rig.Service.ResolveExceptionAsync(
                TransactionId,
                CreateRequest("UnknownAction"),
                AdminUserId));

        VerifyTransactionIsNotLoaded(rig.Repository);
        VerifyNoResolutionSaved(rig.Resolutions);
    }

    [Fact]
    public async Task T04_UnknownResolutionStatusIsRejected()
    {
        var rig = CreateRig();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            rig.Service.ResolveExceptionAsync(
                TransactionId,
                CreateRequest("ManualReview", status: "Pending"),
                AdminUserId));

        VerifyTransactionIsNotLoaded(rig.Repository);
        VerifyNoResolutionSaved(rig.Resolutions);
    }

    [Fact]
    public async Task T05_AutomaticActionWithDismissedStatusIsRejected()
    {
        var rig = CreateRig();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            rig.Service.ResolveExceptionAsync(
                TransactionId,
                CreateRequest("ConfirmPayment", status: "Dismissed"),
                AdminUserId));

        VerifyTransactionIsNotLoaded(rig.Repository);
        VerifyNoResolutionSaved(rig.Resolutions);
    }

    [Fact]
    public async Task T06_TransactionDoesNotExist_ThrowsKeyNotFoundException()
    {
        var rig = CreateRig();

        rig.Repository
            .Setup(r => r.GetByIdAsync(TransactionId))
            .ReturnsAsync((PaymentTransaction?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            rig.Service.ResolveExceptionAsync(
                TransactionId,
                CreateRequest("ManualReview"),
                AdminUserId));

        VerifyNoResolutionSaved(rig.Resolutions);
    }

    [Fact]
    public async Task T07_ExceptionWasAlreadyResolved_IsRejected()
    {
        var rig = CreateRig();

        rig.Resolutions
            .Setup(r => r.ExistsByPaymentTransactionIdAsync(TransactionId))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rig.Service.ResolveExceptionAsync(
                TransactionId,
                CreateRequest("ManualReview"),
                AdminUserId));

        rig.TicketApi.Verify(
            c => c.GetOrderReconciliationInfoAsync(It.IsAny<int>()),
            Times.Never);

        VerifyNoResolutionSaved(rig.Resolutions);
    }

    [Fact]
    public async Task T08_AutomaticActionForFailedTransactionIsRejected()
    {
        var rig = CreateRig(
            transaction: CreateTransaction(status: "Failed"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rig.Service.ResolveExceptionAsync(
                TransactionId,
                CreateRequest("ConfirmPayment"),
                AdminUserId));

        rig.TicketApi.Verify(
            c => c.GetOrderReconciliationInfoAsync(It.IsAny<int>()),
            Times.Never);

        VerifyNoResolutionSaved(rig.Resolutions);
    }

    [Fact]
    public async Task T09_AutomaticActionWithoutGatewayReferenceIsRejected()
    {
        var rig = CreateRig(
            transaction: CreateTransaction(gatewayReference: null));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rig.Service.ResolveExceptionAsync(
                TransactionId,
                CreateRequest("ConfirmPayment"),
                AdminUserId));

        rig.TicketApi.Verify(
            c => c.GetOrderReconciliationInfoAsync(It.IsAny<int>()),
            Times.Never);

        VerifyNoResolutionSaved(rig.Resolutions);
    }

    [Fact]
    public async Task T10_OrderDoesNotExistInTicketApi_IsRejected()
    {
        var rig = CreateRig();

        rig.TicketApi
            .Setup(c => c.GetOrderReconciliationInfoAsync(OrderId))
            .ReturnsAsync((TicketOrderReconciliationInfoDto?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            rig.Service.ResolveExceptionAsync(
                TransactionId,
                CreateRequest("ConfirmPayment"),
                AdminUserId));

        VerifyNoResolutionSaved(rig.Resolutions);
    }

    [Fact]
    public async Task T11_PaymentAmountDoesNotMatchOrderAmount_IsRejected()
    {
        var rig = CreateRig(
            order: CreateOrder(finalAmount: Amount + 1));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rig.Service.ResolveExceptionAsync(
                TransactionId,
                CreateRequest("ConfirmPayment"),
                AdminUserId));

        rig.TicketApi.Verify(
            c => c.ConfirmOrderPaymentAsync(
                It.IsAny<int>(),
                It.IsAny<ConfirmOrderPaymentRequestDTO>()),
            Times.Never);

        VerifyNoResolutionSaved(rig.Resolutions);
    }

    [Fact]
    public async Task T12_ConfirmPaymentForNonPendingOrderIsRejected()
    {
        var rig = CreateRig(
            order: CreateOrder(status: "Paid"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rig.Service.ResolveExceptionAsync(
                TransactionId,
                CreateRequest("ConfirmPayment"),
                AdminUserId));

        rig.TicketApi.Verify(
            c => c.ConfirmOrderPaymentAsync(
                It.IsAny<int>(),
                It.IsAny<ConfirmOrderPaymentRequestDTO>()),
            Times.Never);

        VerifyNoResolutionSaved(rig.Resolutions);
    }

    [Fact]
    public async Task T13_TicketApiCannotConfirmPayment_IsRejected()
    {
        var rig = CreateRig();

        rig.TicketApi
            .Setup(c => c.ConfirmOrderPaymentAsync(
                OrderId,
                It.IsAny<ConfirmOrderPaymentRequestDTO>()))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rig.Service.ResolveExceptionAsync(
                TransactionId,
                CreateRequest("ConfirmPayment"),
                AdminUserId));

        VerifyNoResolutionSaved(rig.Resolutions);
    }

    [Fact]
    public async Task T14_ConfirmPaymentSuccess_SavesResolution()
    {
        var rig = CreateRig();

        var result = await rig.Service.ResolveExceptionAsync(
            TransactionId,
            CreateRequest(
                "ConfirmPayment",
                note: "  Confirmed with gateway record  "),
            AdminUserId);

        Assert.Equal(TransactionId, result.PaymentTransactionId);
        Assert.Equal("ConfirmPayment", result.ResolutionAction);
        Assert.Equal("Resolved", result.ResolutionStatus);
        Assert.Equal("Confirmed with gateway record", result.ResolutionNote);
        Assert.Equal(AdminUserId, result.ResolvedByUserId);

        rig.TicketApi.Verify(c => c.ConfirmOrderPaymentAsync(
            OrderId,
            It.Is<ConfirmOrderPaymentRequestDTO>(request =>
                request.Amount == Amount &&
                request.TransactionRef == GatewayReference)),
            Times.Once);

        rig.Resolutions.Verify(r => r.AddAsync(
            It.Is<PaymentReconciliationResolution>(resolution =>
                resolution.PaymentTransactionId == TransactionId &&
                resolution.ResolutionAction == "ConfirmPayment" &&
                resolution.ResolutionStatus == "Resolved" &&
                resolution.ResolutionNote == "Confirmed with gateway record" &&
                resolution.ResolvedByUserId == AdminUserId)),
            Times.Once);
    }

    [Fact]
    public async Task T15_RepairActionForUnpaidOrderIsRejected()
    {
        var rig = CreateRig(
            order: CreateOrder(
                status: "Pending",
                expectedTickets: 2,
                issuedTickets: 1));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rig.Service.ResolveExceptionAsync(
                TransactionId,
                CreateRequest("RepairPaidOrderArtifacts"),
                AdminUserId));

        rig.TicketApi.Verify(
            c => c.RepairPaidOrderArtifactsAsync(
                It.IsAny<int>(),
                It.IsAny<ConfirmOrderPaymentRequestDTO>()),
            Times.Never);

        VerifyNoResolutionSaved(rig.Resolutions);
    }

    [Fact]
    public async Task T16_RepairActionWhenNoTicketsOrQrAreMissing_IsRejected()
    {
        var rig = CreateRig(
            order: CreateOrder(
                status: "Paid",
                expectedTickets: 2,
                issuedTickets: 2,
                ticketsWithoutQr: 0));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rig.Service.ResolveExceptionAsync(
                TransactionId,
                CreateRequest("RepairPaidOrderArtifacts"),
                AdminUserId));

        rig.TicketApi.Verify(
            c => c.RepairPaidOrderArtifactsAsync(
                It.IsAny<int>(),
                It.IsAny<ConfirmOrderPaymentRequestDTO>()),
            Times.Never);

        VerifyNoResolutionSaved(rig.Resolutions);
    }

    [Fact]
    public async Task T17_TicketApiCannotRepairArtifacts_IsRejected()
    {
        var rig = CreateRig(
            order: CreateOrder(
                status: "Paid",
                expectedTickets: 2,
                issuedTickets: 1));

        rig.TicketApi
            .Setup(c => c.RepairPaidOrderArtifactsAsync(
                OrderId,
                It.IsAny<ConfirmOrderPaymentRequestDTO>()))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rig.Service.ResolveExceptionAsync(
                TransactionId,
                CreateRequest("RepairPaidOrderArtifacts"),
                AdminUserId));

        VerifyNoResolutionSaved(rig.Resolutions);
    }

    [Fact]
    public async Task T18_RepairMissingTicketsSuccess_SavesResolution()
    {
        var rig = CreateRig(
            order: CreateOrder(
                status: "Paid",
                expectedTickets: 2,
                issuedTickets: 1,
                ticketsWithoutQr: 0));

        var result = await rig.Service.ResolveExceptionAsync(
            TransactionId,
            CreateRequest("RepairPaidOrderArtifacts"),
            AdminUserId);

        Assert.Equal("RepairPaidOrderArtifacts", result.ResolutionAction);
        Assert.Equal("Resolved", result.ResolutionStatus);

        rig.TicketApi.Verify(c => c.RepairPaidOrderArtifactsAsync(
            OrderId,
            It.Is<ConfirmOrderPaymentRequestDTO>(request =>
                request.Amount == Amount &&
                request.TransactionRef == GatewayReference)),
            Times.Once);

        rig.Resolutions.Verify(r => r.AddAsync(
            It.Is<PaymentReconciliationResolution>(resolution =>
                resolution.PaymentTransactionId == TransactionId &&
                resolution.ResolutionAction == "RepairPaidOrderArtifacts" &&
                resolution.ResolutionStatus == "Resolved")),
            Times.Once);
    }

    [Fact]
    public async Task T19_RepairMissingQrSuccess_SavesResolution()
    {
        var rig = CreateRig(
            order: CreateOrder(
                status: "Paid",
                expectedTickets: 2,
                issuedTickets: 2,
                ticketsWithoutQr: 1));

        var result = await rig.Service.ResolveExceptionAsync(
            TransactionId,
            CreateRequest("RepairPaidOrderArtifacts"),
            AdminUserId);

        Assert.Equal("RepairPaidOrderArtifacts", result.ResolutionAction);
        Assert.Equal("Resolved", result.ResolutionStatus);

        rig.TicketApi.Verify(c => c.RepairPaidOrderArtifactsAsync(
            OrderId,
            It.IsAny<ConfirmOrderPaymentRequestDTO>()),
            Times.Once);

        rig.Resolutions.Verify(r => r.AddAsync(
            It.Is<PaymentReconciliationResolution>(resolution =>
                resolution.ResolutionAction == "RepairPaidOrderArtifacts" &&
                resolution.ResolutionStatus == "Resolved")),
            Times.Once);
    }

    [Fact]
    public async Task T20_ManualReviewResolved_TrimsActionStatusAndNote()
    {
        var rig = CreateRig();

        var result = await rig.Service.ResolveExceptionAsync(
            TransactionId,
            CreateRequest(
                " ManualReview ",
                status: " Resolved ",
                note: "  Reviewed gateway evidence  "),
            AdminUserId);

        Assert.Equal("ManualReview", result.ResolutionAction);
        Assert.Equal("Resolved", result.ResolutionStatus);
        Assert.Equal("Reviewed gateway evidence", result.ResolutionNote);
        Assert.Equal(AdminUserId, result.ResolvedByUserId);

        rig.Resolutions.Verify(r => r.AddAsync(
            It.Is<PaymentReconciliationResolution>(resolution =>
                resolution.ResolutionAction == "ManualReview" &&
                resolution.ResolutionStatus == "Resolved" &&
                resolution.ResolutionNote == "Reviewed gateway evidence" &&
                resolution.ResolvedByUserId == AdminUserId)),
            Times.Once);
    }

    [Fact]
    public async Task T21_ManualReviewDismissed_DoesNotCallTicketApi()
    {
        var rig = CreateRig(
            transaction: CreateTransaction(
                status: "Failed",
                gatewayReference: null));

        rig.TicketApi
            .Setup(c => c.GetOrderReconciliationInfoAsync(OrderId))
            .ReturnsAsync((TicketOrderReconciliationInfoDto?)null);

        var result = await rig.Service.ResolveExceptionAsync(
            TransactionId,
            CreateRequest(
                "ManualReview",
                status: "Dismissed",
                note: "Reviewed and dismissed"),
            AdminUserId);

        Assert.Equal("ManualReview", result.ResolutionAction);
        Assert.Equal("Dismissed", result.ResolutionStatus);
        Assert.Equal("Reviewed and dismissed", result.ResolutionNote);

        rig.TicketApi.Verify(
            c => c.GetOrderReconciliationInfoAsync(It.IsAny<int>()),
            Times.Never);

        rig.TicketApi.Verify(
            c => c.ConfirmOrderPaymentAsync(
                It.IsAny<int>(),
                It.IsAny<ConfirmOrderPaymentRequestDTO>()),
            Times.Never);

        rig.TicketApi.Verify(
            c => c.RepairPaidOrderArtifactsAsync(
                It.IsAny<int>(),
                It.IsAny<ConfirmOrderPaymentRequestDTO>()),
            Times.Never);

        rig.Resolutions.Verify(r => r.AddAsync(
            It.Is<PaymentReconciliationResolution>(resolution =>
                resolution.ResolutionAction == "ManualReview" &&
                resolution.ResolutionStatus == "Dismissed" &&
                resolution.ResolvedByUserId == AdminUserId)),
            Times.Once);
    }

    private static Rig CreateRig(
        PaymentTransaction? transaction = null,
        TicketOrderReconciliationInfoDto? order = null)
    {
        transaction ??= CreateTransaction();
        order ??= CreateOrder();

        var repository = new Mock<IPaymentTransactionRepository>();
        repository
            .Setup(r => r.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync(transaction);

        var ticketApi = new Mock<ITicketApiClient>();
        ticketApi
            .Setup(c => c.GetOrderReconciliationInfoAsync(It.IsAny<int>()))
            .ReturnsAsync(order);

        ticketApi
            .Setup(c => c.ConfirmOrderPaymentAsync(
                It.IsAny<int>(),
                It.IsAny<ConfirmOrderPaymentRequestDTO>()))
            .ReturnsAsync(true);

        ticketApi
            .Setup(c => c.RepairPaidOrderArtifactsAsync(
                It.IsAny<int>(),
                It.IsAny<ConfirmOrderPaymentRequestDTO>()))
            .ReturnsAsync(true);

        var resolutions =
            new Mock<IPaymentReconciliationResolutionRepository>();

        resolutions
            .Setup(r => r.ExistsByPaymentTransactionIdAsync(
                It.IsAny<int>()))
            .ReturnsAsync(false);

        resolutions
            .Setup(r => r.AddAsync(
                It.IsAny<PaymentReconciliationResolution>()))
            .Returns(Task.CompletedTask);

        var service = new PaymentReconciliationService(
            repository.Object,
            ticketApi.Object,
            resolutions.Object);

        return new Rig(service, repository, ticketApi, resolutions);
    }

    private static PaymentTransaction CreateTransaction(
        string status = "Success",
        string? gatewayReference = GatewayReference,
        long amount = Amount)
    {
        return new PaymentTransaction
        {
            PaymentTransactionId = TransactionId,
            OrderId = OrderId,
            Gateway = "VNPay",
            GatewayTransactionRef = gatewayReference,
            Amount = amount,
            Status = status,
            RequestedAt = DateTime.UtcNow.AddMinutes(-5),
            RespondedAt = DateTime.UtcNow.AddMinutes(-4)
        };
    }

    private static TicketOrderReconciliationInfoDto CreateOrder(
        string status = "Pending",
        long finalAmount = Amount,
        int expectedTickets = 1,
        int issuedTickets = 0,
        int ticketsWithoutQr = 0)
    {
        return new TicketOrderReconciliationInfoDto
        {
            OrderId = OrderId,
            EventId = 42,
            Status = status,
            FinalAmount = finalAmount,
            ExpectedTicketCount = expectedTickets,
            IssuedTicketCount = issuedTickets,
            TicketsWithoutQrCount = ticketsWithoutQr
        };
    }

    private static ResolvePaymentReconciliationRequestDto CreateRequest(
        string action,
        string status = "Resolved",
        string? note = "Reviewed exception")
    {
        return new ResolvePaymentReconciliationRequestDto
        {
            ResolutionAction = action,
            ResolutionStatus = status,
            ResolutionNote = note!
        };
    }

    private static void VerifyNoResolutionSaved(
        Mock<IPaymentReconciliationResolutionRepository> resolutions)
    {
        resolutions.Verify(r => r.AddAsync(
            It.IsAny<PaymentReconciliationResolution>()),
            Times.Never);
    }

    private static void VerifyTransactionIsNotLoaded(
        Mock<IPaymentTransactionRepository> repository)
    {
        repository.Verify(
            r => r.GetByIdAsync(It.IsAny<int>()),
            Times.Never);
    }

    private sealed class Rig
    {
        public Rig(
            PaymentReconciliationService service,
            Mock<IPaymentTransactionRepository> repository,
            Mock<ITicketApiClient> ticketApi,
            Mock<IPaymentReconciliationResolutionRepository> resolutions)
        {
            Service = service;
            Repository = repository;
            TicketApi = ticketApi;
            Resolutions = resolutions;
        }

        public PaymentReconciliationService Service { get; }
        public Mock<IPaymentTransactionRepository> Repository { get; }
        public Mock<ITicketApiClient> TicketApi { get; }
        public Mock<IPaymentReconciliationResolutionRepository> Resolutions { get; }
    }
}