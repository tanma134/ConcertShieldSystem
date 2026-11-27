using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TicketAPI.API;
using TicketAPI.Domain;
using TicketAPI.DTOs;
using TicketAPI.Models;
using TicketAPI.Repositories;
using TicketAPI.Services;
using Xunit;

namespace TicketAPI.UnitTests;

public class RefundAccountVerificationTests
{
    [Theory] [InlineData("ConfirmManualRefund")] [InlineData("VerifyRefundAccount")]
    public void AccountingEndpointsExcludeStaff(string method)
    {
        var attributes = typeof(TicketAPI.Controllers.TicketReturnsController).GetMethod(method)!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>();
        Assert.Contains(attributes, a => a.Roles == "Admin");
    }
    private static readonly string Proof = "data:application/pdf;base64," + Convert.ToBase64String(Encoding.ASCII.GetBytes("%PDF-1.4\nTEST ONLY\n%%EOF"));
    private static TicketReturnRequest Request() => new()
    {
        TicketReturnRequestId = 18, TicketId = 7049, RequesterUserId = 7, OrderId = 54,
        Status = ReturnRequestStatuses.Approved, RefundAmount = 2250000,
        BankName = "Liobank (OCB)", BankAccountNumber = "123456789", BankAccountName = "NGUYEN VAN AN",
        Ticket = new Ticket { TicketId = 7049, Order = new Order { OrderId = 54, EventId = 99, FullName = "Nguyen Van An" } }
    };
    private static VerifyRefundAccountDto Checks() => new()
    {
        IdentityName = "Nguyễn Văn An", BankReportedName = "NGUYEN VAN AN",
        IdentityEvidence = Proof, BankEvidence = Proof, CustomerConfirmationEvidence = Proof,
        Notes = "Test-only manual verification evidence.", IsSimulation = true
    };
    private static void Verify(TicketReturnRequest request) => request.BankVerificationJson = JsonSerializer.Serialize(RefundAccountVerification.Verify(request, Checks(), 1, true));

    [Fact] public void NamesMatchAcrossCaseAndVietnameseAccents()
    {
        var request = Request(); Verify(request);
        Assert.Equal(1, RefundAccountVerification.Read(request)!.VerifiedBy);
    }
    [Fact] public void DifferentIdentityCannotBeVerified()
    {
        var input = Checks(); input.IdentityName = "TRAN VAN B";
        Assert.Throws<ArgumentException>(() => RefundAccountVerification.Verify(Request(), input, 1, true));
    }
    [Fact] public void CustomerTypedNameCannotReplaceBankLookup()
    {
        var input = Checks(); input.BankReportedName = "LE VAN C";
        Assert.Throws<ArgumentException>(() => RefundAccountVerification.Verify(Request(), input, 1, true));
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void EveryEvidenceSourceIsRequired(int index)
    {
        var input = Checks();
        if (index == 0) input.IdentityEvidence = null;
        if (index == 1) input.BankEvidence = null;
        if (index == 2) input.CustomerConfirmationEvidence = null;
        Assert.Throws<ArgumentException>(() => RefundAccountVerification.Verify(Request(), input, 1, true));
    }
    [Fact] public void DifferentDestinationInvalidatesSavedChecks()
    {
        var request = Request(); Verify(request); request.BankAccountNumber = "999999999";
        Assert.Null(RefundAccountVerification.Read(request));
    }
    [Theory]
    [InlineData("123456789", "*****6789")]
    [InlineData("1234", "****")]
    public void AccountNumberMaskLeavesOnlyLastFourDigits(string input, string expected)
        => Assert.Equal(expected, RefundAccountVerification.MaskAccountNumber(input));
    [Fact] public void SimulationIsDisabledByDefault() => Assert.Throws<InvalidOperationException>(() => RefundAccountVerification.Verify(Request(), Checks(), 1, false));
    [Theory] [InlineData("javascript:alert(1)")] [InlineData("data:text/html;base64,PHNjcmlwdD4=")] [InlineData("data:application/pdf;base64,aGVsbG8=")]
    public void UnsafeOrMislabelledEvidenceIsRejected(string evidence) => Assert.Throws<ArgumentException>(() => RefundAccountVerification.ValidateEvidence(evidence, "Proof"));

    private sealed class NotificationHandler : HttpMessageHandler
    {
        public List<string> Messages { get; } = new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Messages.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        }
    }
    private static (TicketReturnService Service, Mock<IPaymentAPIClient> Payments, Mock<ITicketReturnRepository> Repository, List<string> Notifications) Service(TicketReturnRequest request, bool ledgerSuccess = true)
    {
        var repository = new Mock<ITicketReturnRepository>(); repository.Setup(r => r.GetForReviewAsync(18)).ReturnsAsync(request);
        repository.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);
        repository.Setup(r => r.SyncOrderRefundStatusAsync(54)).Returns(Task.CompletedTask);
        var payments = new Mock<IPaymentAPIClient>();
        payments.Setup(p => p.RecordManualRefundAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefundOutcome(ledgerSuccess, "SIM-TEST-18", ledgerSuccess ? null : "Unavailable"));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ManualRefund:AllowSimulation"] = "true" }).Build();
        var handler = new NotificationHandler();
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("ChangeNotification")).Returns(new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") });
        var notifier = new TicketReturnNotifier(factory.Object, NullLogger<TicketReturnNotifier>.Instance);
        return (new TicketReturnService(repository.Object, NullLogger<TicketReturnService>.Instance, Mock.Of<IEventApiClient>(), config, payments.Object, notifier), payments, repository, handler.Messages);
    }
    private static ConfirmManualRefundDto Transfer() => new() { Amount = 2250000, Reference = "TEST-18", TransferReceipt = Proof, IsSimulation = true };
    [Fact] public async Task DirectPayoutCannotBypassAccountChecks()
    {
        var request = Request(); var rig = Service(request);
        await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Service.ConfirmManualRefundAsync(1, 18, Transfer()));
        Assert.Equal(ReturnRequestStatuses.Approved, request.Status);
        rig.Repository.Verify(r => r.SaveChangesAsync(), Times.Never);
        Assert.Empty(rig.Payments.Invocations);
    }
    [Fact] public async Task ReceiptIsRequiredEvenWithValidAccountChecks()
    {
        var request = Request(); Verify(request); var rig = Service(request); var input = Transfer(); input.TransferReceipt = null;
        await Assert.ThrowsAsync<ArgumentException>(() => rig.Service.ConfirmManualRefundAsync(1, 18, input));
        Assert.Equal(ReturnRequestStatuses.Approved, request.Status);
    }
    [Fact] public async Task SimulatedChecksCannotConfirmRealTransfer()
    {
        var request = Request(); Verify(request); var rig = Service(request); var input = Transfer(); input.IsSimulation = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Service.ConfirmManualRefundAsync(1, 18, input));
    }
    [Fact] public async Task LedgerFailureDoesNotMarkRefundComplete()
    {
        var request = Request(); Verify(request); var rig = Service(request, false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Service.ConfirmManualRefundAsync(1, 18, Transfer()));
        Assert.Equal(ReturnRequestStatuses.Approved, request.Status);
        Assert.Null(request.RefundedAt); rig.Repository.Verify(r => r.SaveChangesAsync(), Times.Never);
        Assert.Empty(rig.Notifications);
    }
    [Fact] public async Task SimulationPersistsReceiptAndAccountingAudit()
    {
        var request = Request(); Verify(request); var rig = Service(request);
        var result = await rig.Service.ConfirmManualRefundAsync(1, 18, Transfer());
        Assert.Equal(ReturnRequestStatuses.Refunded, result.Status);
        Assert.StartsWith("SIM-", result.ManualRefundReference);
        Assert.True(result.BankVerification!.IsSimulation); Assert.Equal(Proof, result.BankVerification.TransferReceipt);
        var notification = Assert.Single(rig.Notifications);
        Assert.Contains("Refund simulation completed", notification);
        Assert.Contains("No real money was transferred", notification);
    }
}
