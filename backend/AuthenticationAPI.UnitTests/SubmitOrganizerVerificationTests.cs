using AuthenticationAPI.DTOs;
using AuthenticationAPI.Models;
using AuthenticationAPI.Repositories;
using AuthenticationAPI.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AuthenticationAPI.UnitTests;

/// <summary>
/// Unit Tests for Function_162_SubmitOrganizerVer (SubmitVerification / SubmitVerificationAsync)
/// Total Test Cases: 9 (UTCID01..UTCID09: N=2, B=2, A=5)
/// </summary>
public sealed class SubmitOrganizerVerificationTests
{
    private static OrganizerRequestService CreateService(
        Mock<IOrganizerRequestRepository> requestRepo,
        Mock<IUserRepository>? userRepo = null)
    {
        var users = userRepo ?? new Mock<IUserRepository>();
        return new OrganizerRequestService(
            requestRepo.Object,
            users.Object,
            Mock.Of<ILogger<OrganizerRequestService>>());
    }

    [Fact]
    public async Task SubmitVerification_UTCID01_Valid10DigitTaxCodeAndTwoDocuments_CreatesPendingRequest()
    {
        var repo = new Mock<IOrganizerRequestRepository>();
        repo.Setup(r => r.GetPendingByUserIdAsync(10)).ReturnsAsync((OrganizerRequest?)null);
        var service = CreateService(repo);

        var result = await service.SubmitVerificationAsync(10, new SubmitOrganizerVerificationDTO
        {
            BusinessName = "Saigon Sound LLC",
            TaxCode = "0102030405",
            DocumentUrls = new List<string>
            {
                "https://s3.bucket/doc1.pdf",
                "https://s3.bucket/doc2.pdf"
            },
            ContactEmail = "contact@saigonsound.com"
        }, callerRole: "Organizer");

        Assert.NotNull(result);
        Assert.True(result.RequestId > 0);
        Assert.Equal("PENDING", result.Status);
        Assert.Equal("Saigon Sound LLC", result.CompanyName);
        Assert.Equal("0102030405", result.TaxCode);
        Assert.Equal(2, result.DocumentsCount);
        repo.Verify(r => r.AddAsync(It.IsAny<OrganizerRequest>()), Times.Once);
        repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task SubmitVerification_UTCID02_Valid13DigitBranchTaxCode_CreatesPendingRequest()
    {
        var repo = new Mock<IOrganizerRequestRepository>();
        repo.Setup(r => r.GetPendingByUserIdAsync(11)).ReturnsAsync((OrganizerRequest?)null);
        var service = CreateService(repo);

        var result = await service.SubmitVerificationAsync(11, new SubmitOrganizerVerificationDTO
        {
            BusinessName = "Hanoi Live Events Co.",
            TaxCode = "0102030405-001",
            DocumentUrls = new List<string>
            {
                "https://s3.bucket/doc1.pdf",
                "https://s3.bucket/doc2.pdf"
            },
            ContactEmail = "contact@saigonsound.com"
        }, callerRole: "Organizer");

        Assert.NotNull(result);
        Assert.True(result.RequestId > 0);
        Assert.Equal("PENDING", result.Status);
        Assert.Equal("Hanoi Live Events Co.", result.CompanyName);
        Assert.Equal("0102030405-001", result.TaxCode);
    }

    [Fact]
    public async Task SubmitVerification_UTCID03_BoundaryMinimumOneDocumentUrl_CreatesPendingRequest()
    {
        var repo = new Mock<IOrganizerRequestRepository>();
        repo.Setup(r => r.GetPendingByUserIdAsync(12)).ReturnsAsync((OrganizerRequest?)null);
        var service = CreateService(repo);

        var result = await service.SubmitVerificationAsync(12, new SubmitOrganizerVerificationDTO
        {
            BusinessName = "Saigon Sound LLC",
            TaxCode = "0102030405",
            DocumentUrls = new List<string> { "https://s3.bucket/doc1.pdf" },
            ContactEmail = "contact@saigonsound.com"
        }, callerRole: "Organizer");

        Assert.True(result.RequestId > 0);
        Assert.Equal("PENDING", result.Status);
        Assert.Equal(1, result.DocumentsCount);
    }

    [Fact]
    public async Task SubmitVerification_UTCID04_BoundaryMaximumFiveDocumentUrls_CreatesPendingRequest()
    {
        var repo = new Mock<IOrganizerRequestRepository>();
        repo.Setup(r => r.GetPendingByUserIdAsync(13)).ReturnsAsync((OrganizerRequest?)null);
        var service = CreateService(repo);

        var result = await service.SubmitVerificationAsync(13, new SubmitOrganizerVerificationDTO
        {
            BusinessName = "Saigon Sound LLC",
            TaxCode = "0102030405",
            DocumentUrls = new List<string>
            {
                "https://s3.bucket/doc1.pdf",
                "https://s3.bucket/doc2.pdf",
                "https://s3.bucket/doc3.pdf",
                "https://s3.bucket/doc4.pdf",
                "https://s3.bucket/doc5.pdf"
            },
            ContactEmail = "contact@saigonsound.com"
        }, callerRole: "Organizer");

        Assert.True(result.RequestId > 0);
        Assert.Equal("PENDING", result.Status);
        Assert.Equal(5, result.DocumentsCount);
    }

    [Fact]
    public async Task SubmitVerification_UTCID05_AlreadyHasActivePendingRequest_ThrowsInvalidOperationException()
    {
        var repo = new Mock<IOrganizerRequestRepository>();
        var existingPending = TestData.CreateOrganizerRequest(100, "Pending", TestData.CreateUser(14, "Organizer"));
        repo.Setup(r => r.GetPendingByUserIdAsync(14)).ReturnsAsync(existingPending);
        var service = CreateService(repo);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SubmitVerificationAsync(14, new SubmitOrganizerVerificationDTO
            {
                BusinessName = "Saigon Sound LLC",
                TaxCode = "0102030405",
                DocumentUrls = new List<string>
                {
                    "https://s3.bucket/doc1.pdf",
                    "https://s3.bucket/doc2.pdf"
                },
                ContactEmail = "contact@saigonsound.com"
            }, callerRole: "Organizer"));

        Assert.Equal("You already have a pending verification request", ex.Message);
    }

    [Fact]
    public async Task SubmitVerification_UTCID06_EmptyDocumentUrlsList_ThrowsArgumentException()
    {
        var repo = new Mock<IOrganizerRequestRepository>();
        var service = CreateService(repo);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SubmitVerificationAsync(15, new SubmitOrganizerVerificationDTO
            {
                BusinessName = "Saigon Sound LLC",
                TaxCode = "0102030405",
                DocumentUrls = new List<string>(),
                ContactEmail = "contact@saigonsound.com"
            }, callerRole: "Organizer"));

        Assert.Equal("At least one document URL is required", ex.Message);
    }

    [Fact]
    public async Task SubmitVerification_UTCID07_InvalidTaxCodeFormat_ThrowsArgumentException()
    {
        var repo = new Mock<IOrganizerRequestRepository>();
        var service = CreateService(repo);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SubmitVerificationAsync(16, new SubmitOrganizerVerificationDTO
            {
                BusinessName = "Saigon Sound LLC",
                TaxCode = "INVALID_TAX",
                DocumentUrls = new List<string>
                {
                    "https://s3.bucket/doc1.pdf",
                    "https://s3.bucket/doc2.pdf"
                },
                ContactEmail = "contact@saigonsound.com"
            }, callerRole: "Organizer"));

        Assert.Equal("Invalid tax code format", ex.Message);
    }

    [Fact]
    public async Task SubmitVerification_UTCID08_InvalidContactEmailFormat_ThrowsArgumentException()
    {
        var repo = new Mock<IOrganizerRequestRepository>();
        var service = CreateService(repo);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SubmitVerificationAsync(17, new SubmitOrganizerVerificationDTO
            {
                BusinessName = "Saigon Sound LLC",
                TaxCode = "0102030405",
                DocumentUrls = new List<string>
                {
                    "https://s3.bucket/doc1.pdf",
                    "https://s3.bucket/doc2.pdf"
                },
                ContactEmail = "invalid-email-format"
            }, callerRole: "Organizer"));

        Assert.Equal("Invalid contact email address", ex.Message);
    }

    [Fact]
    public async Task SubmitVerification_UTCID09_CallerIsCustomerWithoutOrganizerRole_ThrowsUnauthorizedAccessException()
    {
        var repo = new Mock<IOrganizerRequestRepository>();
        var service = CreateService(repo);

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.SubmitVerificationAsync(18, new SubmitOrganizerVerificationDTO
            {
                BusinessName = "Saigon Sound LLC",
                TaxCode = "0102030405",
                DocumentUrls = new List<string>
                {
                    "https://s3.bucket/doc1.pdf",
                    "https://s3.bucket/doc2.pdf"
                },
                ContactEmail = "contact@saigonsound.com"
            }, callerRole: "Customer"));

        Assert.Equal("Organizer role required to submit KYC", ex.Message);
    }
}
